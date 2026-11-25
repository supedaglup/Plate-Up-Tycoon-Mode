using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Makes sure every Tycoon desk in the restaurant has the components its menu needs.</summary>
    public class EnsureTycoonDesksSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _appliances;

        protected override void Initialise()
        {
            base.Initialise();
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (Mod.PurchasingDesk == null || Mod.ManagementDesk == null) return;

            using NativeArray<Entity> entities = _appliances.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                int id = EntityManager.GetComponentData<CAppliance>(entities[i]).ID;
                bool purchasing = id == Mod.PurchasingDesk.ID;
                bool management = id == Mod.ManagementDesk.ID;
                if (!purchasing && !management) continue;

                bool fixedSomething = false;
                if (purchasing && !Has<CTycoonPurchasingDesk>(entities[i])) { EntityManager.AddComponent<CTycoonPurchasingDesk>(entities[i]); fixedSomething = true; }
                if (management && !Has<CTycoonManagementDesk>(entities[i])) { EntityManager.AddComponent<CTycoonManagementDesk>(entities[i]); fixedSomething = true; }
                if (!Has<CTriggerPlayerSpecificUI>(entities[i])) { EntityManager.AddComponent<CTriggerPlayerSpecificUI>(entities[i]); fixedSomething = true; }
                if (fixedSomething) Mod.LogInfo("Tycoon desk: added missing menu components to a desk.");
            }
        }
    }
}
