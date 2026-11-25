using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Debug: applies the reputation buttons of the debug menu.</summary>
    public class TycoonDebugReputationSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _requests;

        protected override void Initialise()
        {
            base.Initialise();
            _requests = GetEntityQuery(new QueryHelper().All(typeof(CTycoonDebugReputation)));
            RequireForUpdate(_requests);
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STycoonReputation>();
        }

        protected override void OnUpdate()
        {
            float total = 0f;
            using (NativeArray<CTycoonDebugReputation> requests = _requests.ToComponentDataArray<CTycoonDebugReputation>(Allocator.Temp))
                for (int i = 0; i < requests.Length; i++) total += requests[i].Amount;
            EntityManager.DestroyEntity(_requests);

            STycoonReputation rep = GetSingleton<STycoonReputation>();
            TycoonReputation.Add(ref rep, total);
            TycoonReputationReport.Note("Debug", total);
            Set(rep);
            TycoonReputation.Current = rep;
            Mod.LogWarning($"DEBUG: reputation {total:+0.##;-0.##}, now {rep.Reputation:0.00}.");
        }
    }
}
