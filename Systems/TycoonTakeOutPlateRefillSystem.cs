using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Take-Out Plates never run out: every rack has its CItemProvider.Available kept at Maximum.</summary>
    public class TycoonTakeOutPlateRefillSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _racks;
        private EntityQuery _appliances;
        private bool _listed;

        protected override void Initialise()
        {
            base.Initialise();
            _racks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonTakeOutPlateRack), typeof(CItemProvider)));
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance), typeof(CPosition)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        private void RemoveReservations()
        {
            if (!HasSingleton<SIsNightTime>()) return;
            int reservation = KitchenLib.References.ApplianceReferences.ItemSourceReservation;
            int removed = 0;
            using NativeArray<Entity> all = _appliances.ToEntityArray(Allocator.Temp);
            using NativeArray<CAppliance> apps = _appliances.ToComponentDataArray<CAppliance>(Allocator.Temp);
            for (int i = 0; i < all.Length; i++)
                if (apps[i].ID == reservation) { EntityManager.DestroyEntity(all[i]); removed++; }
            if (removed > 0) Mod.LogInfo($"Kitchen: {removed} empty ingredient-box spot(s) (ItemSourceReservation) removed.");
        }

        protected override void OnUpdate()
        {
            if (!_listed && !_appliances.IsEmpty)
            {
                _listed = true;
                var parts = new List<string>();
                using NativeArray<CAppliance> apps = _appliances.ToComponentDataArray<CAppliance>(Allocator.Temp);
                using NativeArray<CPosition> pos = _appliances.ToComponentDataArray<CPosition>(Allocator.Temp);
                for (int i = 0; i < apps.Length; i++)
                {
                    string name = GameData.Main.TryGet<Appliance>(apps[i].ID, out Appliance a, false) && !string.IsNullOrEmpty(a.Name) ? a.Name : apps[i].ID.ToString();
                    parts.Add($"{name} ({UnityEngine.Mathf.RoundToInt(pos[i].Position.x)},{UnityEngine.Mathf.RoundToInt(pos[i].Position.z)})");
                }
                Mod.LogInfo($"Restaurant appliances ({apps.Length}): {string.Join("; ", parts)}.");
            }
            RemoveReservations();
            if (_racks.IsEmpty) return;
            using NativeArray<Entity> racks = _racks.ToEntityArray(Allocator.Temp);
            foreach (Entity rack in racks)
            {
                CItemProvider p = EntityManager.GetComponentData<CItemProvider>(rack);
                bool changed = false;
                if (p.ProvidedItem == 0) { p.SetAsItem(KitchenLib.References.ItemReferences.Plate); changed = true; }
                if (p.Maximum < 2) { p.Maximum = 8; changed = true; }
                int target = p.Maximum - 1;
                if (p.Available != target) { p.Available = target; changed = true; }
                if (changed) EntityManager.SetComponentData(rack, p);
            }
        }
    }
}
