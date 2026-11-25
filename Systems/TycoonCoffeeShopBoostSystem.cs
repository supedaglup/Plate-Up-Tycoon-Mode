using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Components
{
    /// <summary>Tag on the customer-numbers modifier the Coffee Shop card's Tycoon boost owns.</summary>
    public struct CTycoonCoffeeShopBoost : IComponentData, IModComponent
    {
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Extra customers while the Coffee Shop card is on and coffee or a dessert is on the menu.</summary>
    public class TycoonCoffeeShopBoostSystem : RestaurantSystem, IModSystem
    {
        internal static float CoffeeOnlyShare;
        internal static float CoffeeGuestsToday;
        internal static readonly HashSet<Entity> CoffeeGroups = new HashSet<Entity>();
        internal static int CoffeeOnlyPhase = -1;
        internal static bool CoffeeShopOn;
        internal static bool TakeOutOn;

        internal static float CoffeeGuests(int stars, int players) => Mathf.Floor(ExtraGuests(stars, players) / 2f);
        internal static float TakeOutGuests(int stars, int players) => ExtraGuests(stars, players) - CoffeeGuests(stars, players);

        internal static float ExtraGuests(int stars, int players)
        {
            float extra = TycoonEconomy.COFFEE_SHOP_EXTRA_BASE * Mathf.Pow(TycoonEconomy.COFFEE_SHOP_EXTRA_GROWTH, TycoonEconomy.ClampStars(stars));
            if (players > 1) extra *= TycoonEconomy.COFFEE_SHOP_MULTIPLAYER_FACTOR;
            return Mathf.Floor(extra);
        }

        private EntityQuery _boosts;
        private EntityQuery _menu;
        private string _last = "";

        protected override void Initialise()
        {
            base.Initialise();
            _boosts = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCoffeeShopBoost)));
            _menu = GetEntityQuery(new QueryHelper().All(typeof(CMenuItem)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STycoonCards>();
        }

        protected override void OnUpdate()
        {
            if (!_boosts.IsEmpty) { EntityManager.DestroyEntity(_boosts); Mod.LogInfo("Coffee Shop: the old customer multiplier is gone (flat extra coffee guests instead)."); }
            CoffeeShopOn = TycoonCards.IsEnabled(EntityManager, Mod.CoffeeShopCard);
            TakeOutOn = HasSingleton<STycoonTakeOutOn>();
            CoffeeOnlyPhase = CoffeePhase();
            string report = CoffeeShopOn
                ? $"Coffee Shop: on; {CoffeeGuests(TycoonEconomy.CurrentStars, 1):0.#} extra coffee guests a day solo, {CoffeeGuests(TycoonEconomy.CurrentStars, 2):0.#} in multiplayer{(CoffeeOnlyPhase < 0 ? " (no coffee on the menu yet: none come)" : "")}."
                : "Coffee Shop: card off, no extra coffee guests.";
            if (report != _last) { _last = report; Mod.LogInfo(report); }
        }

        private static bool IsDessert(int dishID) => GameData.Main.TryGet<Dish>(dishID, out Dish d, false) && d.Type == DishType.Dessert;

        private int CoffeePhase()
        {
            int phase = int.MaxValue;
            using NativeArray<CMenuItem> items = _menu.ToComponentDataArray<CMenuItem>(Allocator.Temp);
            for (int i = 0; i < items.Length; i++)
                if (Patches.TycoonServeReputationPatch.IsCoffeeFamily(items[i].SourceDish)) phase = Mathf.Min(phase, (int)items[i].Phase);
            return phase == int.MaxValue ? -1 : phase;
        }
    }

    /// <summary>Adds the Coffee Shop's extra coffee guests to the day's total, after the reputation postfix.</summary>
    [HarmonyPatch(typeof(CreateCustomerSchedule), "DetermineTotalCustomers")]
    public static class TycoonCoffeeGuestsPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(HarmonyLib.Priority.Low)]
        private static void Postfix(ScheduleParameters schedule_parameters, ref float __result)
        {
            if (!TycoonSession.IsActive || Patches.TycoonDays.ForcedStars >= 0) return;
            TycoonCoffeeShopBoostSystem.CoffeeGroups.Clear();
            TycoonCoffeeShopBoostSystem.CoffeeGuestsToday = 0f;
            if (!TycoonCoffeeShopBoostSystem.CoffeeShopOn || TycoonCoffeeShopBoostSystem.CoffeeOnlyPhase < 0)
            {
                TycoonCoffeeShopBoostSystem.CoffeeOnlyShare = 0f;
                return;
            }
            float before = __result;
            float extra = TycoonCoffeeShopBoostSystem.CoffeeGuests(TycoonEconomy.CurrentStars, schedule_parameters.Players);
            __result = before + extra;
            TycoonCoffeeShopBoostSystem.CoffeeGuestsToday = extra;
            TycoonCoffeeShopBoostSystem.CoffeeOnlyShare = __result > 0f ? extra / __result : 0f;
            Mod.LogInfo($"Coffee Shop: +{extra:0.#} coffee guests ({schedule_parameters.Players} player(s), {TycoonEconomy.CurrentStars} star(s)): {before:0.#} -> {__result:0.#}.");
        }
    }

    /// <summary>Take-Out brings the other half of the extra guests, for any dish.</summary>
    [HarmonyPatch(typeof(CreateCustomerSchedule), "DetermineTotalCustomers")]
    public static class TycoonTakeOutGuestsPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(HarmonyLib.Priority.Low)]
        private static void Postfix(ScheduleParameters schedule_parameters, ref float __result)
        {
            if (!TycoonSession.IsActive || Patches.TycoonDays.ForcedStars >= 0 || !TycoonCoffeeShopBoostSystem.TakeOutOn) return;
            float extra = TycoonCoffeeShopBoostSystem.TakeOutGuests(TycoonEconomy.CurrentStars, schedule_parameters.Players);
            if (extra <= 0f) return;
            float before = __result;
            __result += extra;
            Mod.LogInfo($"Take-Out: +{extra:0} guests ({schedule_parameters.Players} player(s), {TycoonEconomy.CurrentStars} star(s)): {before:0.#} -> {__result:0.#}.");
        }
    }

    /// <summary>A flat number of extra customers for the low star levels, TycoonEconomy.LOW_STAR_EXTRA_CUSTOMERS[stars].</summary>
    [HarmonyPatch(typeof(CreateCustomerSchedule), "DetermineTotalCustomers")]
    public static class TycoonLowStarCustomersPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(HarmonyLib.Priority.Low)]
        private static void Postfix(ref float __result)
        {
            if (!TycoonSession.IsActive || Patches.TycoonDays.ForcedStars >= 0) return;
            int stars = TycoonEconomy.ClampStars(TycoonEconomy.CurrentStars);
            float extra = stars < TycoonEconomy.LOW_STAR_EXTRA_CUSTOMERS.Length ? TycoonEconomy.LOW_STAR_EXTRA_CUSTOMERS[stars] : 0f;
            if (extra <= 0f) return;
            float before = __result;
            __result += extra;
            Mod.LogInfo($"Customers: +{extra:0} for a young restaurant ({stars} star(s)): {before:0.#} -> {__result:0.#}.");
        }
    }

    /// <summary>The Coffee Shop's extra customers only order coffee/desserts.</summary>
    [HarmonyPatch(typeof(CreateCustomerGroup), "NewGroup")]
    public static class TycoonCoffeeOnlyGroupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CreateCustomerGroup __instance, bool is_cat, Entity __result)
        {
            if (!TycoonSession.IsActive || is_cat || TycoonPartySplitPatch.Inside || TycoonCoffeeShopBoostSystem.CoffeeOnlyShare <= 0f || TycoonCoffeeShopBoostSystem.CoffeeOnlyPhase < 0) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                if (!em.Exists(__result) || !em.HasComponent<CGroupMealPhase>(__result)) return;
                if (em.GetComponentData<CGroupMealPhase>(__result).Phase != 0) return;
                if (UnityEngine.Random.value >= TycoonCoffeeShopBoostSystem.CoffeeOnlyShare) return;

                var groups = new List<Entity> { __result };
                if (em.HasComponent<CTycoonParty>(__result))
                {
                    int party = em.GetComponentData<CTycoonParty>(__result).PartyID;
                    using EntityQuery q = em.CreateEntityQuery(typeof(CTycoonParty), typeof(CGroupMealPhase));
                    using NativeArray<Entity> all = q.ToEntityArray(Allocator.Temp);
                    foreach (Entity g in all)
                        if (g != __result && em.GetComponentData<CTycoonParty>(g).PartyID == party) groups.Add(g);
                }
                foreach (Entity g in groups)
                {
                    em.SetComponentData(g, new CGroupMealPhase { Phase = (MenuPhase)TycoonCoffeeShopBoostSystem.CoffeeOnlyPhase });
                    TycoonCoffeeShopBoostSystem.CoffeeGroups.Add(g);
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Coffee-only group: " + e.Message);
            }
        }
    }

    /// <summary>The Coffee Shop's extra guests order coffee, not other desserts.</summary>
    [HarmonyPatch(typeof(AssignMenuRequests), nameof(AssignMenuRequests.AddItemToGroupMember))]
    public static class TycoonCoffeeOrderPatch
    {
        internal static bool CoffeeGroupOrdering;
        private static int _logged;

        [HarmonyPrefix]
        private static void Prefix(Entity group) =>
            CoffeeGroupOrdering = TycoonSession.IsActive && TycoonCoffeeShopBoostSystem.CoffeeGroups.Contains(group);

        [HarmonyPostfix]
        private static void Postfix() => CoffeeGroupOrdering = false;

        [HarmonyPatch(typeof(AssignMenuRequests), nameof(AssignMenuRequests.PickRandomMenuItem))]
        public static class PickPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(NativeArray<CMenuItem> items, MenuPhase phase, ref int __result)
            {
                if (!CoffeeGroupOrdering) return true;
                float total = 0f;
                for (int i = 0; i < items.Length; i++)
                    if (items[i].Phase == phase && Patches.TycoonServeReputationPatch.IsCoffeeFamily(items[i].SourceDish)) total += items[i].Weight;
                if (total <= 0f) return true;
                float pick = UnityEngine.Random.Range(0f, total);
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Phase != phase || !Patches.TycoonServeReputationPatch.IsCoffeeFamily(items[i].SourceDish)) continue;
                    pick -= items[i].Weight;
                    if (pick <= 0f) { __result = i; break; }
                    __result = i;
                }
                if (_logged++ < 3) Mod.LogInfo($"Coffee Shop: a coffee guest orders from the coffee items only (item {items[__result].Item}).");
                return false;
            }
        }
    }
}
