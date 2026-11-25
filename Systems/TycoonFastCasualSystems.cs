using HarmonyLib;
using Kitchen;
using Kitchen.Layouts;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Fast Casual Dining: the Take-Out card's path card.</summary>
    public static class TycoonFastCasual
    {
        public const int STATE_NEEDS_TABLE = 0, STATE_WALKING = 1, STATE_SEATED = 2, STATE_EATING = 3, STATE_DONE = 4, STATE_LEFT = 9;

        internal static int NextPartyID;

        internal static bool IsFastCasual(EntityManager em, Entity group) =>
            em.HasComponent<CTycoonParty>(group) && em.GetComponentData<CTycoonParty>(group).FastCasual;

        internal static int CoffeeTableID => KitchenLib.References.ApplianceReferences.CoffeeTable;

        internal static bool On(EntityManager em)
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(STycoonFastCasualOn));
            return !q.IsEmpty;
        }
    }

    /// <summary>Host side, Fast Casual Dining.</summary>
    public class TycoonFastCasualSystem : RestaurantSystem, IModSystem
    {
        public const float WALK_TIMEOUT = 15f;
        public const float ARRIVED_DISTANCE = 0.45f;
        public const float BAR_HEIGHT = 0.8f;

        private EntityQuery _parties;
        private EntityQuery _tableSets;
        private EntityQuery _bars;
        private EntityQuery _food;
        private readonly Dictionary<Entity, Entity> _barOf = new Dictionary<Entity, Entity>();
        private readonly HashSet<Vector3> _waitSpotsTaken = new HashSet<Vector3>();

        protected override void Initialise()
        {
            base.Initialise();
            _parties = GetEntityQuery(new QueryHelper().All(typeof(CCustomerGroup), typeof(CTycoonParty), typeof(CGroupMember)));
            _tableSets = GetEntityQuery(new QueryHelper().All(typeof(CTableSet), typeof(CTablePlace), typeof(CTableSetParts)));
            _bars = GetEntityQuery(new QueryHelper().All(typeof(CTycoonFastCasualBar)));
            _food = GetEntityQuery(new QueryHelper().All(typeof(CTycoonFastCasualFood)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            KeepOnMarker();

            var live = new List<Entity>();
            using (NativeArray<Entity> groups = _parties.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < groups.Length; i++)
                    if (TycoonFastCasual.IsFastCasual(EntityManager, groups[i])) live.Add(groups[i]);

            if (live.Count == 0) _waitSpotsTaken.Clear();
            var waiting = new HashSet<Entity>();
            var bars = new Dictionary<Entity, (float progress, bool bad)>();
            float now = Time.TotalTime;
            foreach (Entity g in live)
            {
                if (!EntityManager.Exists(g)) continue;
                if (!Has<CTycoonFastCasual>(g))
                {
                    if (Has<CGroupEating>(g) && Require(g, out CAssignedTable at) && TycoonTakeOutCounterSystem.IsOrderingCounterSet(EntityManager, at.Table))
                        PickUp(g, at.Table, now);
                    continue;
                }
                CTycoonFastCasual fc = EntityManager.GetComponentData<CTycoonFastCasual>(g);
                switch (fc.State)
                {
                    case TycoonFastCasual.STATE_NEEDS_TABLE:
                        if (Has<CGroupStartLeaving>(g) || Has<CGroupLeaving>(g)) break;
                        if (!FindTableFor(g, ref fc, now)) { WaitForTable(g, ref fc); waiting.Add(g); PatienceBar(g, bars); }
                        EntityManager.SetComponentData(g, fc);
                        break;
                    case TycoonFastCasual.STATE_WALKING:
                        Walk(g, ref fc, now);
                        EntityManager.SetComponentData(g, fc);
                        break;
                    case TycoonFastCasual.STATE_SEATED:
                        TryStartMeal(g, fc, now);
                        break;
                    case TycoonFastCasual.STATE_EATING:
                        Eat(g, ref fc, bars);
                        EntityManager.SetComponentData(g, fc);
                        break;
                    case TycoonFastCasual.STATE_DONE:
                        if (!Has<CGroupStartLeaving>(g) && !Has<CGroupLeaving>(g)) WaitForFriends(g, fc, now);
                        break;
                }
            }
            UpdateBars(bars);
            SweepFood();
        }

        private void ShowFood(Entity g, CTycoonFastCasual fc)
        {
            try
            {
                ClearFood(g);
                if (fc.Table == Entity.Null || !EntityManager.Exists(fc.Table) || !EntityManager.HasComponent<CTablePlace>(fc.Table)) return;
                if (!EntityManager.HasComponent<CWaitingForItem>(g)) return;
                DynamicBuffer<CTablePlace> places = EntityManager.GetBuffer<CTablePlace>(fc.Table);
                if (fc.Seat < 0 || fc.Seat >= places.Length) return;
                CTablePlace place = places[fc.Seat];
                Vector3 tablePosition = Require(fc.Table, out CPosition tablePos) ? tablePos.Position : place.SeatPosition.Position;

                var shown = new List<CDisplayedItem>();
                DynamicBuffer<CWaitingForItem> orders = EntityManager.GetBuffer<CWaitingForItem>(g);
                for (int i = 0; i < orders.Length; i++)
                {
                    CWaitingForItem w = orders[i];
                    if (!w.Satisfied || w.ItemID == 0) continue;
                    CItem item = w.Item != Entity.Null && EntityManager.Exists(w.Item) && EntityManager.HasComponent<CItem>(w.Item)
                        ? EntityManager.GetComponentData<CItem>(w.Item) : new CItem { ID = w.ItemID };
                    Entity copy = EntityManager.CreateEntity(typeof(CItem), typeof(CTycoonFastCasualFood), typeof(CDoNotPersist));
                    EntityManager.SetComponentData(copy, item);
                    EntityManager.SetComponentData(copy, new CTycoonFastCasualFood { Group = g });
                    shown.Add(new CDisplayedItem
                    {
                        IsComplete = true,
                        Item = copy,
                        ItemID = w.ItemID,
                        SeatPosition = place.SeatPosition.Position,
                        TablePosition = place.TablePosition,
                        IsSide = w.IsSide,
                    });
                }
                if (shown.Count == 0) return;

                Entity display = EntityManager.CreateEntity(typeof(CTycoonFastCasualFood), typeof(CDoNotPersist), typeof(CPosition), typeof(CRequiresView), typeof(CInterfaceOf));
                Entity anchor = EntityManager.CreateEntity(typeof(CTycoonFastCasualFood), typeof(CDoNotPersist), typeof(CHasItemCollectionIndicator));
                EntityManager.SetComponentData(display, new CTycoonFastCasualFood { Group = g });
                EntityManager.SetComponentData(anchor, new CTycoonFastCasualFood { Group = g });
                EntityManager.SetComponentData(anchor, new CHasItemCollectionIndicator { Indicator = display });
                EntityManager.SetComponentData(display, new CPosition(tablePosition));
                EntityManager.SetComponentData(display, new CRequiresView { Type = (ViewType)7 });
                EntityManager.SetComponentData(display, new CInterfaceOf { Entity = anchor });
                DynamicBuffer<CDisplayedItem> buffer = EntityManager.AddBuffer<CDisplayedItem>(display);
                foreach (CDisplayedItem d in shown) buffer.Add(d);
                if (_foodLogged++ < 5) Mod.LogInfo($"Fast Casual: customer {g.Index}'s food ({shown.Count} item(s)) is on table {fc.Table.Index}.");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Fast Casual: could not show the food on the table: " + e.Message);
            }
        }
        private int _foodLogged;

        private void ClearFood(Entity g)
        {
            if (_food.IsEmpty) return;
            using NativeArray<Entity> all = _food.ToEntityArray(Allocator.Temp);
            using NativeArray<CTycoonFastCasualFood> tags = _food.ToComponentDataArray<CTycoonFastCasualFood>(Allocator.Temp);
            for (int i = 0; i < all.Length; i++)
                if (tags[i].Group == g && EntityManager.Exists(all[i])) EntityManager.DestroyEntity(all[i]);
        }

        private void SweepFood()
        {
            if (_food.IsEmpty) return;
            using NativeArray<Entity> all = _food.ToEntityArray(Allocator.Temp);
            using NativeArray<CTycoonFastCasualFood> tags = _food.ToComponentDataArray<CTycoonFastCasualFood>(Allocator.Temp);
            for (int i = 0; i < all.Length; i++)
            {
                Entity g = tags[i].Group;
                bool eating = g != Entity.Null && EntityManager.Exists(g) && Has<CTycoonFastCasual>(g)
                    && EntityManager.GetComponentData<CTycoonFastCasual>(g).State == TycoonFastCasual.STATE_EATING;
                if (!eating && EntityManager.Exists(all[i])) EntityManager.DestroyEntity(all[i]);
            }
        }

        private void KeepOnMarker()
        {
            bool on = Mod.FastCasualCard != null && HasSingleton<STycoonTakeOutOn>() && TycoonCards.IsEnabled(EntityManager, Mod.FastCasualCard.ID);
            bool marked = HasSingleton<STycoonFastCasualOn>();
            if (on && !marked) { Set(new STycoonFastCasualOn()); Mod.LogInfo("Fast Casual Dining: on (some customers eat at a table after ordering at the counter)."); }
            else if (!on && marked) { EntityManager.DestroyEntity(GetSingletonEntity<STycoonFastCasualOn>()); Mod.LogInfo("Fast Casual Dining: off."); }
        }

        private void PickUp(Entity g, Entity counterSet, float now)
        {
            var fc = new CTycoonFastCasual { State = TycoonFastCasual.STATE_NEEDS_TABLE, Seat = -1, StateStarted = now, CoffeeOnly = true };
            var dirt = new List<int>();
            bool dessertCourse = Require(g, out CGroupMealPhase phase) && phase.Phase == MenuPhase.Dessert;
            if (EntityManager.HasComponent<CWaitingForItem>(g))
            {
                DynamicBuffer<CWaitingForItem> orders = EntityManager.GetBuffer<CWaitingForItem>(g);
                for (int i = 0; i < orders.Length; i++)
                {
                    CWaitingForItem w = orders[i];
                    if (!dessertCourse && !CoffeeTableDish(w.SourceMenuItem)) fc.CoffeeOnly = false;
                    if (!w.Satisfied) continue;
                    if (GameData.Main.TryGet<Item>(w.ItemID, out Item item, false) && item.DirtiesTo != null) dirt.Add(item.DirtiesTo.ID);
                }
            }
            var carried = new List<int>();
            if (counterSet != Entity.Null && EntityManager.Exists(counterSet) && EntityManager.HasComponent<CDirtItem>(counterSet))
            {
                DynamicBuffer<CDirtItem> counterDirt = EntityManager.GetBuffer<CDirtItem>(counterSet);
                foreach (int id in dirt)
                    for (int k = counterDirt.Length - 1; k >= 0; k--)
                        if (counterDirt[k].ID == id) { counterDirt.RemoveAt(k); carried.Add(id); break; }
            }
            if (carried.Count > 0) fc.Dirt1 = carried[0];
            if (carried.Count > 1) fc.Dirt2 = carried[1];
            if (carried.Count > 2) fc.Dirt3 = carried[2];

            EntityManager.RemoveComponent<CGroupEating>(g);
            if (counterSet != Entity.Null && EntityManager.Exists(counterSet) && Require(counterSet, out COccupiedByGroup occupied) && occupied.Group == g)
                EntityManager.RemoveComponent<COccupiedByGroup>(counterSet);
            EntityManager.RemoveComponent<CAssignedTable>(g);
            ForMembers(g, m => { if (!Has<CCustomerHasLeftoversBag>(m)) EntityManager.AddComponent<CCustomerHasLeftoversBag>(m); });
            EntityManager.AddComponentData(g, fc);
            CTycoonParty party = EntityManager.GetComponentData<CTycoonParty>(g);
            Mod.LogInfo($"Fast Casual: customer {g.Index} (party {party.PartyID} of {party.Size}) picked up their order{(fc.CoffeeOnly ? " (coffee or dessert: a Coffee Table will do)" : "")} and looks for a table.");
        }

        private static bool CoffeeTableDish(int dishID)
        {
            if (Patches.TycoonServeReputationPatch.IsCoffeeFamily(dishID)) return true;
            return dishID != 0 && GameData.Main.TryGet<Dish>(dishID, out Dish dish, false) && dish.Type == DishType.Dessert;
        }

        private bool FindTableFor(Entity g, ref CTycoonFastCasual fc, float now)
        {
            int partyID = EntityManager.GetComponentData<CTycoonParty>(g).PartyID;
            List<Entity> friends = Party(partyID);
            Entity table = Entity.Null;
            int seat = -1;
            int withoutTable = 0;
            foreach (Entity f in friends)
            {
                CTycoonFastCasual other = Has<CTycoonFastCasual>(f) ? EntityManager.GetComponentData<CTycoonFastCasual>(f) : default;
                if (f == g || !Has<CTycoonFastCasual>(f) || other.Table == Entity.Null || !EntityManager.Exists(other.Table)) { withoutTable++; continue; }
                if (table == Entity.Null || other.Table != table)
                {
                    int free = FreeSeat(other.Table, partyID, g);
                    if (free >= 0) { table = other.Table; seat = free; break; }
                }
            }
            bool coffee = true;
            foreach (Entity f in friends) if (Has<CTycoonFastCasual>(f) && !EntityManager.GetComponentData<CTycoonFastCasual>(f).CoffeeOnly) coffee = false;
            if (table == Entity.Null) table = FreeTable(Mathf.Max(1, withoutTable), coffee);
            if (table == Entity.Null && withoutTable > 1) table = FreeTable(1, coffee, biggest: true);
            if (table == Entity.Null) return false;

            if (seat < 0) seat = FreeSeat(table, partyID, g);
            if (seat < 0) return false;
            fc.Table = table;
            fc.Seat = seat;
            fc.State = TycoonFastCasual.STATE_WALKING;
            fc.StateStarted = now;
            _waitSpotsTaken.Remove(fc.WaitSpot);
            SendToSeat(g, table, seat);
            Mod.LogInfo($"Fast Casual: customer {g.Index} (party {partyID}, {friends.Count} still around) goes to place {seat + 1} of table {table.Index}.");
            return true;
        }

        private List<Entity> Party(int partyID)
        {
            var list = new List<Entity>();
            using NativeArray<Entity> groups = _parties.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                Entity f = groups[i];
                if (EntityManager.GetComponentData<CTycoonParty>(f).PartyID != partyID) continue;
                if (Has<CGroupStartLeaving>(f) || Has<CGroupLeaving>(f)) continue;
                if (Has<CTycoonFastCasual>(f) && EntityManager.GetComponentData<CTycoonFastCasual>(f).State == TycoonFastCasual.STATE_LEFT) continue;
                list.Add(f);
            }
            return list;
        }

        private Entity FreeTable(int seatsNeeded, bool coffee, bool biggest = false)
        {
            var kept = new HashSet<Entity>();
            using (NativeArray<Entity> groups = _parties.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < groups.Length; i++)
                    if (Has<CTycoonFastCasual>(groups[i])) kept.Add(EntityManager.GetComponentData<CTycoonFastCasual>(groups[i]).Table);

            Entity best = Entity.Null;
            int bestSeats = int.MaxValue;
            using NativeArray<Entity> sets = _tableSets.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < sets.Length; i++)
            {
                Entity set = sets[i];
                if (kept.Contains(set) || Has<COccupiedByGroup>(set) || !Has<CTableReadyForCustomers>(set)) continue;
                if (TycoonTakeOutCounterSystem.IsOrderingCounterSet(EntityManager, set)) continue;
                bool coffeeTable = HasPart(set, TycoonFastCasual.CoffeeTableID);
                if (coffeeTable ? !coffee : EntityManager.GetComponentData<CTableSet>(set).IsWaitingTable) continue;
                int seats = EntityManager.GetBuffer<CTablePlace>(set).Length;
                if (seats < seatsNeeded) continue;
                if (biggest ? (best != Entity.Null && seats <= -bestSeats) : seats >= bestSeats) continue;
                best = set;
                bestSeats = biggest ? -seats : seats;
            }
            return best;
        }

        private bool HasPart(Entity set, int applianceID)
        {
            DynamicBuffer<CTableSetParts> parts = EntityManager.GetBuffer<CTableSetParts>(set);
            for (int p = 0; p < parts.Length; p++)
            {
                Entity part = parts[p];
                if (EntityManager.Exists(part) && Require(part, out CAppliance a) && a.ID == applianceID) return true;
            }
            return false;
        }

        private int FreeSeat(Entity table, int partyID, Entity self)
        {
            int seats = EntityManager.GetBuffer<CTablePlace>(table).Length;
            var used = new HashSet<int>();
            foreach (Entity f in Party(partyID))
            {
                if (f == self || !Has<CTycoonFastCasual>(f)) continue;
                CTycoonFastCasual other = EntityManager.GetComponentData<CTycoonFastCasual>(f);
                if (other.Table == table && other.Seat >= 0) used.Add(other.Seat);
            }
            for (int s = 0; s < seats; s++) if (!used.Contains(s)) return s;
            return -1;
        }

        private void SendToSeat(Entity g, Entity table, int seat)
        {
            DynamicBuffer<CTablePlace> places = EntityManager.GetBuffer<CTablePlace>(table);
            if (seat < 0 || seat >= places.Length) return;
            CTablePlace place = places[seat];
            CPosition at = place.SeatPosition;
            var move = new CMoveToLocation
            {
                Location = at.Position + at.Forward(0.1f),
                DesiredFacing = at.Position + at.Forward(1f),
                StoppingDistance = 0f,
                Chair = place.Chair,
            };
            ForMembers(g, m =>
            {
                if (Has<CMoveToLocation>(m)) EntityManager.SetComponentData(m, move);
                else EntityManager.AddComponentData(m, move);
            });
        }

        private void Walk(Entity g, ref CTycoonFastCasual fc, float now)
        {
            if (fc.Table == Entity.Null || !EntityManager.Exists(fc.Table) || !EntityManager.HasComponent<CTablePlace>(fc.Table))
            {
                fc.State = TycoonFastCasual.STATE_NEEDS_TABLE;
                fc.Table = Entity.Null;
                fc.Seat = -1;
                return;
            }
            DynamicBuffer<CTablePlace> places = EntityManager.GetBuffer<CTablePlace>(fc.Table);
            if (fc.Seat >= places.Length) { fc.State = TycoonFastCasual.STATE_NEEDS_TABLE; fc.Table = Entity.Null; fc.Seat = -1; return; }
            CPosition seat = places[fc.Seat].SeatPosition;
            Vector3 target = seat.Position + seat.Forward(0.1f);
            Entity member = FirstMember(g);
            bool arrived = member != Entity.Null && Require(member, out CPosition pos) && Flat(pos.Position - target) < ARRIVED_DISTANCE;
            if (!arrived && now - fc.StateStarted < WALK_TIMEOUT) return;
            fc.State = TycoonFastCasual.STATE_SEATED;
            fc.StateStarted = now;
            ForMembers(g, m => { if (Has<CCustomerHasLeftoversBag>(m)) EntityManager.RemoveComponent<CCustomerHasLeftoversBag>(m); });
            Mod.LogInfo($"Fast Casual: customer {g.Index} sat down at table {fc.Table.Index}{(arrived ? "" : " (took too long walking)")}.");
        }

        private void WaitForTable(Entity g, ref CTycoonFastCasual fc)
        {
            if (fc.WaitSpot == Vector3.zero)
            {
                fc.WaitSpot = WaitSpot(g);
                if (fc.WaitSpot != Vector3.zero)
                {
                    _waitSpotsTaken.Add(fc.WaitSpot);
                    var move = new CMoveToLocation { Location = fc.WaitSpot, DesiredFacing = fc.WaitSpot + new Vector3(0f, 0f, 1f), StoppingDistance = 0.2f };
                    ForMembers(g, m =>
                    {
                        if (Has<CMoveToLocation>(m)) EntityManager.SetComponentData(m, move);
                        else EntityManager.AddComponentData(m, move);
                    });
                    Mod.LogInfo($"Fast Casual: no free table for customer {g.Index}; waiting to the side at ({fc.WaitSpot.x:0}, {fc.WaitSpot.z:0}).");
                }
            }

            CTycoonCarriedPatience pool = Has<CTycoonCarriedPatience>(g)
                ? EntityManager.GetComponentData<CTycoonCarriedPatience>(g)
                : new CTycoonCarriedPatience { Remaining = TycoonQueuePatienceSystem.DEFAULT_SECONDS, Total = TycoonQueuePatienceSystem.DEFAULT_SECONDS };
            pool.Remaining -= Time.DeltaTime;
            if (Has<CTycoonCarriedPatience>(g)) EntityManager.SetComponentData(g, pool);
            else EntityManager.AddComponentData(g, pool);
            if (pool.Remaining > 0f) return;

            fc.State = TycoonFastCasual.STATE_LEFT;
            _waitSpotsTaken.Remove(fc.WaitSpot);
            EntityManager.AddComponent<CGroupStartLeaving>(g);
            if (HasSingleton<STycoonReputation>())
            {
                STycoonReputation rep = GetSingleton<STycoonReputation>();
                TycoonReputation.Add(ref rep, -TycoonReputation.FAST_CASUAL_WALKOUT_PENALTY);
                TycoonReputationReport.Note("Fast Casual guests who found no table", -TycoonReputation.FAST_CASUAL_WALKOUT_PENALTY);
                Set(rep);
                TycoonReputation.Current = rep;
            }
            if (HasSingleton<STycoonEconomy>())
            {
                STycoonEconomy economy = GetSingleton<STycoonEconomy>();
                economy.WalkoutsToday++;
                economy.WalkoutsTotal++;
                Set(economy);
            }
            Mod.LogInfo($"Fast Casual: customer {g.Index} gave up waiting for a table and left with their order (-{TycoonReputation.FAST_CASUAL_WALKOUT_PENALTY:0.#} reputation, no money lost).");
        }

        private Vector3 WaitSpot(Entity g)
        {
            TileManager tiles = TileManager;
            Entity member = FirstMember(g);
            if (tiles == null || member == Entity.Null || !Require(member, out CPosition from)) return Vector3.zero;
            Vector3 door = GetFrontDoor(false);
            int doorRoom = tiles.GetTile(new IntVector3(Mathf.RoundToInt(door.x), 0, Mathf.RoundToInt(door.z))).RoomID;
            var line = new HashSet<(int, int)>();
            foreach (Vector3 p in TycoonCounterQueueSystem.LineTiles) line.Add((Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)));

            Vector3 best = Vector3.zero;
            float bestScore = float.MaxValue;
            Bounds b = Bounds;
            for (int x = Mathf.FloorToInt(b.min.x); x <= Mathf.CeilToInt(b.max.x); x++)
                for (int z = Mathf.FloorToInt(b.min.z); z <= Mathf.CeilToInt(b.max.z); z++)
                {
                    CLayoutRoomTile t = tiles.GetTile(new IntVector3(x, 0, z));
                    if (t.Type == RoomType.NoRoom) continue;
                    if (t.RoomID != doorRoom && t.Type != RoomType.Dining && t.Type != RoomType.FrontEntrance) continue;
                    if (line.Contains((x, z))) continue;
                    if (Mathf.Abs(x - door.x) + Mathf.Abs(z - door.z) < 1.5f) continue;
                    if (tiles.GetOccupant(new IntVector3(x, 0, z), OccupancyLayer.Default) != Entity.Null) continue;
                    var spot = new Vector3(x, from.Position.y, z);
                    if (_waitSpotsTaken.Contains(spot)) continue;
                    float score = Flat(spot - from.Position);
                    if (score < bestScore) { bestScore = score; best = spot; }
                }
            if (best != Vector3.zero) return best;

            Vector3 doorOutside = GetFrontDoor(true);
            Vector3 outward = doorOutside - door; outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) outward = new Vector3(0f, 0f, -1f);
            outward.Normalize();
            for (int k = 1; k < 20; k++)
            {
                Vector3 spot = doorOutside + new Vector3(-1f, 0f, 0f) * k + outward * 0.5f;
                spot.y = from.Position.y;
                if (!_waitSpotsTaken.Contains(spot)) return spot;
            }
            return Vector3.zero;
        }

        private void PatienceBar(Entity g, Dictionary<Entity, (float, bool)> bars)
        {
            if (!Require(g, out CTycoonCarriedPatience pool)) return;
            float left = pool.Total > 0f ? Mathf.Clamp01(pool.Remaining / pool.Total) : 1f;
            bars[g] = (left, left < TycoonQueuePatienceBarsSystem.WARNING_BELOW);
        }

        private void UpdateBars(Dictionary<Entity, (float progress, bool bad)> wanted)
        {
            _barOf.Clear();
            using (NativeArray<Entity> bars = _bars.ToEntityArray(Allocator.Temp))
            using (NativeArray<CTycoonFastCasualBar> data = _bars.ToComponentDataArray<CTycoonFastCasualBar>(Allocator.Temp))
                for (int i = 0; i < bars.Length; i++)
                {
                    if (wanted.ContainsKey(data[i].Group) && !_barOf.ContainsKey(data[i].Group)) _barOf[data[i].Group] = bars[i];
                    else EntityManager.DestroyEntity(bars[i]);
                }
            foreach (KeyValuePair<Entity, (float progress, bool bad)> w in wanted)
            {
                Entity g = w.Key;
                Entity member = FirstMember(g);
                if (member == Entity.Null || !Require(member, out CPosition head)) continue;
                if (!_barOf.TryGetValue(g, out Entity bar))
                {
                    bar = EntityManager.CreateEntity(typeof(CTycoonFastCasualBar), typeof(CRequiresView), typeof(CPosition), typeof(CProgressIndicator), typeof(CDoNotPersist));
                    EntityManager.SetComponentData(bar, new CTycoonFastCasualBar { Group = g });
                    EntityManager.SetComponentData(bar, new CRequiresView { Type = ViewType.ProgressView });
                }
                EntityManager.SetComponentData(bar, new CPosition(head.Position + new Vector3(0f, BAR_HEIGHT, 0f)));
                EntityManager.SetComponentData(bar, new CProgressIndicator { Progress = w.Value.progress, IsBad = w.Value.bad, Process = Patches.PatienceBarViewPatch.PATIENCE_BAR_PROCESS });
            }
        }

        private void TryStartMeal(Entity g, CTycoonFastCasual mine, float now)
        {
            int partyID = EntityManager.GetComponentData<CTycoonParty>(g).PartyID;
            List<Entity> friends = Party(partyID);
            var atTable = new List<Entity>();
            foreach (Entity f in friends)
            {
                if (!Has<CTycoonFastCasual>(f)) continue;
                CTycoonFastCasual other = EntityManager.GetComponentData<CTycoonFastCasual>(f);
                if (other.Table != mine.Table) continue;
                if (other.State == TycoonFastCasual.STATE_WALKING) return;
                if (other.State == TycoonFastCasual.STATE_SEATED) atTable.Add(f);
            }
            friends = atTable;
            if (!EntityManager.Exists(mine.Table))
            {
                foreach (Entity f in friends) Reset(f, now);
                return;
            }
            foreach (Entity f in friends) StartMeal(f);
            Mod.LogInfo($"Fast Casual: party at table {mine.Table.Index} ({friends.Count} customer(s)) starts eating.");
        }

        private void Reset(Entity f, float now)
        {
            CTycoonFastCasual fc = EntityManager.GetComponentData<CTycoonFastCasual>(f);
            fc.State = TycoonFastCasual.STATE_NEEDS_TABLE;
            fc.Table = Entity.Null;
            fc.Seat = -1;
            fc.StateStarted = now;
            EntityManager.SetComponentData(f, fc);
        }

        private void StartMeal(Entity g)
        {
            CTycoonFastCasual fc = EntityManager.GetComponentData<CTycoonFastCasual>(g);
            fc.State = TycoonFastCasual.STATE_EATING;
            EntityManager.SetComponentData(g, fc);
            float seconds = EatingSeconds(g);
            var meal = new CTycoonFastCasualMeal { Remaining = seconds, Total = seconds };
            if (Has<CTycoonFastCasualMeal>(g)) EntityManager.SetComponentData(g, meal);
            else EntityManager.AddComponentData(g, meal);
            ShowFood(g, fc);
        }

        private void Eat(Entity g, ref CTycoonFastCasual fc, Dictionary<Entity, (float, bool)> bars)
        {
            if (!Require(g, out CTycoonFastCasualMeal meal)) { StartMeal(g); return; }
            meal.Remaining -= Time.DeltaTime;
            EntityManager.SetComponentData(g, meal);
            bars[g] = (meal.Total > 0f ? Mathf.Clamp01(1f - meal.Remaining / meal.Total) : 1f, false);
            if (meal.Remaining > 0f) return;
            FinishMeal(g, ref fc);
        }

        private void FinishMeal(Entity g, ref CTycoonFastCasual fc)
        {
            fc.State = TycoonFastCasual.STATE_DONE;
            ClearFood(g);
            Entity table = fc.Table;
            if (table != Entity.Null && EntityManager.Exists(table) && EntityManager.HasComponent<CDirtItem>(table))
            {
                DynamicBuffer<CDirtItem> dirt = EntityManager.GetBuffer<CDirtItem>(table);
                if (fc.Dirt1 != 0) dirt.Add(new CDirtItem { ID = fc.Dirt1 });
                if (fc.Dirt2 != 0) dirt.Add(new CDirtItem { ID = fc.Dirt2 });
                if (fc.Dirt3 != 0) dirt.Add(new CDirtItem { ID = fc.Dirt3 });
                if (!Has<CTableSpawnDirt>(table)) EntityManager.AddComponentData(table, new CTableSpawnDirt());
            }
            Entity member = FirstMember(g);
            if (member != Entity.Null && Require(member, out CPosition at) && Require(g, out CCustomerSettings settings)
                && !settings.Ordering.PreventMess && UnityEngine.Random.value < Mathf.Clamp01(TycoonEconomy.FAST_CASUAL_MESS_CHANCE * Mathf.Max(0f, settings.Ordering.MessFactor)))
            {
                Entity request = EntityManager.CreateEntity(typeof(CPosition), typeof(CMessRequest));
                EntityManager.SetComponentData(request, new CPosition(at.Position));
                EntityManager.SetComponentData(request, new CMessRequest { ID = AssetReference.CustomerMess });
            }
            if (!fc.Tipped) { fc.Tipped = true; Tip(g); }
            fc.StateStarted = Time.TotalTime;
        }

        private void WaitForFriends(Entity g, CTycoonFastCasual mine, float now)
        {
            bool wait = false;
            if (now - mine.StateStarted < TycoonEconomy.FAST_CASUAL_FRIEND_WAIT)
            {
                int partyID = EntityManager.GetComponentData<CTycoonParty>(g).PartyID;
                foreach (Entity f in Party(partyID))
                {
                    if (f == g) continue;
                    if (!Has<CTycoonFastCasual>(f)) { wait = true; break; }
                    CTycoonFastCasual other = EntityManager.GetComponentData<CTycoonFastCasual>(f);
                    if (other.State == TycoonFastCasual.STATE_NEEDS_TABLE) { wait = true; break; }
                    if (other.Table != mine.Table) continue;
                    if (other.State == TycoonFastCasual.STATE_WALKING || other.State == TycoonFastCasual.STATE_SEATED || other.State == TycoonFastCasual.STATE_EATING) { wait = true; break; }
                }
            }
            if (wait) return;
            EntityManager.AddComponent<CGroupStartLeaving>(g);
            Mod.LogInfo($"Fast Casual: customer {g.Index} leaves table {mine.Table.Index}{(now - mine.StateStarted >= TycoonEconomy.FAST_CASUAL_FRIEND_WAIT ? " (waited long enough for their friends)" : "")}.");
        }

        private float EatingSeconds(Entity g)
        {
            float seconds = Require(g, out CCustomerSettings settings) ? settings.Patience.Eating : 0f;
            if (seconds <= 0f || seconds >= TycoonEconomy.FAST_CASUAL_COUNTER_HOLD) seconds = TycoonEconomy.FAST_CASUAL_DEFAULT_EATING;
            if (Require(g, out CEatingTimeFactor factor) && factor.Factor.Value > 0f) seconds *= factor.Factor.Value;
            return seconds;
        }

        private void Tip(Entity g)
        {
            CTycoonParty party = EntityManager.GetComponentData<CTycoonParty>(g);
            int people = Mathf.Max(1, EntityManager.GetBuffer<CGroupMember>(g).Length);
            int each = Mathf.Max(1, Mathf.RoundToInt((float)party.Paid / people));
            float level = TycoonReputation.Level(TycoonReputation.Current.Reputation, TycoonEconomy.CurrentStars);
            int total = 0;
            for (int i = 0; i < people; i++) total += TycoonReputation.FastCasualTip(party.Paid > 0 ? each : 0, level, Random);
            total = Mathf.RoundToInt(total * TycoonIngredientQuality.TipMultiplier(TycoonEconomy.CurrentStars));
            Mod.LogInfo($"Fast Casual: customer {g.Index} finished eating; tips {total} coin(s) (paid {party.Paid}).");
            if (total <= 0) return;

            if (HasSingleton<SMoney>())
            {
                SMoney money = GetSingleton<SMoney>();
                money.Amount += total;
                Set(money);
            }
            MoneyTracker.AddEvent(new EntityContext(EntityManager), Mod.Ledger.Tips, total);
            if (HasSingleton<STycoonReputation>())
            {
                STycoonReputation rep = GetSingleton<STycoonReputation>();
                rep.TipsToday += total;
                Set(rep);
                TycoonReputation.Current = rep;
            }
            Entity member = FirstMember(g);
            if (member != Entity.Null && Require(member, out CPosition at))
            {
                Entity popup = EntityManager.CreateEntity(typeof(CMoneyPopup), typeof(CPosition), typeof(CLifetime), typeof(CRequiresView));
                EntityManager.SetComponentData(popup, new CMoneyPopup { Change = total });
                EntityManager.SetComponentData(popup, new CPosition(at.Position));
                EntityManager.SetComponentData(popup, new CLifetime(1f));
                EntityManager.SetComponentData(popup, new CRequiresView { Type = ViewType.MoneyPopup });
            }
        }

        private static readonly System.Random Random = new System.Random();

        private Entity FirstMember(Entity g)
        {
            if (!EntityManager.HasComponent<CGroupMember>(g)) return Entity.Null;
            DynamicBuffer<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(g);
            return members.Length > 0 ? members[0].Customer : Entity.Null;
        }

        private void ForMembers(Entity g, System.Action<Entity> action)
        {
            if (!EntityManager.HasComponent<CGroupMember>(g)) return;
            var list = new List<Entity>();
            foreach (CGroupMember m in EntityManager.GetBuffer<CGroupMember>(g)) list.Add(m.Customer);
            foreach (Entity m in list) if (EntityManager.Exists(m)) action(m);
        }

        private static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }
    }

    /// <summary>Take-Out: customers queue and order one by one.</summary>
    [HarmonyPatch(typeof(CreateCustomerGroup), "NewGroup")]
    public static class TycoonPartySplitPatch
    {
        private static bool _inside;
        internal static bool Inside => _inside;
        private static int _logged;

        [HarmonyPrefix]
        private static bool Prefix(CreateCustomerGroup __instance, CustomerType type_data, ref int customer_count, bool is_cat, ref Entity __result)
        {
            if (_inside || !TycoonSession.IsActive) return true;
            if (is_cat)
            {
                try
                {
                    using EntityQuery takeOut = __instance.EntityManager.CreateEntityQuery(typeof(STycoonTakeOutOn));
                    if (!takeOut.IsEmpty && customer_count > 1) { Mod.LogInfo($"Take-Out: a cat group of {customer_count} comes as one cat."); customer_count = 1; }
                }
                catch (System.Exception e) { Mod.LogWarning("Cat group size: " + e.Message); }
                return true;
            }
            try
            {
                EntityManager em = __instance.EntityManager;
                using (EntityQuery takeOut = em.CreateEntityQuery(typeof(STycoonTakeOutOn))) if (takeOut.IsEmpty) return true;
                int size = Mathf.Max(1, customer_count);
                bool fast = TycoonFastCasual.On(em) && UnityEngine.Random.value < TycoonEconomy.FAST_CASUAL_SHARE;
                int party = ++TycoonFastCasual.NextPartyID;
                Entity first = Entity.Null;
                _inside = true;
                try
                {
                    for (int i = 0; i < size; i++)
                    {
                        Entity g = __instance.NewGroup(type_data, 1, false);
                        em.AddComponentData(g, new CTycoonParty { PartyID = party, Size = size, FastCasual = fast });
                        if (first == Entity.Null) first = g;
                    }
                }
                finally { _inside = false; }
                __result = first;
                if ((size > 1 || fast) && _logged < 60) { _logged++; Mod.LogInfo($"Take-Out: party {party} of {size} arrives{(size > 1 ? ", each queues alone" : "")}{(fast ? " (Fast Casual: they will eat at a table)" : "")}."); }
                return false;
            }
            catch (System.Exception e)
            {
                _inside = false;
                Mod.LogWarning("Party split: " + e.Message);
                return true;
            }
        }
    }

    /// <summary>Take-Out without Fast Casual: nobody sits down, so the table size doesn't matter.</summary>
    [HarmonyPatch(typeof(DetermineMaxTableSize), "OnUpdate")]
    public static class TycoonTakeOutTableSizePatch
    {
        private static int _logged = -1;

        [HarmonyPostfix]
        private static void Postfix(DetermineMaxTableSize __instance)
        {
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                using (EntityQuery takeOut = em.CreateEntityQuery(typeof(STycoonTakeOutOn))) if (takeOut.IsEmpty) return;
                if (TycoonFastCasual.On(em)) return;
                using (EntityQuery counters = em.CreateEntityQuery(typeof(CTycoonOrderingCounter))) if (counters.IsEmpty) return;
                using EntityQuery sizeQuery = em.CreateEntityQuery(typeof(SLargestTableSize));
                if (sizeQuery.IsEmpty) return;
                int largest = 1;
                using (EntityQuery parameters = em.CreateEntityQuery(typeof(SKitchenParameters)))
                    if (!parameters.IsEmpty) largest = Mathf.Max(largest, parameters.GetSingleton<SKitchenParameters>().Parameters.MaximumGroupSize);
                SLargestTableSize size = sizeQuery.GetSingleton<SLargestTableSize>();
                if (size.LargestTableSize >= largest) return;
                size.LargestTableSize = largest;
                sizeQuery.SetSingleton(size);
                if (_logged != largest) { _logged = largest; Mod.LogInfo($"Take-Out: no tables needed; groups of up to {largest} come in and queue one by one."); }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Take-Out table size: " + e.Message);
            }
        }
    }

    /// <summary>Take-Out: while it is on and an Ordering Counter exists, queued customers are only ever offered the counter.</summary>
    [HarmonyPatch(typeof(AssignmentSystem), "NewAssignment")]
    public static class TycoonCounterOnlyAssignmentPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(AssignmentSystem __instance, CAvailableAssignment assignment)
        {
            if (!(__instance is OpenTables) || !TycoonSession.IsActive) return true;
            try
            {
                EntityManager em = __instance.EntityManager;
                using (EntityQuery takeOut = em.CreateEntityQuery(typeof(STycoonTakeOutOn))) if (takeOut.IsEmpty) return true;
                using (EntityQuery counters = em.CreateEntityQuery(typeof(CTycoonOrderingCounter))) if (counters.IsEmpty) return true;
                return TycoonTakeOutCounterSystem.IsOrderingCounterSet(em, assignment.Entity);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Counter-only seating: " + e.Message);
                return true;
            }
        }
    }
}
