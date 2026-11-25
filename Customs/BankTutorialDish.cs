using HarmonyLib;
using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>Not real dishes: pages for the game's recipe popup, which teaches the bank.</summary>
    public abstract class BankTutorialDish : CustomDish
    {
        public const string TITLE = "The Bank";
        public const int MAX_PAGES = 10;

        protected abstract int Page { get; }

        public override string UniqueNameID => "TycoonBankTutorial" + (Page == 0 ? "" : (Page + 1).ToString());

        public static int PageCount => System.Math.Min(MAX_PAGES, System.Math.Max(1, TycoonBankerDialogue.All("tutorial").Count));

        public static string PageText(int page)
        {
            List<string> lines = TycoonBankerDialogue.All("tutorial");
            if (page >= lines.Count) return "";
            if (page < MAX_PAGES - 1 || lines.Count <= MAX_PAGES) return lines[page];
            return string.Join("\n", lines.GetRange(page, lines.Count - page));
        }

        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(PageCount > 1 ? $"{TITLE} ({Page + 1}/{PageCount})" : TITLE, "How loans and the company wallet work.", "")),
        };

        public override Dictionary<Locale, string> Recipe => new Dictionary<Locale, string>
        {
            { Locale.English, PageText(Page) },
        };

        public override DishType Type => DishType.Extra;
        public override bool IsUnlockable => false;
        public override bool IsAvailableAsLobbyOption => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Dish;
        public override CardType CardType => CardType.Default;
    }

    public class BankTutorialPage1 : BankTutorialDish { protected override int Page => 0; }
    public class BankTutorialPage2 : BankTutorialDish { protected override int Page => 1; }
    public class BankTutorialPage3 : BankTutorialDish { protected override int Page => 2; }
    public class BankTutorialPage4 : BankTutorialDish { protected override int Page => 3; }
    public class BankTutorialPage5 : BankTutorialDish { protected override int Page => 4; }
    public class BankTutorialPage6 : BankTutorialDish { protected override int Page => 5; }
    public class BankTutorialPage7 : BankTutorialDish { protected override int Page => 6; }
    public class BankTutorialPage8 : BankTutorialDish { protected override int Page => 7; }
    public class BankTutorialPage9 : BankTutorialDish { protected override int Page => 8; }
    public class BankTutorialPage10 : BankTutorialDish { protected override int Page => 9; }

    /// <summary>The recipe popup ends its text with the game's "Tip: recipes can be viewed any time in the pause menu" line.</summary>
    [HarmonyPatch(typeof(Kitchen.Modules.TextPanelElement), "SetText")]
    public static class BankTutorialNoRecipeTipPatch
    {
        [HarmonyPrefix]
        private static void Prefix(string title, ref string text)
        {
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(text)) return;
            if (title.StartsWith(TycoonReputationReport.TITLE))
            {
                string page = TycoonReputationReport.PageText(title.EndsWith("gains") ? 0 : 1);
                text = string.IsNullOrEmpty(page) ? "No report yet." : page;
                return;
            }
            if (!title.StartsWith(BankTutorialDish.TITLE) && !title.StartsWith(ChefTutorialDish.TITLE) && !title.StartsWith(CardTutorialDish.TITLE) && !TycoonGuide.IsGuideTitle(title) && !title.StartsWith(DiscountTutorialDish.TITLE)) return;
            int tip = text.LastIndexOf("\n\n", System.StringComparison.Ordinal);
            if (tip >= 0) text = text.Substring(0, tip);
        }
    }
}
