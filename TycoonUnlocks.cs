namespace PlateUpTycoon
{
    /// <summary>What the Purchasing Desk sells at the restaurant's star level.</summary>
    public static class TycoonUnlocks
    {
        public const int UPGRADES_STAR = 1, DECOR_STAR = 2;
        public const int PRICING_STAR = 1;
        public const int QUALITY_STAR = 2;
        public const int HOURS_STAR = 3, THEMES_STAR = 3;
        public static bool HoursUnlocked => Stars >= HOURS_STAR;
        public const int FIVE_STAR = 5, FIVE_STAR_PRICE_BONUS = 25;
        public const float FIVE_STAR_PATIENCE = 0.85f;
        public static bool FiveStarPricesOn => TycoonEconomy.CurrentStars >= FIVE_STAR;

        public static int Stars => TycoonNetData.IsHost ? TycoonEconomy.CurrentStars : Patches.TycoonDayBar.Stars;

        public static bool UpgradesForSale => Stars >= UPGRADES_STAR;
        public static bool DecorForSale => Stars >= DECOR_STAR;
    }
}
