using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Tycoon economy rules: the daily bill and how the restaurant's star rating sets the difficulty.</summary>
    public static class TycoonEconomy
    {
        public const float RENT_PER_TILE = 0.06f;
        public const float UTILITIES_PER_APPLIANCE = 0.5f;
        public const float UPKEEP_RATE = 0.01f;
        public const float RESTOCK_RATE = 0.15f;
        public const float SPOILAGE_PER_INGREDIENT = 1f;
        public const float LICENCES = 3f;
        public const float DISPOSABLE_PLATE_COST = 1f;

        public const float CLEANING_PER_MESS = 1f;
        public const float CLEANING_PER_TRASH = 0.5f;
        public const float CLEANING_MESS_PER_REPUTATION = 0.002f, CLEANING_TRASH_PER_REPUTATION = 0.0003f;

        public const float TAKEOUT_EATING_SECONDS = 0.5f;
        public const float TAKEOUT_THINKING_SECONDS = 0.5f;
        public const float TAKEOUT_PRICE_FACTOR = 0.7f;
        public const float DISH_PRICE_MULTIPLIER = 1.5f;
        public const float TAKEOUT_EXTRA_CUSTOMERS = 0.5f;

        public const int PAY_PER_STAR = 1;

        public static readonly float[] LOW_STAR_EXTRA_CUSTOMERS = { 0f, 0f, 0f, 0f, 0f, 0f };

        public const float COFFEE_SHOP_EXTRA_BASE = 5f, COFFEE_SHOP_EXTRA_GROWTH = 1.15f, COFFEE_SHOP_MULTIPLAYER_FACTOR = 1.15f;

        public const float CROWD_SOLO = 0.9f, CROWD_MULTIPLAYER = 0.85f;
        public static float CrowdFactor(int players) => players > 1 ? CROWD_MULTIPLAYER : CROWD_SOLO;

        public const int FAST_CASUAL_STAR = 3;
        public const float FAST_CASUAL_SHARE = 0.5f;
        public const float FAST_CASUAL_COUNTER_HOLD = 9999f;
        public const float FAST_CASUAL_DEFAULT_EATING = 10f;
        public const float FAST_CASUAL_MESS_CHANCE = 0.25f;
        public const float FAST_CASUAL_FRIEND_WAIT = 40f;

        public const int MIN_STARS = 0;
        public const int MAX_STARS = 5;
        public const float COST_INCREASE_PER_STAR = 0.25f;
        public const int DAYS_PER_STAR = 3;

        public const int BANKRUPT_AFTER_NIGHTS_IN_DEBT = 2;

        public static int CurrentStars { get; internal set; } = MIN_STARS;

        public static int ClampStars(int stars) => Mathf.Clamp(stars, MIN_STARS, MAX_STARS);

        public static float CostMultiplier(int stars) => 1f + COST_INCREASE_PER_STAR * (ClampStars(stars) - MIN_STARS);

        public static int EffectiveDay(int stars) => 1 + DAYS_PER_STAR * (ClampStars(stars) - MIN_STARS);

        public struct Bill
        {
            public int Rent;
            public int Utilities;
            public int Upkeep;
            public int Restocking;
            public int Licences;
            public int DirtyFloors;
            public int TrashLeft;
            public int Wages;

            public int Total => Rent + Utilities + Upkeep + Restocking + Licences + DirtyFloors + TrashLeft + Wages;
        }

        public const float WAGE_BASE = 1f, WAGE_SHARE = 0.35f, WAGE_SHARE_PER_STAR = 0.03f;

        public static int Wages(int stars, int players, int takings)
        {
            if (players <= 1) return 0;
            int s = ClampStars(stars);
            float each = WAGE_BASE * (1 + s) + (WAGE_SHARE + WAGE_SHARE_PER_STAR * s) * Mathf.Max(0, takings) / players;
            return Mathf.CeilToInt(each * (players - 1));
        }

        public static Bill CalculateBill(int stars, int interiorTiles, int appliances, int applianceValue, int ingredientSources, int takings, int messes, int trash, int players = 1, float reputation = 0f, int disposablePlates = 0)
        {
            float m = CostMultiplier(stars);
            float rep = Mathf.Max(0f, reputation);
            return new Bill
            {
                Rent = 0,
                Utilities = Mathf.CeilToInt(appliances * UTILITIES_PER_APPLIANCE * m),
                Upkeep = Mathf.CeilToInt(applianceValue * UPKEEP_RATE),
                Restocking = Mathf.CeilToInt(Mathf.Max(0, takings) * TycoonIngredientQuality.RestockRate + ingredientSources * SPOILAGE_PER_INGREDIENT
                    + TycoonIngredientQuality.DailyFee
                    + Mathf.Max(0, disposablePlates) * DISPOSABLE_PLATE_COST * m),
                Licences = Mathf.CeilToInt(LICENCES * m),
                DirtyFloors = Mathf.CeilToInt(messes * (CLEANING_PER_MESS + CLEANING_MESS_PER_REPUTATION * rep) * m),
                TrashLeft = Mathf.CeilToInt(trash * (CLEANING_PER_TRASH + CLEANING_TRASH_PER_REPUTATION * rep) * m),
                Wages = Wages(stars, players, takings),
            };
        }
    }
}
