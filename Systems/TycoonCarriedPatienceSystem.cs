using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Take-Out only: one pool of patience per customer, in seconds, shared by every phase.</summary>
    public class TycoonCarriedPatienceSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _groups;

        protected override void Initialise()
        {
            base.Initialise();
            _groups = GetEntityQuery(new QueryHelper().All(typeof(CCustomerGroup), typeof(CTycoonCarriedPatience), typeof(CPatience), typeof(CCustomerSettings)));
            RequireForUpdate(_groups);
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> groups = _groups.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                Entity group = groups[i];
                CPatience patience = EntityManager.GetComponentData<CPatience>(group);
                if (!patience.Active) continue;
                if (patience.Reason != PatienceReason.Service && patience.Reason != PatienceReason.WaitForFood) continue;

                CCustomerSettings settings = EntityManager.GetComponentData<CCustomerSettings>(group);
                float phaseSeconds = settings[patience.Reason];
                if (phaseSeconds <= 0f) continue;

                CTycoonCarriedPatience pool = EntityManager.GetComponentData<CTycoonCarriedPatience>(group);
                int phase = (int)patience.Reason + 1;
                if (pool.AppliedPhase != phase)
                {
                    pool.AppliedPhase = phase;
                    pool.PhaseBudget = UnityEngine.Mathf.Max(0.01f, pool.Remaining);
                    pool.LastWritten = 1f;
                    Mod.LogInfo($"Take-Out patience: {patience.Reason} bar starts full and lasts {pool.Remaining:0.0} s (of {pool.Total:0.0} s).");
                }
                else
                {
                    float drained = pool.LastWritten - patience.RemainingTime;
                    if (drained < -0.05f) { pool.Remaining = pool.Total; pool.PhaseBudget = pool.Total; }
                    else if (drained > 0f) pool.Remaining -= drained * phaseSeconds;
                }

                float bar = UnityEngine.Mathf.Clamp01(pool.Remaining / pool.PhaseBudget);
                patience.StartTime = 1f;
                patience.RemainingTime = bar;
                pool.LastWritten = bar;
                EntityManager.SetComponentData(group, patience);
                EntityManager.SetComponentData(group, pool);
            }
        }
    }
}
