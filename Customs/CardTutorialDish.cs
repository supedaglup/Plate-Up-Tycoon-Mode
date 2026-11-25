using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>The Card Market's tutorial, the bank's recipe-popup trick: not real dishes.</summary>
    public abstract class CardTutorialDish : CustomDish
    {
        public const string TITLE = "The Card Market";
        public const int MAX_PAGES = 10;

        protected abstract int Page { get; }

        public override string UniqueNameID => "TycoonCardTutorial" + (Page + 1);

        public static int PageCount => System.Math.Min(MAX_PAGES, System.Math.Max(1, TycoonCardDialogue.All("tutorial").Count));

        public static string PageText(int page)
        {
            List<string> lines = TycoonCardDialogue.All("tutorial");
            if (page >= lines.Count) return "";
            if (page < MAX_PAGES - 1 || lines.Count <= MAX_PAGES) return lines[page];
            return string.Join("\n", lines.GetRange(page, lines.Count - page));
        }

#pragma warning disable CS0618
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(PageCount > 1 ? $"{TITLE} ({Page + 1}/{PageCount})" : TITLE, "How the Card Market works.", "")),
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

    public class CardTutorialPage1 : CardTutorialDish { protected override int Page => 0; }
    public class CardTutorialPage2 : CardTutorialDish { protected override int Page => 1; }
    public class CardTutorialPage3 : CardTutorialDish { protected override int Page => 2; }
    public class CardTutorialPage4 : CardTutorialDish { protected override int Page => 3; }
    public class CardTutorialPage5 : CardTutorialDish { protected override int Page => 4; }
    public class CardTutorialPage6 : CardTutorialDish { protected override int Page => 5; }
    public class CardTutorialPage7 : CardTutorialDish { protected override int Page => 6; }
    public class CardTutorialPage8 : CardTutorialDish { protected override int Page => 7; }
    public class CardTutorialPage9 : CardTutorialDish { protected override int Page => 8; }
    public class CardTutorialPage10 : CardTutorialDish { protected override int Page => 9; }
}
