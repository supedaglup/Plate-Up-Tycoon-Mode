using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Components
{
    /// <summary>The restaurant's Dish Pricing choice in percent.</summary>
    public struct STycoonDishPricing : IComponentData, IModComponent
    {
        public int Percent;
    }

    /// <summary>How used to cheap prices the customers are, in days.</summary>
    public struct STycoonDishPricingFade : IComponentData, IModComponent
    {
        public int Days;
    }
}

namespace PlateUpTycoon
{
    /// <summary>Dish Pricing, a 1-star reward.</summary>
    public static class TycoonDishPricing
    {
        public static readonly int[] CHOICES = { -30, -20, -10, 0, 10, 20, 30 };
        public const float UP_COST = 3f, DOWN_GAIN = 2f, MIN_FACTOR = 0.1f;
        public const float FADE_PER_DAY = 0.7f, FADE_FLOOR = 0.25f;
        public const int FADE_MAX_DAYS = 4;
        public const string NET_KEY = "dp", NET_KEY_PER_CUSTOMER = "rpc";

        public static int Percent
        {
            get
            {
                if (TycoonNetData.IsHost) return _percent;
                return int.TryParse(TycoonNetData.Get(NET_KEY), out int p) ? p : 0;
            }
        }
        internal static int _percent;

        public static bool Available => TycoonUnlocks.Stars >= TycoonUnlocks.PRICING_STAR;

        internal static int _fadeDays;

        public static float StarScale(int stars) => stars <= 1 ? 1f : stars == 2 ? 0.85f : stars == 3 ? 0.7f : 0.5f;

        public static float Fade(int fadeDays) => Mathf.Max(FADE_FLOOR, Mathf.Pow(FADE_PER_DAY, Mathf.Clamp(fadeDays, 0, FADE_MAX_DAYS)));

        public static float ReputationFactor(int percent, int stars, int fadeDays)
        {
            float share = percent / 100f;
            return share >= 0f ? Mathf.Max(MIN_FACTOR, 1f - UP_COST * share) : 1f + DOWN_GAIN * -share * StarScale(stars) * Fade(fadeDays);
        }

        public static float ReputationFactorNow => ReputationFactor(Percent, TycoonEconomy.CurrentStars, _fadeDays);

        public static int Encode(int percent) => -75000 - (percent + 50);
        public static bool Decode(int code, out int percent)
        {
            percent = -75000 - code - 50;
            return code <= -75000 && code > -75100 && System.Array.IndexOf(CHOICES, percent) >= 0;
        }

        public static string Effect(int percent) =>
            percent >= 30 ? "very little reputation per customer"
            : percent >= 20 ? "much less reputation per customer"
            : percent >= 10 ? "less reputation per customer"
            : percent <= -30 ? "far more reputation per customer"
            : percent <= -20 ? "much more reputation per customer"
            : percent <= -10 ? "more reputation per customer"
            : "normal reputation";

        public static string Describe(int percent) =>
            (percent == 0 ? "Normal Prices" : $"Prices {(percent > 0 ? "+" : "")}{percent}%") + $"  -  {Effect(percent)}{(percent == Percent ? "  (current)" : "")}";

        public static bool TryPerCustomer(out float value)
        {
            if (TycoonNetData.IsHost) { value = _perCustomer; return _perCustomerKnown; }
            value = 0f;
            string s = TycoonNetData.Get(NET_KEY_PER_CUSTOMER);
            return !string.IsNullOrEmpty(s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }
        internal static float _perCustomer;
        internal static bool _perCustomerKnown;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Keeps TycoonDishPricing in step with the saved choice, sends it to guests.</summary>
    public class TycoonDishPricingSystem : RestaurantSystem, IModSystem
    {
        private const float TYPICAL_PATIENCE = 0.75f;
        private int _logged = int.MinValue;
        private float _next;

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            int picked = HasSingleton<STycoonDishPricing>() ? GetSingleton<STycoonDishPricing>().Percent : 0;
            if (System.Array.IndexOf(TycoonDishPricing.CHOICES, picked) < 0) picked = 0;
            int percent = TycoonEconomy.CurrentStars >= TycoonUnlocks.PRICING_STAR ? picked : 0;
            TycoonDishPricing._percent = percent;
            TycoonDishPricing._fadeDays = HasSingleton<STycoonDishPricingFade>() ? GetSingleton<STycoonDishPricingFade>().Days : 0;
            TycoonNetData.Set(TycoonDishPricing.NET_KEY, percent.ToString());
            float factor = TycoonDishPricing.ReputationFactorNow;
            int logKey = percent * 1000 + Mathf.RoundToInt(factor * 100f);
            if (logKey != _logged)
            {
                _logged = logKey;
                Mod.LogInfo($"Dish Pricing: prices {percent:+0;-0;0}%, serve reputation x{factor:0.00} when positive ({TycoonEconomy.CurrentStars} star, used to cheap prices for {TycoonDishPricing._fadeDays} day(s)){(percent != picked ? $" (picked {picked}%, below {TycoonUnlocks.PRICING_STAR} star)" : "")}.");
            }

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _next) return;
            _next = now + 0.5f;
            int serves = TycoonReputationReport.Serves;
            float patience = serves > 0 ? TycoonReputationReport.SumPatience / serves : TycoonReputation.PatienceModifier(TYPICAL_PATIENCE);
            float mess = serves > 0 ? TycoonReputationReport.SumMess / serves : 0f;
            float serve = TycoonReputation.BASE_GAIN + patience + TycoonReputation.CardBonus + TycoonPromotions.RepPerServe
                + (TycoonContractSystem.LuxuryActive ? TycoonPromotions.LUXURY_REP_PER_SERVE : 0f) + mess;
            if (serve > 0f) serve *= TycoonReputation.VolumeFactor * factor;
            float perCustomer = serve * Mathf.Max(1f, TycoonReputation.ServesPerCustomer);
            TycoonDishPricing._perCustomer = perCustomer;
            TycoonDishPricing._perCustomerKnown = true;
            TycoonNetData.Set(TycoonDishPricing.NET_KEY_PER_CUSTOMER, perCustomer.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }

        internal static void CloseDay(EntityManager em)
        {
            int days = TycoonDishPricing._fadeDays;
            int next = TycoonDishPricing._percent < 0 ? Mathf.Min(days + 1, TycoonDishPricing.FADE_MAX_DAYS) : Mathf.Max(0, days - 1);
            using (EntityQuery q = em.CreateEntityQuery(typeof(STycoonDishPricingFade)))
            {
                if (q.IsEmpty) { Entity e = em.CreateEntity(typeof(STycoonDishPricingFade)); em.SetComponentData(e, new STycoonDishPricingFade { Days = next }); }
                else q.SetSingleton(new STycoonDishPricingFade { Days = next });
            }
            TycoonDishPricing._fadeDays = next;
            if (next != days) Mod.LogInfo($"Dish Pricing: customers used to cheap prices for {next} day(s) (was {days}).");
        }

        internal static void Pick(EntityManager em, int percent)
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(STycoonDishPricing));
            if (q.IsEmpty) { Entity e = em.CreateEntity(typeof(STycoonDishPricing)); em.SetComponentData(e, new STycoonDishPricing { Percent = percent }); }
            else q.SetSingleton(new STycoonDishPricing { Percent = percent });
            Mod.LogInfo($"Management Desk: Dish Pricing set to {percent:+0;-0;0}%.");
        }
    }
}
