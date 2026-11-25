using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Host side: applies the Take-Out card while it is on, and removes it when it is off.</summary>
    public class TycoonTakeOutSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _boosts;

        protected override void Initialise()
        {
            base.Initialise();
            _boosts = GetEntityQuery(new QueryHelper().All(typeof(CTycoonSpawnBoost)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STycoonCards>();
        }

        protected override void OnUpdate()
        {
            bool on = Mod.TakeOutCard != null && TycoonCards.IsEnabled(EntityManager, Mod.TakeOutCard.ID);

            if (HasSingleton<STycoonTakeOutStatusApplied>())
            {
                RemoveStatus(RestaurantStatus.CustomersOrderImmediately);
                EntityManager.DestroyEntity(GetSingletonEntity<STycoonTakeOutStatusApplied>());
                Mod.LogInfo("Take-Out: customers no longer order on arrival (removed from this restaurant).");
            }

            bool marked = HasSingleton<STycoonTakeOutOn>();
            if (on && !marked)
            {
                Set(new STycoonTakeOutOn());
                Mod.LogInfo("Take-Out: on (orders taken at the counter; more customers from tomorrow).");
            }
            else if (!on && marked)
            {
                EntityManager.DestroyEntity(GetSingletonEntity<STycoonTakeOutOn>());
                Mod.LogInfo("Take-Out: off (regular table service; customer numbers change from tomorrow).");
            }

            if (HasSingleton<STycoonTakeOutGroupSize>())
            {
                STycoonTakeOutGroupSize original = GetSingleton<STycoonTakeOutGroupSize>();
                if (HasSingleton<SKitchenParameters>())
                {
                    SKitchenParameters parameters = GetSingleton<SKitchenParameters>();
                    parameters.Parameters.MinimumGroupSize = UnityEngine.Mathf.Max(1, original.OriginalMinimum);
                    parameters.Parameters.MaximumGroupSize = UnityEngine.Mathf.Max(parameters.Parameters.MinimumGroupSize, original.OriginalMaximum);
                    Set(parameters);
                    Mod.LogInfo($"Take-Out: group sizes back to {parameters.Parameters.MinimumGroupSize}-{parameters.Parameters.MaximumGroupSize} (friends are split at the door instead).");
                }
                EntityManager.DestroyEntity(GetSingletonEntity<STycoonTakeOutGroupSize>());
            }
            if (!_boosts.IsEmpty)
            {
                EntityManager.DestroyEntity(_boosts);
                Mod.LogInfo("Take-Out: no longer brings extra customers (the Coffee Shop card does that now).");
            }
        }
    }

    /// <summary>Take-Out, per customer group at an Ordering Counter: grab-and-go timings, takeaway prices.</summary>
    [HarmonyLib.HarmonyPatch(typeof(UpdateCustomerGroupSettings), "OnUpdate")]
    public static class TycoonTakeOutCounterSettingsPatch
    {
        [HarmonyLib.HarmonyPostfix]
        private static void Postfix(UpdateCustomerGroupSettings __instance)
        {
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                bool takeOut;
                using (EntityQuery takeOutQuery = em.CreateEntityQuery(typeof(STycoonTakeOutOn))) takeOut = !takeOutQuery.IsEmpty;
                using EntityQuery groups = em.CreateEntityQuery(typeof(CCustomerGroup), typeof(CCustomerSettings));
                if (groups.IsEmpty) return;
                using NativeArray<Entity> entities = groups.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    CCustomerSettings settings = em.GetComponentData<CCustomerSettings>(entities[i]);
                    settings.Ordering.PriceModifier *= TycoonEconomy.DISH_PRICE_MULTIPLIER;

                    bool atCounter = takeOut && em.HasComponent<CAssignedTable>(entities[i])
                        && TycoonTakeOutCounterSystem.IsOrderingCounterSet(em, em.GetComponentData<CAssignedTable>(entities[i]).Table);
                    if (atCounter)
                    {
                        bool fastCasual = em.HasComponent<CTycoonParty>(entities[i]) && em.GetComponentData<CTycoonParty>(entities[i]).FastCasual;
                        settings.Patience.Eating = fastCasual
                            ? TycoonEconomy.FAST_CASUAL_COUNTER_HOLD
                            : UnityEngine.Mathf.Min(settings.Patience.Eating, TycoonEconomy.TAKEOUT_EATING_SECONDS);
                        settings.Patience.Thinking = UnityEngine.Mathf.Min(settings.Patience.Thinking, TycoonEconomy.TAKEOUT_THINKING_SECONDS);
                        settings.Ordering.PriceModifier *= TycoonEconomy.TAKEOUT_PRICE_FACTOR;
                        settings.Ordering.PreventMess = true;
                    }
                    em.SetComponentData(entities[i], settings);
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Take-Out counter settings: " + e.Message);
            }
        }
    }

    /// <summary>Helpers for recognising the Ordering Counter.</summary>
    public static class TycoonTakeOutCounterSystem
    {
        internal static bool IsOrderingCounterSet(EntityManager em, Entity tableSet)
        {
            if (tableSet == Entity.Null || !em.Exists(tableSet)) return false;
            if (em.HasComponent<CTycoonOrderingCounter>(tableSet)) return true;
            if (!em.HasComponent<CTableSetParts>(tableSet)) return false;
            DynamicBuffer<CTableSetParts> parts = em.GetBuffer<CTableSetParts>(tableSet);
            for (int i = 0; i < parts.Length; i++)
                if (em.Exists(parts[i]) && em.HasComponent<CTycoonOrderingCounter>(parts[i])) return true;
            return false;
        }
    }
}
