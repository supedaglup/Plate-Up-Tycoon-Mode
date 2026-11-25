using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>HQ shop prices follow the company's recent earnings.</summary>
    public static class TycoonShopPrices
    {
        public const int PROFIT_DAYS = 5;
        public const float MIN = 0.35f, MAX = 1.3f, FULL_PRICE_PROFIT = 150f;
        public const float APPLIANCE_MIN = 0.75f;

        public const float RECIPE_MIN = 0.1f, RECIPE_FULL_REPUTATION = 3000f;

        public static float ReputationRate
        {
            get
            {
                string tr = TycoonNetData.Get(Systems.TycoonCompanyTrackerSystem.NET_KEY);
                if (tr == null || !int.TryParse(tr, out int hundredths)) return 1f;
                return Mathf.Clamp(RECIPE_MIN + (1f - RECIPE_MIN) * (hundredths / 100f) / RECIPE_FULL_REPUTATION, RECIPE_MIN, 1f);
            }
        }

        public static float RecipeMultiplier => Mathf.Min(Multiplier, ReputationRate);

        public static int ApplyRecipe(float fullPrice) => Mathf.Max(10, Mathf.RoundToInt(fullPrice * RecipeMultiplier / 10f) * 10);

        public static float ApplianceMultiplier
        {
            get
            {
                float m = Multiplier;
                float earnings = (m - MIN) / (1f - MIN);
                return Mathf.Clamp(APPLIANCE_MIN + (1f - APPLIANCE_MIN) * earnings, APPLIANCE_MIN, MAX);
            }
        }

        public static float AverageProfit
        {
            get
            {
                TycoonCompany.EnsureLoaded();
                List<int> p = TycoonCompany.RecentProfits;
                if (p.Count == 0) return 0f;
                float sum = 0f;
                foreach (int v in p) sum += v;
                return sum / p.Count;
            }
        }

        public static float Multiplier
        {
            get
            {
                if (!TycoonNetData.IsHost)
                    return int.TryParse(TycoonNetData.Get("pm") ?? "", out int pm) && pm > 0 ? pm / 100f : 1f;
                float m = Mathf.Clamp(MIN + (1f - MIN) * AverageProfit / FULL_PRICE_PROFIT, MIN, MAX);
                TycoonNetData.Set("pm", Mathf.RoundToInt(m * 100f).ToString());
                return m;
            }
        }

        public static int Apply(float fullPrice) => Mathf.Max(10, Mathf.RoundToInt(fullPrice * Multiplier / 10f) * 10);

        public static int OrderPrice(TycoonCatalogue.Entry entry)
        {
            if (entry.Cost <= 0 || entry.Category == TycoonCatalogue.Category.Decorations || PlateUpTycoon.Systems.TycoonVisibleDecor.IsThemeFurniture(entry.ApplianceID))
                return entry.Cost;
            return Mathf.Max(5, Mathf.RoundToInt(entry.Cost * ApplianceMultiplier / 5f) * 5);
        }

        public static void NoteProfit(int profit)
        {
            TycoonCompany.EnsureLoaded();
            TycoonCompany.RecentProfits.Add(profit);
            while (TycoonCompany.RecentProfits.Count > PROFIT_DAYS) TycoonCompany.RecentProfits.RemoveAt(0);
            Mod.LogInfo($"Shop prices: today's profit {profit}; average of the last {TycoonCompany.RecentProfits.Count} closing(s) {AverageProfit:0} -> HQ prices x{Multiplier:0.00}.");
        }
    }
}
