using KitchenLib;
using KitchenMods;
using System.Reflection;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Entry point for the Tycoon Mode overhaul.</summary>
    public class Mod : BaseMod, IModSystem
    {
        public const string MOD_GUID = "com.lucas.plateuptycoon";
        public const string MOD_NAME = "Tycoon Mode";
        public const string MOD_VERSION = "0.3.0";
        public const string BUILD = "2026-11-25 ht (first public release, 0.3.0)";
        public const string MOD_AUTHOR = "supedaglup";
        public const string MOD_GAMEVERSION = ">=1.1.9";

        public const int DEFAULT_DAILY_RENT = 25;
        public const int STARTING_MONEY = 150;
        public const int STARTING_LAYOUT_PROFILE = 222370461;
        public const bool USE_STARTER_DESIGN = true;
        public const int STARTING_DISH = 16318784;
        /// <summary>End-of-day page row labels, set when the mod registers them.</summary>
        public static class Ledger
        {
            public static int Rent, Utilities, Upkeep, Restocking, Licences, DirtyFloors, TrashLeft, Purchases, Tips, CostsHeading, Cash, Debt, Loan, InspectionFine, Wages;
            public static int ReputationGained, ReputationTotal, StarChange;
            public static int RepServing, RepFullHouse, RepDecor, RepMarketing, RepContract, RepInspection, RepSlow, RepWalkouts, RepFastCasual, RepDecay, RepOther, RepIngredients;

            public static bool IsMarker(int id) =>
                id != 0 && (id == Cash || id == Debt || id == ReputationGained || id == ReputationTotal || id == StarChange);

            public static bool IsCostRow(int id) =>
                id != 0 && (id == Rent || id == Utilities || id == Upkeep || id == Restocking || id == Licences
                    || id == DirtyFloors || id == TrashLeft || id == Purchases || id == Loan || id == InspectionFine || id == Wages);

            public static bool IsTycoonId(int id) =>
                id != 0 && (IsCostRow(id) || id == Tips || id == CostsHeading || IsMarker(id));
        }

        public static Customs.PurchasingDesk PurchasingDesk { get; private set; }
        public static Customs.ManagementDesk ManagementDesk { get; private set; }
        public static Customs.OrderingCounter OrderingCounter { get; private set; }
        public static Customs.TakeOutPlates TakeOutPlates { get; private set; }
        public static Customs.BankDesk BankDesk { get; private set; }
        public static Customs.ChefDesk ChefDesk { get; private set; }
        public static Customs.CardDesk CardDesk { get; private set; }
        public static Customs.DiscountDesk DiscountDesk { get; private set; }
        public static readonly System.Collections.Generic.List<int> DiscountTutorialPages = new System.Collections.Generic.List<int>();
        public static Customs.CardItem CardItem { get; private set; }
        public static readonly System.Collections.Generic.List<int> CardTutorialPages = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<int> ReportPages = new System.Collections.Generic.List<int>();

        public static readonly int[,] GuidePages = new int[TycoonGuide.SECTIONS, TycoonGuide.MAX_PAGES];
        public static readonly System.Collections.Generic.HashSet<int> GuidePageIDs = new System.Collections.Generic.HashSet<int>();

        public static bool IsTutorialPage(int id) =>
            BankTutorialPages.Contains(id) || ChefTutorialPages.Contains(id) || CardTutorialPages.Contains(id) || GuidePageIDs.Contains(id)
            || DiscountTutorialPages.Contains(id) || ReportPages.Contains(id);

        public static readonly System.Collections.Generic.List<int> BankTutorialPages = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<int> ChefTutorialPages = new System.Collections.Generic.List<int>();
        public static Customs.TakeOutCard TakeOutCard { get; private set; }
        public static Customs.FastCasualCard FastCasualCard { get; private set; }
        public static Customs.UpgradedAppliancesCard UpgradedAppliancesCard { get; private set; }
        public static Customs.DishPricingCard DishPricingCard { get; private set; }
        public static Customs.BusinessHoursCard BusinessHoursCard { get; private set; }
        public static Customs.FiveStarPricesCard FiveStarPricesCard { get; private set; }
        public static Customs.IngredientQualityCard IngredientQualityCard { get; private set; }

        public static readonly int[] MarketingCardIDs = new int[TycoonPromotions.MARKETING_SLOTS];
        public static readonly int[] ContractCardIDs = new int[TycoonPromotions.CONTRACT_SLOTS];
        public static readonly int[] CustomerCardIDs = new int[TycoonCustomerCards.SLOTS];

        public static bool IsRewardCard(int id) =>
            (UpgradedAppliancesCard != null && id == UpgradedAppliancesCard.ID) || (BusinessHoursCard != null && id == BusinessHoursCard.ID)
            || (FiveStarPricesCard != null && id == FiveStarPricesCard.ID) || (DishPricingCard != null && id == DishPricingCard.ID);
        public static int CoffeeShopCard => KitchenLib.References.UnlockReferences.CoffeeshopMode;

        public static readonly System.Collections.Generic.Dictionary<string, int> CustomApplianceNames =
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        public static int[] StartingKit => new[]
        {
            OrderingCounter.ID,
            KitchenLib.References.ApplianceReferences.CoffeeMachine,
            PurchasingDesk.ID,
            ManagementDesk.ID,
        };

        public static bool IsTycoonDesk(int applianceID) =>
            (PurchasingDesk != null && applianceID == PurchasingDesk.ID) || (ManagementDesk != null && applianceID == ManagementDesk.ID)
            || (BankDesk != null && applianceID == BankDesk.ID) || (ChefDesk != null && applianceID == ChefDesk.ID)
            || (CardDesk != null && applianceID == CardDesk.ID) || (DiscountDesk != null && applianceID == DiscountDesk.ID);

        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, MOD_GAMEVERSION, Assembly.GetExecutingAssembly()) { }

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            TycoonLog.Start();
            PurchasingDesk = AddGameDataObject<Customs.PurchasingDesk>();
            ManagementDesk = AddGameDataObject<Customs.ManagementDesk>();
            OrderingCounter = AddGameDataObject<Customs.OrderingCounter>();
            TakeOutPlates = AddGameDataObject<Customs.TakeOutPlates>();
            BankDesk = AddGameDataObject<Customs.BankDesk>();
            ChefDesk = AddGameDataObject<Customs.ChefDesk>();
            CardDesk = AddGameDataObject<Customs.CardDesk>();
            DiscountDesk = AddGameDataObject<Customs.DiscountDesk>();
            CardItem = AddGameDataObject<Customs.CardItem>();
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage1>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage2>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage3>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage4>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage5>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage6>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage7>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage8>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage9>().ID);
            BankTutorialPages.Add(AddGameDataObject<Customs.BankTutorialPage10>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage1>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage2>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage3>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage4>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage5>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage6>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage7>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage8>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage9>().ID);
            ChefTutorialPages.Add(AddGameDataObject<Customs.ChefTutorialPage10>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage1>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage2>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage3>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage4>().ID);
            ReportPages.Add(AddGameDataObject<Customs.ReputationReportPage1>().ID);
            ReportPages.Add(AddGameDataObject<Customs.ReputationReportPage2>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage1>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage2>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage3>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage4>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage5>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage6>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage7>().ID);
            DiscountTutorialPages.Add(AddGameDataObject<Customs.DiscountTutorialPage8>().ID);
            for (int s = 0; s < TycoonGuide.SECTIONS; s++)
                for (int p = 0; p < TycoonGuide.MAX_PAGES; p++)
                {
                    int id = ((KitchenLib.Customs.CustomGameDataObject)AddGameDataObjectType(typeof(Mod).Assembly.GetType($"PlateUpTycoon.Customs.TycoonGuidePage{s}_{p}"))).ID;
                    GuidePages[s, p] = id;
                    GuidePageIDs.Add(id);
                }
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage5>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage6>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage7>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage8>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage9>().ID);
            CardTutorialPages.Add(AddGameDataObject<Customs.CardTutorialPage10>().ID);
            TakeOutCard = AddGameDataObject<Customs.TakeOutCard>();
            FastCasualCard = AddGameDataObject<Customs.FastCasualCard>();
            UpgradedAppliancesCard = AddGameDataObject<Customs.UpgradedAppliancesCard>();
            DishPricingCard = AddGameDataObject<Customs.DishPricingCard>();
            BusinessHoursCard = AddGameDataObject<Customs.BusinessHoursCard>();
            FiveStarPricesCard = AddGameDataObject<Customs.FiveStarPricesCard>();
            IngredientQualityCard = AddGameDataObject<Customs.IngredientQualityCard>();
            for (int i = 0; i < TycoonPromotions.MARKETING_SLOTS; i++)
                MarketingCardIDs[i] = ((KitchenLib.Customs.CustomGameDataObject)AddGameDataObjectType(typeof(Mod).Assembly.GetType($"PlateUpTycoon.Customs.MarketingCard{i:00}"))).ID;
            for (int i = 0; i < TycoonCustomerCards.SLOTS; i++)
                CustomerCardIDs[i] = ((KitchenLib.Customs.CustomGameDataObject)AddGameDataObjectType(typeof(Mod).Assembly.GetType($"PlateUpTycoon.Customs.CustomerCard{i:00}"))).ID;
            for (int i = 0; i < TycoonPromotions.CONTRACT_SLOTS; i++)
                ContractCardIDs[i] = ((KitchenLib.Customs.CustomGameDataObject)AddGameDataObjectType(typeof(Mod).Assembly.GetType($"PlateUpTycoon.Customs.ContractCard{i:00}"))).ID;
            CustomApplianceNames["OrderingCounter"] = OrderingCounter.ID;
            CustomApplianceNames["TakeOutPlates"] = TakeOutPlates.ID;
            CustomApplianceNames["PurchasingDesk"] = PurchasingDesk.ID;
            CustomApplianceNames["ManagementDesk"] = ManagementDesk.ID;
            Ledger.Rent = AddGameDataObject<Customs.LedgerRent>().ID;
            Ledger.Utilities = AddGameDataObject<Customs.LedgerUtilities>().ID;
            Ledger.Upkeep = AddGameDataObject<Customs.LedgerUpkeep>().ID;
            Ledger.Restocking = AddGameDataObject<Customs.LedgerRestocking>().ID;
            Ledger.Licences = AddGameDataObject<Customs.LedgerLicences>().ID;
            Ledger.DirtyFloors = AddGameDataObject<Customs.LedgerDirtyFloors>().ID;
            Ledger.TrashLeft = AddGameDataObject<Customs.LedgerTrashLeft>().ID;
            Ledger.Purchases = AddGameDataObject<Customs.LedgerPurchases>().ID;
            Ledger.Loan = AddGameDataObject<Customs.LedgerLoan>().ID;
            Ledger.InspectionFine = AddGameDataObject<Customs.LedgerInspectionFine>().ID;
            Ledger.Wages = AddGameDataObject<Customs.LedgerWages>().ID;
            Ledger.Tips = AddGameDataObject<Customs.LedgerTips>().ID;
            Ledger.CostsHeading = AddGameDataObject<Customs.LedgerCostsHeading>().ID;
            Ledger.Cash = AddGameDataObject<Customs.LedgerCashMarker>().ID;
            Ledger.Debt = AddGameDataObject<Customs.LedgerDebtMarker>().ID;
            Ledger.ReputationGained = AddGameDataObject<Customs.LedgerReputationGainedMarker>().ID;
            Ledger.ReputationTotal = AddGameDataObject<Customs.LedgerReputationTotalMarker>().ID;
            Ledger.StarChange = AddGameDataObject<Customs.LedgerStarChangeMarker>().ID;
            Ledger.RepServing = AddGameDataObject<Customs.LedgerRepServing>().ID;
            Ledger.RepFullHouse = AddGameDataObject<Customs.LedgerRepFullHouse>().ID;
            Ledger.RepDecor = AddGameDataObject<Customs.LedgerRepDecor>().ID;
            Ledger.RepMarketing = AddGameDataObject<Customs.LedgerRepMarketing>().ID;
            Ledger.RepContract = AddGameDataObject<Customs.LedgerRepContract>().ID;
            Ledger.RepInspection = AddGameDataObject<Customs.LedgerRepInspection>().ID;
            Ledger.RepSlow = AddGameDataObject<Customs.LedgerRepSlow>().ID;
            Ledger.RepWalkouts = AddGameDataObject<Customs.LedgerRepWalkouts>().ID;
            Ledger.RepFastCasual = AddGameDataObject<Customs.LedgerRepFastCasual>().ID;
            Ledger.RepDecay = AddGameDataObject<Customs.LedgerRepDecay>().ID;
            Ledger.RepOther = AddGameDataObject<Customs.LedgerRepOther>().ID;
            Ledger.RepIngredients = AddGameDataObject<Customs.LedgerRepIngredients>().ID;
            TycoonPreferences.Init();
            UI.TycoonMainMenu.Register();
            UI.TycoonPreferencesMenu.Register();
            UI.TycoonDebugMenu.Register();
        }

        protected override void OnInitialise()
        {
            LogWarning($"{MOD_NAME} v{MOD_VERSION} loaded. Build: {BUILD}");
        }

        private static Kitchen.SceneType _lastScene = Kitchen.SceneType.Null;

        private static void NewPicksOnHqArrival()
        {
            Kitchen.SceneType scene = Kitchen.GameInfo.CurrentScene;
            if (scene == _lastScene) return;
            _lastScene = scene;
            if (scene != Kitchen.SceneType.Franchise || !TycoonSession.IsActive) return;
            TycoonCompany.EnsureLoaded();
            TycoonCompany.ShopReroll++;
            TycoonCompany.Save();
            LogInfo($"HQ: arrived - new picks in every shop (reroll {TycoonCompany.ShopReroll}).");
        }

        protected override void OnFrameUpdate()
        {
            TycoonLog.Tick();
            NewPicksOnHqArrival();
            Patches.TycoonReputationBar.Animate(UnityEngine.Time.unscaledDeltaTime);
            Patches.TycoonDialogueView.FollowAll();
            Patches.DeskMenuItemsPatch.RefreshGuestMenus();
            UI.TycoonCardPreview.Update();
            UI.TycoonHqReputationLabel.Update();
        }

        #region Logging
        public static void LogInfo(string msg) { TycoonLog.Write('I', msg); if (TycoonPreferences.DeveloperMode) Debug.Log($"[{MOD_NAME}] {msg}"); }
        public static void LogWarning(string msg) { TycoonLog.Write('W', msg); Debug.LogWarning($"[{MOD_NAME}] {msg}"); }
        public static void LogError(string msg) { TycoonLog.Write('E', msg); Debug.LogError($"[{MOD_NAME}] {msg}"); }
        #endregion
    }
}
