using Kitchen;
using Kitchen.Modules;
using KitchenLib;
using KitchenLib.UI.PlateUp.PreferenceMenus;
using KitchenLib.Utils;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>Mod Preferences: the debug tools page.</summary>
    public class TycoonDebugMenu : KLMenu
    {
        public TycoonDebugMenu(Transform container, ModuleList module_list) : base(container, module_list) { }

        public override void Setup(int player_id)
        {
            AddLabel("Tycoon Debug Tools");
            New<SpacerElement>(true);
            AddButton("Service and events", _ => RequestSubMenu(typeof(TycoonDebugServiceMenu)), 0, 1f, 0.2f);
            AddButton("Reputation and stars", _ => RequestSubMenu(typeof(TycoonDebugReputationMenu)), 0, 1f, 0.2f);
            AddButton("Coins", _ => RequestSubMenu(typeof(TycoonDebugCoinsMenu)), 0, 1f, 0.2f);
            AddButton("Tutorials and introductions", _ => RequestSubMenu(typeof(TycoonDebugTutorialsMenu)), 0, 1f, 0.2f);
            AddButton("Logs", _ => RequestSubMenu(typeof(TycoonDebugLogsMenu)), 0, 1f, 0.2f);
            AddInfo("To start a company over: title screen → Play Tycoon → pick the company → Reset Progress.");
            New<SpacerElement>(true);
            AddButton("Back", _ => RequestPreviousMenu(), 0, 1f, 0.2f);
        }

        public static void Register()
        {
            foreach (System.Type menu in new[] { typeof(TycoonDebugMenu), typeof(TycoonDebugServiceMenu), typeof(TycoonDebugReputationMenu),
                typeof(TycoonDebugCoinsMenu), typeof(TycoonDebugTutorialsMenu), typeof(TycoonDebugLogsMenu) })
            {
                MainMenuPreferencesesMenu.RegisterUsableMenu(menu);
                PauseMenuPreferencesesMenu.RegisterUsableMenu(menu);
            }
        }
    }

    /// <summary>A debug sub-menu: a title, a line saying what the last button did, its buttons, Back.</summary>
    public abstract class TycoonDebugPage : KLMenu
    {
        protected TycoonDebugPage(Transform container, ModuleList module_list) : base(container, module_list) { }

        protected const string HOST_ONLY = "Only available in a Tycoon game, for the host.";
        private string _message = "";

        protected abstract string Title { get; }
        protected abstract void Buttons();

        public override void Setup(int player_id)
        {
            AddLabel(Title);
            if (!string.IsNullOrEmpty(_message)) AddInfo(_message);
            New<SpacerElement>(true);
            Buttons();
            New<SpacerElement>(true);
            AddButton("Back", _ => RequestPreviousMenu(), 0, 1f, 0.2f);
        }

        protected void Button(string label, System.Action action) => AddButton(label, _ => action(), 0, 1f, 0.2f);

        protected void Say(string message)
        {
            _message = message;
            ModuleList.Clear();
            Setup(0);
        }

        protected void HostOnly(System.Func<string> action) => Say(TycoonSession.IsActive ? action() : HOST_ONLY);

        protected static void OpenFile(string path) => Application.OpenURL(new System.Uri(path).AbsoluteUri);
    }

    public class TycoonDebugServiceMenu : TycoonDebugPage
    {
        public TycoonDebugServiceMenu(Transform container, ModuleList module_list) : base(container, module_list) { }
        protected override string Title => "Service and events";

        protected override void Buttons()
        {
            Button($"Skip HQ and start the restaurant immediately: {(TycoonPreferences.DebugAutoStart ? "On" : "Off")}", () =>
            {
                TycoonPreferences.Set(TycoonPreferences.DEBUG_AUTO_START, !TycoonPreferences.DebugAutoStart);
                Say($"Skip HQ at start is now {(TycoonPreferences.DebugAutoStart ? "On" : "Off")}.");
            });
            Button("Restock everything sold at HQ (new random picks)", () => HostOnly(() =>
            {
                TycoonCompany.EnsureLoaded();
                TycoonCompany.ShopReroll++;
                TycoonCompany.Save();
                Systems.TycoonHqRestockSystem.Requested = true;
                return "Smokey's recipes, Chubs's cards and Mr. Meowser's sites are picked again (in the HQ: now; else on your next visit).";
            }));
            Button("End service now", () => HostOnly(() =>
            {
                EntityUtils.GetEntityManager().CreateEntity(typeof(CTycoonDebugEndDay));
                return "Ending service: customers are walking out, then night starts. Close the menu to continue.";
            }));
            Button("Send a health inspector now", () => HostOnly(() =>
            {
                Systems.TycoonHealthInspectorSystem.DebugSendNow = true;
                return "A health inspector is on the way (during service; if it is night, once the day starts).";
            }));
            Button("Go bankrupt now (deletes the restaurant!)", () => HostOnly(() =>
            {
                Systems.BankruptcySystem.BankruptNow = true;
                Mod.LogWarning("DEBUG: going bankrupt now.");
                return "Bankrupt: close this menu and the bank seizes the restaurant. This DELETES the restaurant.";
            }));
        }
    }

    public class TycoonDebugReputationMenu : TycoonDebugPage
    {
        public TycoonDebugReputationMenu(Transform container, ModuleList module_list) : base(container, module_list) { }
        protected override string Title => "Reputation and stars";

        protected override void Buttons()
        {
            foreach (float amount in new[] { 100f, 10f, -10f, -100f })
                Button($"{(amount > 0 ? "Add" : "Remove")} {Mathf.Abs(amount):0} reputation", () => ChangeReputation(amount));
            Button("Award a star at the next closing", () => ForceStar(1));
            Button("Remove a star at the next closing", () => ForceStar(-1));
            Button("Recipe Mastery: level up every recipe", () => HostOnly(() =>
            {
                Systems.TycoonRecipeMastery.DebugLevelUps++;
                return "Every recipe at this restaurant gains one Mastery level (its next add-on is unlocked as a card).";
            }));
        }

        private void ChangeReputation(float amount) => HostOnly(() =>
        {
            EntityManager em = EntityUtils.GetEntityManager();
            Entity request = em.CreateEntity(typeof(CTycoonDebugReputation));
            em.SetComponentData(request, new CTycoonDebugReputation { Amount = amount });
            return $"Reputation {amount:+0;-0} requested (shown on the top-left label once the menu is closed). "
                + "A star change still needs the food critic the next day.";
        });

        private void ForceStar(int change) => HostOnly(() =>
        {
            Systems.TycoonStarSystem.DebugStarChange = change;
            return change > 0
                ? "A star will be AWARDED at the end of the next day (or this day, if service is running), whatever the critic does."
                : "A star will be REMOVED at the end of the next day (or this day, if service is running), whatever the critic does.";
        });
    }

    public class TycoonDebugCoinsMenu : TycoonDebugPage
    {
        public TycoonDebugCoinsMenu(Transform container, ModuleList module_list) : base(container, module_list) { }
        protected override string Title => "Coins";

        protected override void Buttons()
        {
            foreach (int amount in new[] { 1000, 100, -100, -1000 })
                Button($"{(amount > 0 ? "Add" : "Remove")} {Mathf.Abs(amount)} coins", () => HostOnly(() =>
                {
                    Systems.TycoonDebugMoneySystem.Pending += amount;
                    return $"{amount:+0;-0} coins requested (the coin counter changes once the menu is closed; going below 0 counts as debt at closing).";
                }));
        }
    }

    public class TycoonDebugTutorialsMenu : TycoonDebugPage
    {
        public TycoonDebugTutorialsMenu(Transform container, ModuleList module_list) : base(container, module_list) { }
        protected override string Title => "Tutorials and introductions";

        protected override void Buttons()
        {
            Button("Show the Tycoon guide pop-ups again (HQ and every star)", () =>
            {
                TycoonCompany.EnsureLoaded();
                TycoonCompany.GuideSeen = 0;
                TycoonCompany.Save();
                Say("The HQ guide shows on the next HQ visit; each star's guide at the next night in a restaurant with that many stars.");
            });
            Button("Show Whiskers's tutorial and introduction again", () =>
            {
                TycoonCompany.EnsureLoaded();
                TycoonCompany.DiscountTutorialSeen = false;
                TycoonCompany.MetWhiskers = false;
                TycoonCompany.Save();
                Say("Whiskers's tutorial and introduction will show again on your next visit to the top of the garage.");
            });
            Button("Show the bank tutorial and Mr. Meowser's introduction again", () =>
            {
                TycoonCompany.EnsureLoaded();
                TycoonCompany.BankTutorialSeen = false;
                TycoonCompany.MetBanker = false;
                TycoonCompany.Save();
                Say("The bank tutorial and the banker's introduction will show again on your next visit to the bank office.");
            });
            Button("Show the recipe shop tutorial and Smokey's introduction again", () =>
            {
                TycoonCompany.EnsureLoaded();
                TycoonCompany.ChefTutorialSeen = false;
                TycoonCompany.MetChef = false;
                TycoonCompany.Save();
                Say("The recipe shop tutorial and Smokey's introduction will show again on your next visit to the trophy room.");
            });
            Button("Show the Card Market tutorial and Chubs's introduction again", () =>
            {
                TycoonCompany.EnsureLoaded();
                TycoonCompany.CardTutorialSeen = false;
                TycoonCompany.MetDealer = false;
                TycoonCompany.Save();
                Say("The Card Market tutorial and Chubs's introduction will show again on your next visit to the office.");
            });
        }
    }

    public class TycoonDebugLogsMenu : TycoonDebugPage
    {
        public TycoonDebugLogsMenu(Transform container, ModuleList module_list) : base(container, module_list) { }
        protected override string Title => "Logs";

        private const int SHOWN = 8;

        protected override void Buttons()
        {
            AddLabel("Purchase log (newest first)");
            IReadOnlyList<TycoonPurchaseLog.Entry> log = TycoonPurchaseLog.Entries;
            if (log.Count == 0) AddInfo("No purchases yet this session.");
            else
                for (int i = log.Count - 1, shown = 0; i >= 0 && shown < SHOWN; i--, shown++)
                    AddInfo(log[i].ToString());
            Button("Open the full purchase log", () =>
            {
                try { OpenFile(TycoonPurchaseLog.FilePath); Say("Opened " + TycoonPurchaseLog.FilePath); }
                catch (System.Exception e) { Say("Could not open the purchase log: " + e.Message); }
            });
            Button("Open the Purchasing Desk menu log", () =>
            {
                try
                {
                    _ = TycoonMenuLayout.Root;
                    string path = System.IO.Path.Combine(Application.persistentDataPath, TycoonMenuLayout.LOG_FILE);
                    OpenFile(path);
                    Say("Opened " + path);
                }
                catch (System.Exception e) { Say("Could not open the menu log: " + e.Message); }
            });
        }
    }
}
