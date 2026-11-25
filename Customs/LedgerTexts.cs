using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>Row labels for the end-of-day page.</summary>
    public abstract class LedgerText : CustomMoneyTrackerText
    {
        protected abstract string Label { get; }

        public override List<(Locale, BasicInfo)> InfoList => new List<(Locale, BasicInfo)>
        {
            (Locale.English, LocalisationUtils.CreateBasicInfo(Label, "")),
        };
    }

    public class LedgerRent : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerRent";
        protected override string Label => "Rent";
    }

    public class LedgerUtilities : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerUtilities";
        protected override string Label => "Utilities";
    }

    public class LedgerUpkeep : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerUpkeep";
        protected override string Label => "Equipment upkeep";
    }

    public class LedgerRestocking : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerRestocking";
        protected override string Label => "Restocking";
    }

    public class LedgerLicences : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerLicences";
        protected override string Label => "Licences & insurance";
    }

    /// <summary>End-of-day row: staff wages, one paycheck for every player after the first.</summary>
    public class LedgerWages : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerWages";
        protected override string Label => "Staff wages";
    }

    public class LedgerPurchases : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerPurchases";
        protected override string Label => "Purchasing Desk orders";
    }

    public class LedgerDirtyFloors : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerDirtyFloors";
        protected override string Label => "Cleaning fee: floors left dirty";
    }

    public class LedgerTrashLeft : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerTrashLeft";
        protected override string Label => "Cleaning fee: trash not taken out";
    }

    /// <summary>End-of-day row: income the game does not itemise.</summary>
    public class LedgerTips : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerTips";
        protected override string Label => "Tips & other income";
    }

    /// <summary>Section heading row.</summary>
    public class LedgerCostsHeading : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerCostsHeading";
        protected override string Label => "Daily costs";
    }

    /// <summary>Not a row: carries the cash left after the bill to every player's screen.</summary>
    public class LedgerCashMarker : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerCash";
        protected override string Label => "Cash in the till";
    }

    /// <summary>Not a row: carries the number of nights in a row the restaurant closed in debt.</summary>
    public class LedgerDebtMarker : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerDebt";
        protected override string Label => "Nights in debt";
    }

    /// <summary>Not a row: reputation gained today.</summary>
    public class LedgerReputationGainedMarker : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerReputationGained";
        protected override string Label => "Reputation today";
    }

    /// <summary>Not a row: reputation total after today.</summary>
    public class LedgerReputationTotalMarker : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerReputationTotal";
        protected override string Label => "Reputation";
    }

    /// <summary>Not a row: +1 for a star gained tonight, -1 for a star lost.</summary>
    public class LedgerStarChangeMarker : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerStarChange";
        protected override string Label => "Star change";
    }

    public class LedgerRepServing : LedgerText
    {
        public override string UniqueNameID => "TycoonRepServing";
        protected override string Label => "Serving customers";
    }
    public class LedgerRepFullHouse : LedgerText
    {
        public override string UniqueNameID => "TycoonRepFullHouse";
        protected override string Label => "Full House bonus";
    }
    public class LedgerRepDecor : LedgerText
    {
        public override string UniqueNameID => "TycoonRepDecor";
        protected override string Label => "Ambience";
    }
    public class LedgerRepMarketing : LedgerText
    {
        public override string UniqueNameID => "TycoonRepMarketing";
        protected override string Label => "Marketing campaigns";
    }
    public class LedgerRepContract : LedgerText
    {
        public override string UniqueNameID => "TycoonRepContract";
        protected override string Label => "Contracts";
    }
    public class LedgerRepInspection : LedgerText
    {
        public override string UniqueNameID => "TycoonRepInspection";
        protected override string Label => "Health inspection";
    }
    public class LedgerRepSlow : LedgerText
    {
        public override string UniqueNameID => "TycoonRepSlow";
        protected override string Label => "Slow or messy serves";
    }
    public class LedgerRepWalkouts : LedgerText
    {
        public override string UniqueNameID => "TycoonRepWalkouts";
        protected override string Label => "Walkouts";
    }
    public class LedgerRepFastCasual : LedgerText
    {
        public override string UniqueNameID => "TycoonRepFastCasual";
        protected override string Label => "Fast Casual: no table found";
    }
    public class LedgerRepDecay : LedgerText
    {
        public override string UniqueNameID => "TycoonRepDecay";
        protected override string Label => "Nightly decay";
    }
    /// <summary>The ingredient quality's change to the day's serving reputation.</summary>
    public class LedgerRepIngredients : LedgerText
    {
        public override string UniqueNameID => "TycoonRepIngredients";
        protected override string Label => "Ingredients";
    }

    public class LedgerRepOther : LedgerText
    {
        public override string UniqueNameID => "TycoonRepOther";
        protected override string Label => "Other";
    }
}
