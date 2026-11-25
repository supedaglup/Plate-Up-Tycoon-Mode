using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.Customs
{
    /// <summary>1-star reward: the Purchasing Desk sells upgraded appliances.</summary>
    public class UpgradedAppliancesCard : CustomUnlockCard
    {
        public const string NAME = "Upgraded Appliances";
        public override string UniqueNameID => "TycoonRewardUpgradedAppliances";
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(NAME, "1-star reward: the Purchasing Desk now sells the upgraded versions of appliances.", "Bigger, faster, shinier.")),
        };
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }

    /// <summary>1-star reward, always on: set the menu's prices at the Management Desk.</summary>
    public class DishPricingCard : CustomUnlockCard
    {
        public const string NAME = "Dish Pricing";
        public override string UniqueNameID => "TycoonRewardDishPricing";
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(NAME, "1-star reward: set your dish prices at the Management Desk. Dearer dishes earn more coins but less reputation; cheaper dishes earn more reputation but fewer coins.", "Name your price.")),
        };
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }

    public class BusinessHoursCard : CustomUnlockCard
    {
        public const string NAME = "Business Hours";
        public override string UniqueNameID => "TycoonRewardBusinessHours";
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(NAME, "3-star reward: add an Opening and a Closing time to your day (0-15 seconds each), set at the Management Desk.", "Your restaurant, your hours.")),
        };
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }

    /// <summary>2-star reward: ingredient quality.</summary>
    public class IngredientQualityCard : CustomUnlockCard
    {
        public const string NAME = "Ingredient Quality";
        public override string UniqueNameID => "TycoonRewardIngredientQuality";
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(NAME, "2-star reward: choose your ingredients at the Management Desk, from Budget to Gourmet. Better ingredients cost more to restock but earn more reputation, tips and Recipe Mastery. Switched off: Budget ingredients.", "You get what you pay for.")),
        };
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }

    /// <summary>5-star reward: premium prices.</summary>
    public class FiveStarPricesCard : CustomUnlockCard
    {
        public const string NAME = "Five-Star Menu Prices";
        public override string UniqueNameID => "TycoonRewardFiveStarPrices";
        public override List<(Locale, UnlockInfo)> InfoList => new List<(Locale, UnlockInfo)>
        {
            (Locale.English, LocalisationUtils.CreateUnlockInfo(NAME, $"5-star reward: every dish costs {TycoonUnlocks.FIVE_STAR_PRICE_BONUS}% more, but customers in the queue are {Mathf.RoundToInt((1f - TycoonUnlocks.FIVE_STAR_PATIENCE) * 100f)}% less patient. Lost with the fifth star.", "You're paying for the name.")),
        };
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }
}
