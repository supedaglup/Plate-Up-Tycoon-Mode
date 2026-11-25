using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Logs the menu at opening.</summary>
    public class TycoonEnsureMenuSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _menuItems;
        private EntityQuery _mains;
        private EntityQuery _starters;
        private EntityQuery _desserts;
        private EntityQuery _pendingDishes;

        protected override void Initialise()
        {
            base.Initialise();
            _menuItems = GetEntityQuery(new QueryHelper().All(typeof(CMenuItem)));
            _mains = GetEntityQuery(new QueryHelper().All(typeof(CMenuItem), typeof(CMenuItemMain)));
            _starters = GetEntityQuery(new QueryHelper().All(typeof(CMenuItem), typeof(CMenuItemStarter)));
            _desserts = GetEntityQuery(new QueryHelper().All(typeof(CMenuItem), typeof(CMenuItemDessert)));
            _pendingDishes = GetEntityQuery(new QueryHelper().All(typeof(CNewDish)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SIsDayFirstUpdate>();
        }

        protected override void OnUpdate()
        {
            int total = _menuItems.CalculateEntityCount();
            Mod.LogInfo($"Menu at opening: {total} item(s) (starters {_starters.CalculateEntityCount()}, mains {_mains.CalculateEntityCount()}, desserts {_desserts.CalculateEntityCount()}).");
            if (total == 0 && _pendingDishes.IsEmpty) Mod.LogWarning("Menu was EMPTY at opening: customers will leave without ordering.");
        }
    }

    /// <summary>Logs each customer group's journey, to see where customers give up.</summary>
    public class TycoonCustomerDiagnosticsSystem : RestaurantSystem, IModSystem
    {
        public const int MAX_LINES_PER_DAY = 400;

        private EntityQuery _groups;
        private EntityQuery _menuItems;
        private readonly Dictionary<Entity, string> _lastState = new Dictionary<Entity, string>();
        private readonly HashSet<Entity> _seen = new HashSet<Entity>();
        private readonly List<Entity> _gone = new List<Entity>();
        private int _linesToday;

        protected override void Initialise()
        {
            base.Initialise();
            _groups = GetEntityQuery(new QueryHelper().All(typeof(CCustomerGroup)));
            _menuItems = GetEntityQuery(new QueryHelper().All(typeof(CMenuItem)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (HasSingleton<SIsDayFirstUpdate>()) _linesToday = 0;

            _seen.Clear();
            using (NativeArray<Entity> groups = _groups.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < groups.Length; i++)
                {
                    Entity group = groups[i];
                    _seen.Add(group);
                    string state = Describe(group);
                    if (_lastState.TryGetValue(group, out string last) && last == state) continue;
                    _lastState[group] = state;
                    Write($"Customer {group.Index}: {(last == null ? "arrived, " : last + " -> ")}{state}");
                }
            }

            _gone.Clear();
            foreach (Entity e in _lastState.Keys) if (!_seen.Contains(e)) _gone.Add(e);
            foreach (Entity e in _gone)
            {
                _lastState.Remove(e);
            }
        }

        private void Write(string line)
        {
            if (_linesToday >= MAX_LINES_PER_DAY) return;
            _linesToday++;
            Mod.LogInfo(_linesToday == MAX_LINES_PER_DAY ? line + " (customer log full for today)" : line);
        }

        private string Describe(Entity g)
        {
            string where = "";
            if (Require(g, out CAssignedTable table))
                where = TycoonTakeOutCounterSystem.IsOrderingCounterSet(EntityManager, table.Table) ? " (Ordering Counter)" : " (table)";

            if (Has<CGroupLeaving>(g)) return "leaving";
            if (Has<CGroupStartLeaving>(g)) return "starts leaving" + where;
            if (Has<CGroupEating>(g)) return "eating" + where;
            if (Has<CGroupAwaitingOrder>(g)) return "waiting for their food" + where;
            if (Has<CGroupReadyToOrder>(g)) return "ready to order" + where;
            if (Has<CGroupChoosingOrder>(g)) return $"choosing{where}, menu has {_menuItems.CalculateEntityCount()} item(s)";
            if (Has<CGroupGoingToTable>(g)) return "going to table" + where;
            if (Has<CQueuePosition>(g)) return "in the queue";
            if (Has<CGroupWaitingForTable>(g)) return "waiting for a table";
            return "walking";
        }
    }
}
