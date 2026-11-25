using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>The recipe shop's tutorial, the bank's recipe-popup trick: not real dishes.</summary>
    public abstract class ChefTutorialDish : CustomDish
    {
        public const string TITLE = "The Recipe Shop";
        public const int MAX_PAGES = 10;

        protected abstract int Page { get; }

        public override string UniqueNameID => "TycoonChefTutorial" + (Page + 1);

        public static int PageCount => System.Math.Min(MAX_PAGES, System.Math.Max(1, TycoonChefDialogue.All("tutorial").Count));

        public static string PageText(int page)
        {
            List<string> lines = TycoonChefDialogue.All("tutorial");
            if (page >= lines.Count) return "";
            if (page < MAX_PAGES - 1 || lines.Count <= MAX_PAGES) return lines[page];
            return string.Join("\n", lines.GetRange(page, lines.Count - page));
        }

#pragma warning disable CS0618
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(PageCount > 1 ? $"{TITLE} ({Page + 1}/{PageCount})" : TITLE, "How the recipe shop works.", "")),
        };
#pragma warning restore CS0618

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

    public class ChefTutorialPage1 : ChefTutorialDish { protected override int Page => 0; }
    public class ChefTutorialPage2 : ChefTutorialDish { protected override int Page => 1; }
    public class ChefTutorialPage3 : ChefTutorialDish { protected override int Page => 2; }
    public class ChefTutorialPage4 : ChefTutorialDish { protected override int Page => 3; }
    public class ChefTutorialPage5 : ChefTutorialDish { protected override int Page => 4; }
    public class ChefTutorialPage6 : ChefTutorialDish { protected override int Page => 5; }
    public class ChefTutorialPage7 : ChefTutorialDish { protected override int Page => 6; }
    public class ChefTutorialPage8 : ChefTutorialDish { protected override int Page => 7; }
    public class ChefTutorialPage9 : ChefTutorialDish { protected override int Page => 8; }
    public class ChefTutorialPage10 : ChefTutorialDish { protected override int Page => 9; }
}
