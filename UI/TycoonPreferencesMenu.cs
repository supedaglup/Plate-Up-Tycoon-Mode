using Kitchen;
using Kitchen.Modules;
using KitchenLib;
using KitchenLib.UI.PlateUp.PreferenceMenus;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>Options → Mod Preferences → Tycoon Mode.</summary>
    public class TycoonPreferencesMenu : KLMenu
    {
        public TycoonPreferencesMenu(Transform container, ModuleList module_list) : base(container, module_list) { }

        private static readonly List<bool> OnOff = new List<bool> { true, false };
        private static readonly List<string> OnOffLabels = new List<string> { "On", "Off" };
        private static int _versionPresses;

        public override void Setup(int player_id)
        {
            AddLabel("Tycoon Mode");
            AddButton(TycoonPreferences.DeveloperMode ? $"Version {Mod.MOD_VERSION} (developer mode)" : $"Version {Mod.MOD_VERSION}", _ =>
            {
                if (++_versionPresses < 5) return;
                _versionPresses = 0;
                TycoonPreferences.Set(TycoonPreferences.DEVELOPER_MODE, !TycoonPreferences.DeveloperMode);
                UnityEngine.Debug.Log($"[{Mod.MOD_NAME}] Developer mode {(TycoonPreferences.DeveloperMode ? "on" : "off")}.");
                ModuleList.Clear();
                Setup(0);
            }, 0, 1f, 0.2f);
            New<SpacerElement>(true);

            AddInfo("Tutorials (new characters and the HQ shopkeepers)");
            var tutorial = new Option<bool>(OnOff, TycoonPreferences.PlayTutorial, OnOffLabels);
            tutorial.OnChanged += (_, value) => TycoonPreferences.Set(TycoonPreferences.PLAY_TUTORIAL, value);
            AddSelect(tutorial);

            New<SpacerElement>(true);

            AddInfo("Tycoon guide and shopkeeper tutorial pop-ups");
            var guides = new Option<bool>(OnOff, TycoonPreferences.ShowGuides, OnOffLabels);
            guides.OnChanged += (_, value) => TycoonPreferences.Set(TycoonPreferences.SHOW_GUIDES, value);
            AddSelect(guides);

            New<SpacerElement>(true);

            AddInfo("Reputation Report after each day");
            var report = new Option<bool>(OnOff, TycoonPreferences.ShowReputationReport, OnOffLabels);
            report.OnChanged += (_, value) => TycoonPreferences.Set(TycoonPreferences.SHOW_REPUTATION_REPORT, value);
            AddSelect(report);

            New<SpacerElement>(true);

            New<SpacerElement>(true);
            if (TycoonPreferences.DeveloperMode) AddButton("Debug tools", _ => RequestSubMenu(typeof(TycoonDebugMenu)), 0, 1f, 0.2f);
            AddButton("Back", _ => RequestPreviousMenu(), 0, 1f, 0.2f);
        }

        public static void Register()
        {
            MainMenuPreferencesesMenu.RegisterMenu("Tycoon Mode", typeof(TycoonPreferencesMenu));
            PauseMenuPreferencesesMenu.RegisterMenu("Tycoon Mode", typeof(TycoonPreferencesMenu));
        }
    }
}
