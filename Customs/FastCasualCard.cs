using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>"Fast Casual Dining": the Take-Out card's path card.</summary>
    public class FastCasualCard : CustomUnlockCard
    {
        public const string NAME = "Fast Casual Dining";
        public const string DESCRIPTION =
            "Needs Take-Out. Some customers order and pay at the Ordering Counter, then carry their order to a table "
            + "and eat there with their friends. With no free table they wait to the side and leave if they run out of "
            + "patience (a small reputation hit). They tip more often, and more. 15% fewer customers.";

        public override string UniqueNameID => "TycoonFastCasualCard";

        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(NAME, DESCRIPTION, "Grab a tray, find a seat.")),
        };

        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override DishCustomerChange CustomerMultiplier => DishCustomerChange.SmallDecrease;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }
}
