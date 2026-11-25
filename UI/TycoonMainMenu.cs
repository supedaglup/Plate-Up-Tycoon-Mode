using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenLib;
using KitchenLib.UI.PlateUp.PreferenceMenus;
using KitchenLib.Utils;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>"Tycoon" submenu, opened from a button on the title screen.</summary>
    public class TycoonMainMenu : KLMenu
    {
        public TycoonMainMenu(Transform container, ModuleList module_list) : base(container, module_list) { }

        public override void Setup(int player_id)
        {
            TycoonSaveData.Trace("Tycoon menu: opening");
            base.Setup(player_id);

            AddLabel("Tycoon Mode");
            New<SpacerElement>(true);
            for (int slot = 1; slot <= TycoonSaveData.COMPANY_SLOTS; slot++)
            {
                int chosen = slot;
                AddButton($"Company {slot}: {TycoonCompany.Summary(slot)}", _ =>
                {
                    TycoonSaveData.SelectCompany(chosen);
                    TycoonDeleteSavesMenu.LastResult = null;
                    RequestSubMenu(typeof(TycoonCompanyMenu), false, false);
                });
            }
            New<SpacerElement>(true);
            AddButton("Back", _ =>
            {
                TycoonSession.End();
                RequestPreviousMenu();
            });
        }

        public static void Register()
        {
            MainMenuPreferencesesMenu.RegisterUsableMenu(typeof(TycoonMainMenu));
            MainMenuPreferencesesMenu.RegisterUsableMenu(typeof(TycoonCompanyMenu));
            TycoonDeleteSavesMenu.Register();
        }

        public static void AddTitleScreenButton(StartMainMenu instance)
        {
            TycoonSession.OpenedFromTycoonMenu = false;

            MethodInfo addSubmenuButton = ReflectionUtils.GetMethod<StartMainMenu>("AddSubmenuButton");
            addSubmenuButton.Invoke(instance, new object[] { "Play Tycoon", typeof(TycoonMainMenu), false });
        }
    }

    /// <summary>Inserts the "Play Tycoon" button into the title screen.</summary>
    [HarmonyPatch(typeof(StartMainMenu), "Setup")]
    public static class StartMainMenuTycoonPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            bool afterOnline = false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldtoken && list[i].operand as System.Type == typeof(MultiplayerLoadingMenu))
                    afterOnline = true;

                if (afterOnline && list[i].opcode == OpCodes.Callvirt && (list[i].operand as MethodInfo)?.Name == "SetSelectable")
                {
                    list.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TycoonMainMenu), nameof(TycoonMainMenu.AddTitleScreenButton))),
                    });
                    Mod.LogInfo("Title screen patched: 'Play Tycoon' button added.");
                    return list;
                }
            }

            Mod.LogError("Could not find the Play Online button on the title screen; 'Play Tycoon' was not added.");
            return list;
        }
    }

    /// <summary>One company's page.</summary>
    public class TycoonCompanyMenu : KLMenu
    {
        public TycoonCompanyMenu(Transform container, ModuleList module_list) : base(container, module_list) { }

        private bool _starting;
        private string _error;

        public override void Setup(int player_id)
        {
            base.Setup(player_id);
            int slot = TycoonSaveData.Slot;
            AddLabel($"Company {slot}");
            AddInfo(TycoonCompany.Summary(slot));
            if (_error != null) AddInfo(_error);
            New<SpacerElement>(true);
            AddButton("Play Offline", _ => Open(typeof(SingleplayerMainMenu)));
            AddButton("Play Online", _ => Open(typeof(MultiplayerLoadingMenu)));
            New<SpacerElement>(true);
            if (TycoonDeleteSavesMenu.LastResult != null) AddInfo(TycoonDeleteSavesMenu.LastResult);
            AddSubmenuButton("Reset Progress", typeof(TycoonDeleteSavesMenu), false);
            New<SpacerElement>(true);
            AddButton("Back", _ => RequestPreviousMenu());
        }

        private async void Open(System.Type vanillaMenu)
        {
            if (_starting) return;
            _starting = true;
            bool ok = await TycoonSession.Begin();
            _starting = false;
            if (!ok)
            {
                _error = "Could not set up the Tycoon save files, so Tycoon Mode was not started.\nYour normal saves were not touched. Please send the Player.log.";
                ModuleList.Clear();
                Setup(0);
                return;
            }
            _error = null;
            RequestSubMenu(vanillaMenu, false, false);
        }
    }
}
