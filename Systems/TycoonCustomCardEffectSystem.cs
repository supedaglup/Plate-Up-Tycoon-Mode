using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>The restaurant's measured items served per customer.</summary>
    public struct STycoonServeRatio : IComponentData, IModComponent
    {
        public float ServesPerCustomer;
    }

    /// <summary>The customer modifier of one of Chubs's custom customer cards while it is on.</summary>
    public struct CTycoonCustomCardEffect : IComponentData, IModComponent
    {
        public int UnlockID;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Applies the customer modifiers of Chubs's custom cards while they are on.</summary>
    public class TycoonCustomCardEffectSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _cards, _effects;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            _effects = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCustomCardEffect)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            var wanted = new Dictionary<int, float>();
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                foreach (CTycoonCard c in cards)
                    if (c.Enabled && TycoonCustomerCards.Of(c.UnlockID) is TycoonCustomerCards.Card card) wanted[c.UnlockID] = card.Customers / 100f;

            using (NativeArray<Entity> effects = _effects.ToEntityArray(Allocator.Temp))
                foreach (Entity e in effects)
                {
                    int id = EntityManager.GetComponentData<CTycoonCustomCardEffect>(e).UnlockID;
                    if (!wanted.TryGetValue(id, out float factor)) { EntityManager.DestroyEntity(e); Mod.LogInfo($"Customer cards: {TycoonCustomerCards.Of(id)?.Name ?? id.ToString()} off."); continue; }
                    wanted.Remove(id);
                    if (!Has<CCustomerSpawnModifier>(e) || UnityEngine.Mathf.Abs(EntityManager.GetComponentData<CCustomerSpawnModifier>(e).BaseCustomerMultiplier.Value - factor) > 0.0001f)
                        EntityManager.AddComponentData(e, new CCustomerSpawnModifier { BaseCustomerMultiplier = new Factor { Value = factor }, PerDayCustomerMultiplier = new Factor { Value = 0f } });
                }
            foreach (KeyValuePair<int, float> w in wanted)
            {
                Entity e = EntityManager.CreateEntity(typeof(CTycoonCustomCardEffect), typeof(CCustomerSpawnModifier));
                EntityManager.SetComponentData(e, new CTycoonCustomCardEffect { UnlockID = w.Key });
                EntityManager.SetComponentData(e, new CCustomerSpawnModifier { BaseCustomerMultiplier = new Factor { Value = w.Value }, PerDayCustomerMultiplier = new Factor { Value = 0f } });
                Mod.LogInfo($"Customer cards: {TycoonCustomerCards.Of(w.Key)?.Name} on ({w.Value * 100f:+0}% customers).");
            }
        }
    }

    /// <summary>The day's final customer total is what reputation per serve is measured against.</summary>
    [HarmonyPatch(typeof(CreateCustomerSchedule), "DetermineTotalCustomers")]
    public static class TycoonCustomerVolumePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void Postfix(float __result)
        {
            if (!TycoonSession.IsActive || Patches.TycoonDays.ForcedStars >= 0) return;
            TycoonReputation.CustomersToday = __result;
            Mod.LogInfo($"Reputation: {__result:0.#} customer(s) today x {TycoonReputation.ServesPerCustomer:0.00} serves each = {TycoonReputation.ServesToday:0.#} serves, each serve worth x{TycoonReputation.VolumeFactor:0.00} (measured against {TycoonReputation.REFERENCE_SERVES:0}).");
        }
    }

    /// <summary>Serves per customer, measured.</summary>
    public class TycoonServeRatioSystem : RestaurantSystem, IModSystem
    {
        private int _nightDone = -1;
        private World _world;

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (_world != World)
            {
                _world = World;
                TycoonReputation.MeasuredServes = 0;
                TycoonReputation.MeasuredPeople = 0;
                TycoonReputation.MeasuredGroups.Clear();
                TycoonReputation.GroupServes.Clear();
            }
            TycoonReputation.ServesPerCustomer = HasSingleton<STycoonServeRatio>() && GetSingleton<STycoonServeRatio>().ServesPerCustomer >= 1f
                ? GetSingleton<STycoonServeRatio>().ServesPerCustomer : TycoonReputation.DEFAULT_SERVES_PER_CUSTOMER;
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (!HasSingleton<SIsNightFirstUpdate>() || _nightDone == day) return;
            _nightDone = day;
            if (TycoonReputation.MeasuredPeople > 0)
            {
                float today = (float)TycoonReputation.MeasuredServes / TycoonReputation.MeasuredPeople;
                float ratio = UnityEngine.Mathf.Clamp((TycoonReputation.ServesPerCustomer + today) / 2f, 1f, TycoonReputation.SERVES_PER_CUSTOMER_MAX);
                Mod.LogInfo($"Reputation: {TycoonReputation.MeasuredServes} serve(s) for {TycoonReputation.MeasuredPeople} customer(s) today ({today:0.00} each); serves per customer {TycoonReputation.ServesPerCustomer:0.00} -> {ratio:0.00}.");
                TycoonReputation.ServesPerCustomer = ratio;
                if (!HasSingleton<STycoonServeRatio>()) EntityManager.CreateEntity(typeof(STycoonServeRatio));
                SetSingleton(new STycoonServeRatio { ServesPerCustomer = ratio });
            }
            TycoonReputation.MeasuredServes = 0;
            TycoonReputation.MeasuredPeople = 0;
            TycoonReputation.MeasuredGroups.Clear();
            TycoonReputation.GroupServes.Clear();
        }
    }
}
