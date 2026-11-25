using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Debug: ends the current service phase immediately.</summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public class TycoonDebugEndDaySystem : GenericSystemBase, IModSystem
    {
        private EntityQuery _requests;
        private EntityQuery _schedule;
        private EntityQuery _customers;

        protected override void Initialise()
        {
            base.Initialise();
            _requests = GetEntityQuery(new QueryHelper().All(typeof(CTycoonDebugEndDay)));
            _schedule = GetEntityQuery(new QueryHelper().All(typeof(CScheduledCustomer)));
            _customers = GetEntityQuery(new QueryHelper().Any(typeof(CCustomer), typeof(CCustomerGroup)));
            RequireForUpdate(_requests);
            Mod.LogInfo("End-service debug system ready (runs at the start of each frame).");
        }

        protected override void OnUpdate()
        {
            EntityManager.DestroyEntity(_requests);

            if (!HasSingleton<SIsDayTime>() || !HasSingleton<STime>())
            {
                Mod.LogWarning("DEBUG: 'End service now' ignored - it is not service time.");
                return;
            }

            int scheduled = _schedule.CalculateEntityCount();
            int customers = _customers.CalculateEntityCount();
            EntityManager.DestroyEntity(_schedule);
            EntityManager.DestroyEntity(_customers);

            STime time = GetSingleton<STime>();
            if (time.TimeOfDayUnbounded < 1f) time.TimeOfDayUnbounded = 1f;
            time.TimeOfDay = 1f;
            Set(time);

            Mod.LogWarning($"DEBUG: service ended ({customers} customer entities and {scheduled} scheduled arrivals removed).");
        }
    }
}
