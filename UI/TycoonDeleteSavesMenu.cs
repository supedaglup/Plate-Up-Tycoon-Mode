using Kitchen.Modules;
using KitchenLib;
using KitchenLib.UI.PlateUp.PreferenceMenus;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>Title screen: resets a company's progress.</summary>
    public class TycoonDeleteSavesMenu : KLMenu
    {
        public TycoonDeleteSavesMenu(Transform container, ModuleList module_list) : base(container, module_list) { }

        internal static string LastResult;
        private bool _busy;

        public override void Setup(int player_id)
        {
            TycoonSaveData.Trace("Delete page: opening");
            base.Setup(player_id);
            AddLabel($"Reset Company {TycoonSaveData.Slot}'s progress?");
            AddInfo($"Wipes everything in Company {TycoonSaveData.Slot}: every restaurant, all coins and loans, everything bought, HQ progress, and the introductions and tutorials seen. It starts again as a brand-new company.\nThe other companies and your normal PlateUp! saves are not touched.\nThis cannot be undone.");
            New<SpacerElement>(true);
            AddButton("Cancel", _ => RequestPreviousMenu());
            AddButton($"Yes, reset Company {TycoonSaveData.Slot}", _ => Delete());
            TycoonSaveData.Trace("Delete page: opened");
        }

        private async void Delete()
        {
            if (_busy) return;
            _busy = true;
            TycoonSaveData.Trace("Delete page: Yes pressed");
            Mod.LogInfo($"Reset Progress: confirmed on the title screen for company {TycoonSaveData.Slot}.");
            try
            {
                LastResult = await TycoonSaveData.DeleteAll();
            }
            catch (System.Exception e)
            {
                LastResult = "Could not reset the company: " + e.Message;
                Mod.LogError("Reset Progress failed: " + e);
            }
            _busy = false;
            TycoonSaveData.Trace("Delete page: going back");
            RequestPreviousMenu();
        }

        public static void Register()
        {
            MainMenuPreferencesesMenu.RegisterUsableMenu(typeof(TycoonDeleteSavesMenu));
        }
    }
}
