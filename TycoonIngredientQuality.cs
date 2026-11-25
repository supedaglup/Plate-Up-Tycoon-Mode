using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Components
{
    /// <summary>The restaurant's ingredient quality.</summary>
    public struct STycoonIngredientQuality : IComponentData, IModComponent
    {
        public int Tier;
    }
}

namespace PlateUpTycoon
{
    /// <summary>Ingredient quality, picked at the Management Desk.</summary>
    public static class TycoonIngredientQuality
    {
        public const int BUDGET = 0, STANDARD = 1, PREMIUM = 2, GOURMET = 3;
        public static readonly string[] NAMES = { "Budget", "Standard", "Premium", "Gourmet" };
        public static readonly float[] RESTOCK_RATE = { 0.15f, 0.22f, 0.30f, 0.40f };
        public static readonly int[] DAILY_FEE = { 0, 0, 15, 30 };
        public static readonly int[] REQUIRED = { BUDGET, BUDGET, BUDGET, STANDARD, PREMIUM, GOURMET };
        public const float REP_UP = 0.30f, REP_DOWN = 0.20f, TIP_UP = 0.20f, TIP_DOWN = 0.25f, MASTERY_UP = 0.15f;
        public const string NET_KEY = "iq", NET_KEY_PERCENT = "iqp", NET_KEY_TAKINGS = "iqt";

        public static int Current
        {
            get
            {
                if (TycoonNetData.IsHost) return _current;
                return int.TryParse(TycoonNetData.Get(NET_KEY), out int t) ? Mathf.Clamp(t, BUDGET, GOURMET) : BUDGET;
            }
        }
        internal static int _current;

        public static bool Available { get; internal set; }

        public static int Required(int stars) => REQUIRED[Mathf.Clamp(TycoonEconomy.ClampStars(stars), 0, REQUIRED.Length - 1)];
        public static int Steps(int stars) => Current - Required(stars);
        public static float RepShare(int stars) { int s = Steps(stars); return s >= 0 ? REP_UP * s : -REP_DOWN * -s; }
        public static float TipMultiplier(int stars) { int s = Steps(stars); return s >= 0 ? 1f + TIP_UP * s : Mathf.Max(0f, 1f - TIP_DOWN * -s); }
        public static float MasteryMultiplier(int stars) => 1f + MASTERY_UP * Mathf.Max(0, Steps(stars));
        public static float RestockRate => RESTOCK_RATE[Mathf.Clamp(Current, BUDGET, GOURMET)];
        public static int DailyFee => DAILY_FEE[Mathf.Clamp(Current, BUDGET, GOURMET)];

        public static int Encode(int tier) => -76000 - tier;
        public static bool Decode(int code, out int tier)
        {
            tier = -76000 - code;
            return code <= -76000 && code > -76000 - NAMES.Length;
        }

        public static int LastTakings
        {
            get
            {
                if (TycoonNetData.IsHost)
                    return TycoonCompany.ActiveSlot > 0 && TycoonCompany.LastTakings.TryGetValue(TycoonCompany.ActiveSlot, out int t) ? t : -1;
                return int.TryParse(TycoonNetData.Get(NET_KEY_TAKINGS), out int n) ? n : -1;
            }
        }

        public static string Describe(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 3);
            int takings = LastTakings;
            string cost;
            if (takings >= 0)
                cost = $"restocking about {Mathf.CeilToInt(takings * RESTOCK_RATE[tier]) + DAILY_FEE[tier]} coins a day at yesterday's takings";
            else
                cost = DAILY_FEE[tier] > 0 ? $"restocking grows with takings, plus {DAILY_FEE[tier]} coins a day" : "restocking grows with takings";
            return $"{NAMES[tier]} Ingredients  -  {cost}{(tier == Current ? "  (current)" : "")}";
        }
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Keeps TycoonIngredientQuality.Current in step with the saved singleton and sends it to guests.</summary>
    public class TycoonIngredientQualitySystem : RestaurantSystem, IModSystem
    {
        private int _logged = -1;

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            int picked = HasSingleton<STycoonIngredientQuality>() ? Mathf.Clamp(GetSingleton<STycoonIngredientQuality>().Tier, 0, 3) : TycoonIngredientQuality.BUDGET;
            bool available = TycoonEconomy.CurrentStars >= TycoonUnlocks.QUALITY_STAR && Mod.IngredientQualityCard != null
                && TycoonCards.IsEnabled(EntityManager, Mod.IngredientQualityCard.ID);
            TycoonIngredientQuality.Available = available;
            int tier = available ? picked : TycoonIngredientQuality.BUDGET;
            TycoonIngredientQuality._current = tier;
            TycoonNetData.Set(TycoonIngredientQuality.NET_KEY, tier.ToString());
            TycoonNetData.Set(TycoonIngredientQuality.NET_KEY_TAKINGS, TycoonIngredientQuality.LastTakings.ToString());
            if (tier != _logged)
            {
                _logged = tier;
                Mod.LogInfo($"Ingredient quality: {TycoonIngredientQuality.NAMES[tier]}{(available ? "" : $" (card off or below {TycoonUnlocks.QUALITY_STAR} stars; picked: {TycoonIngredientQuality.NAMES[picked]})")} (restocking {TycoonIngredientQuality.RESTOCK_RATE[tier]:P0} of takings + {TycoonIngredientQuality.DAILY_FEE[tier]} a day).");
            }
        }

        internal static void Pick(EntityManager em, int tier)
        {
            tier = Mathf.Clamp(tier, 0, 3);
            using EntityQuery q = em.CreateEntityQuery(typeof(STycoonIngredientQuality));
            if (q.IsEmpty) { Entity e = em.CreateEntity(typeof(STycoonIngredientQuality)); em.SetComponentData(e, new STycoonIngredientQuality { Tier = tier }); }
            else q.SetSingleton(new STycoonIngredientQuality { Tier = tier });
            TycoonIngredientQuality._current = tier;
            Mod.LogInfo($"Management Desk: ingredient quality set to {TycoonIngredientQuality.NAMES[tier]}.");
        }
    }
}
