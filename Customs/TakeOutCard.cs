using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>The "Take-Out" card.</summary>
    public class TakeOutCard : CustomUnlockCard
    {
        public const string NAME = "Take-Out";
        public const string DESCRIPTION =
            "Customers queue at the Ordering Counter one by one (friends too), order, grab it and go within seconds "
            + "and pay takeaway prices (70%).";

        public override string UniqueNameID => "TycoonTakeOutCard";

        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(NAME, DESCRIPTION, "Grab a cup and go.")),
        };

        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }
}
