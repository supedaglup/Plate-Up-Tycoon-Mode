using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>The two pages of the end-of-day Reputation Report, the bank tutorial's recipe-popup trick.</summary>
    public abstract class ReputationReportDish : CustomDish
    {
        protected abstract int Page { get; }
        public override string UniqueNameID => "TycoonReputationReport" + (Page + 1);
#pragma warning disable CS0618
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(Page == 0 ? TycoonReputationReport.TITLE + ": gains" : TycoonReputationReport.TITLE + ": losses and total", "How today's reputation changed.", "")),
        };
#pragma warning restore CS0618
        public override Dictionary<Locale, string> Recipe => new Dictionary<Locale, string> { { Locale.English, "(today's reputation)" } };
        public override DishType Type => DishType.Extra;
        public override bool IsUnlockable => false;
        public override bool IsAvailableAsLobbyOption => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Dish;
        public override CardType CardType => CardType.Default;
    }

    public class ReputationReportPage1 : ReputationReportDish { protected override int Page => 0; }
    public class ReputationReportPage2 : ReputationReportDish { protected override int Page => 1; }
}
