using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>The HQ's restaurant selector.</summary>
    public class TycoonResumeRestaurantSystem : FranchiseSystem, IModSystem
    {
        internal const int STATE_SAVED = 1;

        private EntityQuery _slots;
        private static Entity _announceAt = Entity.Null;
        private static string _announcement;

        internal static void Announce(Entity slot, string text)
        {
            _announceAt = slot;
            _announcement = text;
        }

        protected override void Initialise()
        {
            base.Initialise();
            _slots = GetEntityQuery(new QueryHelper().All(typeof(CLocationChoice)).None(typeof(CTycoonAnnouncement)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _slots.IsEmpty || !HasSingleton<SSelectedLocation>() || HasSingleton<SPerformSceneTransition>())
                return;

            if (_announcement != null)
            {
                if (_announceAt != Entity.Null && Require(_announceAt, out CPosition at))
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, at.Position, _announcement, 4f);
                _announcement = null;
            }

            TycoonCompany.EnsureLoaded();
            bool found = false;
            CLocationChoice best = default, chosen = default;
            bool chosenFound = false;
            using NativeArray<Entity> entities = _slots.ToEntityArray(Allocator.Temp);
            using NativeArray<CLocationChoice> slots = _slots.ToComponentDataArray<CLocationChoice>(Allocator.Temp);
            for (int i = 0; i < slots.Length; i++)
            {
                bool saved = (int)slots[i].State == STATE_SAVED;
                if (Has<CRequiresGenericInputIndicator>(entities[i])) EntityManager.RemoveComponent<CRequiresGenericInputIndicator>(entities[i]);
                if (!saved)
                {
                    if (!Has<CHideView>(entities[i])) EntityManager.AddComponent<CHideView>(entities[i]);
                    continue;
                }
                if (slots[i].Slot == TycoonCompany.ActiveSlot) { chosen = slots[i]; chosenFound = true; }
                if (!found || slots[i].Day > best.Day || (slots[i].Day == best.Day && slots[i].Slot < best.Slot))
                {
                    best = slots[i];
                    found = true;
                }
            }
            if (!found) return;
            if (chosenFound) best = chosen;

            SSelectedLocation selected = GetSingleton<SSelectedLocation>();
            if (selected.Valid && (int)selected.Selected.State == STATE_SAVED) return;
            Set(new SSelectedLocation { Valid = true, Selected = best });
            if (TycoonCompany.ActiveSlot != best.Slot) { TycoonCompany.ActiveSlot = best.Slot; TycoonCompany.Save(); }
            Mod.LogInfo($"HQ: the loading bay goes to the saved restaurant in slot {best.Slot} (day {best.Day}).");
        }
    }
}
