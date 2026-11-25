using Kitchen;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Debug: skips the HQ and opens the restaurant once the loadout is complete.</summary>
    public class DebugAutoStartSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery _beginSelectors;

        protected override void Initialise()
        {
            base.Initialise();
            _beginSelectors = GetEntityQuery(new QueryHelper()
                .All(typeof(SBeginGameSelector), typeof(CGroupSelector))
                .None(typeof(CSelectorActivated)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || TycoonSession.DebugAutoStartUsed || !TycoonPreferences.DebugAutoStart)
                return;
            if (!HasSingleton<SLoadoutStatus>() || !GetSingleton<SLoadoutStatus>().IsReady)
                return;
            if (_beginSelectors.IsEmpty)
                return;

            using NativeArray<Entity> selectors = _beginSelectors.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < selectors.Length; i++)
                EntityManager.AddComponent<CSelectorActivated>(selectors[i]);

            TycoonSession.DebugAutoStartUsed = true;
            Mod.LogWarning("DEBUG: auto-starting the restaurant (Mod Preferences → Tycoon Mode).");
        }
    }
}
