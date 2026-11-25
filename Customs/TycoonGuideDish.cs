using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>The Tycoon guide's pages, the bank tutorial's recipe-popup trick: not real dishes, one "dish" per page.</summary>
    public abstract class TycoonGuideDish : CustomDish
    {
        protected abstract int Section { get; }
        protected abstract int Page { get; }

        public override string UniqueNameID => $"TycoonGuide{Section}_{Page}";

#pragma warning disable CS0618
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(
                TycoonGuide.PageCount(Section) > 1 ? $"{TycoonGuide.Title(Section)} ({Page + 1}/{TycoonGuide.PageCount(Section)})" : TycoonGuide.Title(Section),
                "Tycoon Mode guide.", "")),
        };
#pragma warning restore CS0618

        public override Dictionary<Locale, string> Recipe => new Dictionary<Locale, string>
        {
            { Locale.English, TycoonGuide.Page(Section, Page) },
        };

        public override DishType Type => DishType.Extra;
        public override bool IsUnlockable => false;
        public override bool IsAvailableAsLobbyOption => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Dish;
        public override CardType CardType => CardType.Default;
    }

    public class TycoonGuidePage0_0 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 0; }
    public class TycoonGuidePage0_1 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 1; }
    public class TycoonGuidePage0_2 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 2; }
    public class TycoonGuidePage0_3 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 3; }
    public class TycoonGuidePage0_4 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 4; }
    public class TycoonGuidePage1_0 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 0; }
    public class TycoonGuidePage1_1 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 1; }
    public class TycoonGuidePage1_2 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 2; }
    public class TycoonGuidePage1_3 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 3; }
    public class TycoonGuidePage1_4 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 4; }
    public class TycoonGuidePage2_0 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 0; }
    public class TycoonGuidePage2_1 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 1; }
    public class TycoonGuidePage2_2 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 2; }
    public class TycoonGuidePage2_3 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 3; }
    public class TycoonGuidePage2_4 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 4; }
    public class TycoonGuidePage3_0 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 0; }
    public class TycoonGuidePage3_1 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 1; }
    public class TycoonGuidePage3_2 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 2; }
    public class TycoonGuidePage3_3 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 3; }
    public class TycoonGuidePage3_4 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 4; }
    public class TycoonGuidePage4_0 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 0; }
    public class TycoonGuidePage4_1 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 1; }
    public class TycoonGuidePage4_2 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 2; }
    public class TycoonGuidePage4_3 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 3; }
    public class TycoonGuidePage4_4 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 4; }
    public class TycoonGuidePage5_0 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 0; }
    public class TycoonGuidePage5_1 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 1; }
    public class TycoonGuidePage5_2 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 2; }
    public class TycoonGuidePage5_3 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 3; }
    public class TycoonGuidePage5_4 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 4; }
    public class TycoonGuidePage6_0 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 0; }
    public class TycoonGuidePage6_1 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 1; }
    public class TycoonGuidePage6_2 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 2; }
    public class TycoonGuidePage6_3 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 3; }
    public class TycoonGuidePage6_4 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 4; }
    public class TycoonGuidePage0_5 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 5; }
    public class TycoonGuidePage0_6 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 6; }
    public class TycoonGuidePage0_7 : TycoonGuideDish { protected override int Section => 0; protected override int Page => 7; }
    public class TycoonGuidePage1_5 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 5; }
    public class TycoonGuidePage1_6 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 6; }
    public class TycoonGuidePage1_7 : TycoonGuideDish { protected override int Section => 1; protected override int Page => 7; }
    public class TycoonGuidePage2_5 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 5; }
    public class TycoonGuidePage2_6 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 6; }
    public class TycoonGuidePage2_7 : TycoonGuideDish { protected override int Section => 2; protected override int Page => 7; }
    public class TycoonGuidePage3_5 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 5; }
    public class TycoonGuidePage3_6 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 6; }
    public class TycoonGuidePage3_7 : TycoonGuideDish { protected override int Section => 3; protected override int Page => 7; }
    public class TycoonGuidePage4_5 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 5; }
    public class TycoonGuidePage4_6 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 6; }
    public class TycoonGuidePage4_7 : TycoonGuideDish { protected override int Section => 4; protected override int Page => 7; }
    public class TycoonGuidePage5_5 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 5; }
    public class TycoonGuidePage5_6 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 6; }
    public class TycoonGuidePage5_7 : TycoonGuideDish { protected override int Section => 5; protected override int Page => 7; }
    public class TycoonGuidePage6_5 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 5; }
    public class TycoonGuidePage6_6 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 6; }
    public class TycoonGuidePage6_7 : TycoonGuideDish { protected override int Section => 6; protected override int Page => 7; }
}
