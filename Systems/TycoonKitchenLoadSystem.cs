using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Kitchen load: a bigger menu brings fewer customers who each pay more.</summary>
    public static class TycoonKitchenLoad
    {
        public const float MAIN_WEIGHT = 1f, COURSE_WEIGHT = 0.6f, EXTRA_WEIGHT = 0.3f;
        public const float AUTOMATION_PER = 0.5f, AUTOMATION_MAX_SHARE = 0.5f;
        public const float CUSTOMERS_PER_LOAD = 0.036f, PRICE_PER_LOAD = 0.031f;
        public const float PRICE_PER_MAIN_LOAD = 0.04f;
        public const float MAIN_CUSTOMER_CUT = 0.5f, ADDED_MAIN_PRICE = 0.57f;
        public const float SIDE_EXTRA_CUSTOMER_WEIGHT = 0.3f;
        public static float AddedMains;
        public static float CustomerLoad;
        public static readonly System.Collections.Generic.HashSet<int> StartingMains = new System.Collections.Generic.HashSet<int>();

        public static int CardCustomerCut(Dish dish)
        {
            if (dish == null) return 0;
            float keep = dish.Type == DishType.Base
                ? (StartingMains.Contains(dish.ID) ? 1f : 1f / (1f + MAIN_CUSTOMER_CUT))
                : 1f / (1f + CUSTOMERS_PER_LOAD * Mathf.Max(1, dish.RecipeDifficulty()) * (Weight(dish) + (dish.Type == DishType.Side ? SIDE_EXTRA_CUSTOMER_WEIGHT : 0f)));
            return Mathf.RoundToInt((1f - keep) * 100f);
        }

        public static float Load, Raw, Automation;
        public static float MainShare;
        public static int Dishes, AutomationAppliances, Players = 1;

        public static float Weight(Dish d)
        {
            switch (d.Type)
            {
                case DishType.Base:
                case DishType.Main: return MAIN_WEIGHT;
                case DishType.Extra: return EXTRA_WEIGHT;
                default: return COURSE_WEIGHT;
            }
        }

        public static float Shown
        {
            get
            {
                if (TycoonNetData.IsHost) return Load;
                string kl = TycoonNetData.Get("kl");
                if (string.IsNullOrEmpty(kl)) return 0f;
                float.TryParse(kl.Split(',')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
                return v;
            }
        }

        public static float CustomerMultiplier() =>
            1f / (1f + MAIN_CUSTOMER_CUT * Mathf.Max(0f, AddedMains)) / (1f + CUSTOMERS_PER_LOAD * Mathf.Max(0f, CustomerLoad));
        public static float PriceBonus(float load) =>
            Mathf.Max(0f, load) * (PRICE_PER_MAIN_LOAD * MainShare + PRICE_PER_LOAD * (1f - MainShare))
            + ADDED_MAIN_PRICE * Mathf.Max(0f, AddedMains);

        public static string Text(float load) => $"Kitchen load {load:0.#}, {AddedMains:0.#} added main(s): customers x{CustomerMultiplier():0.00}, prices +{PriceBonus(load) * 100f:0}%";
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Works out the kitchen load every second in the restaurant and sends it to guests.</summary>
    public class TycoonKitchenLoadSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _cards, _appliances, _players;
        private float _next;
        private string _logged = "";

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance)));
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _next) return;
            _next = now + 1f;

            float raw = 0f, mains = 0f, baseRaw = 0f, sideExtra = 0f;
            int dishes = 0, bases = 0;
            TycoonKitchenLoad.StartingMains.Clear();
            using (NativeArray<Entity> all = _cards.ToEntityArray(Allocator.Temp))
            using (NativeArray<CTycoonCard> owned = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                for (int i = 0; i < owned.Length; i++)
                    if (owned[i].Star == 0 && !Has<CTycoonCardBought>(all[i]) && GameData.Main.TryGet<Dish>(owned[i].UnlockID, out Dish start, false) && start.Type == DishType.Base)
                        TycoonKitchenLoad.StartingMains.Add(start.ID);
            using (NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp))
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                for (int i = 0; i < cards.Length; i++)
                {
                    if (!(cards[i].Vanilla || Has<CTycoonCardApplied>(entities[i]))) continue;
                    if (!GameData.Main.TryGet<Dish>(cards[i].UnlockID, out Dish dish, false)) continue;
                    dishes++;
                    float part = Mathf.Max(1, dish.RecipeDifficulty()) * TycoonKitchenLoad.Weight(dish);
                    raw += part;
                    if (dish.Type == DishType.Base || dish.Type == DishType.Main) mains += part;
                    if (dish.Type == DishType.Base) { bases++; baseRaw += part; }
                    if (dish.Type == DishType.Side) sideExtra += Mathf.Max(1, dish.RecipeDifficulty()) * TycoonKitchenLoad.SIDE_EXTRA_CUSTOMER_WEIGHT;
                }
            int automation = 0;
            using (NativeArray<CAppliance> appliances = _appliances.ToComponentDataArray<CAppliance>(Allocator.Temp))
                foreach (CAppliance a in appliances)
                    if (TycoonCatalogue.TryGetAny(a.ID, out TycoonCatalogue.Entry e) && e.Category == TycoonCatalogue.Category.Automation) automation++;
            int players = Mathf.Max(1, _players.CalculateEntityCount());

            float cut = Mathf.Min(TycoonKitchenLoad.AUTOMATION_PER * automation, TycoonKitchenLoad.AUTOMATION_MAX_SHARE * raw);
            float load = (raw - cut) / Mathf.Sqrt(players);
            TycoonKitchenLoad.Raw = raw;
            TycoonKitchenLoad.MainShare = raw > 0f ? mains / raw : 0f;
            TycoonKitchenLoad.Automation = cut;
            TycoonKitchenLoad.Load = load;
            TycoonKitchenLoad.AddedMains = Mathf.Max(0, bases - 1) / Mathf.Sqrt(players);
            TycoonKitchenLoad.CustomerLoad = (Mathf.Max(0f, raw - baseRaw - cut) + sideExtra) / Mathf.Sqrt(players);
            TycoonKitchenLoad.Dishes = dishes;
            TycoonKitchenLoad.AutomationAppliances = automation;
            TycoonKitchenLoad.Players = players;
            TycoonReputation.MenuDishes = dishes;
            TycoonNetData.Set("kl", $"{load.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)},{dishes},{automation},{players}");

            string summary = $"{dishes}:{bases}:{raw:0.#}:{automation}:{players}";
            if (summary != _logged)
            {
                _logged = summary;
                Mod.LogInfo($"Kitchen load: {dishes} dish(es) = {raw:0.#}, automation ({automation} appliance(s)) -{cut:0.#}, {players} player(s) -> {load:0.#}. {TycoonKitchenLoad.Text(load)}.");
            }
        }
    }

    /// <summary>The day's customers x TycoonKitchenLoad.CustomerMultiplier.</summary>
    [HarmonyPatch(typeof(CreateCustomerSchedule), "DetermineTotalCustomers")]
    public static class TycoonKitchenLoadCustomersPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(HarmonyLib.Priority.VeryLow)]
        private static void Postfix(ScheduleParameters schedule_parameters, ref float __result)
        {
            if (!TycoonSession.IsActive || Patches.TycoonDays.ForcedStars >= 0) return;
            float before = __result;
            __result *= TycoonKitchenLoad.CustomerMultiplier();
            __result *= TycoonEconomy.CrowdFactor(schedule_parameters.Players);
            TycoonCoffeeShopBoostSystem.CoffeeOnlyShare = before > 0f ? Mathf.Clamp01(TycoonCoffeeShopBoostSystem.CoffeeGuestsToday / before) : 0f;
            Mod.LogInfo($"Customers: {TycoonKitchenLoad.Text(TycoonKitchenLoad.Load)}: {before:0.#} -> {__result:0.#}"
                + (TycoonCoffeeShopBoostSystem.CoffeeOnlyShare > 0f ? $"; {TycoonCoffeeShopBoostSystem.CoffeeOnlyShare * 100f:0}% of groups come only for coffee." : "."));
        }
    }
}
