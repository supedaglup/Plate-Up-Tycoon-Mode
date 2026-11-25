using HarmonyLib;
using Kitchen;
using KitchenData;
using Kitchen.Layouts;
using KitchenMods;
using System.Collections.Generic;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Tycoon queues: where the line forms and who goes to the counter next.</summary>
    [UpdateAfter(typeof(UpdateCustomerStatesGroup))]
    public class TycoonCounterQueueSystem : RestaurantSystem, IModSystem
    {
        public static bool InCharge { get; private set; }

        public static readonly List<Vector3> LineTiles = new List<Vector3>();

        public const float OUTSIDE_SPACING = 1f;
        public const int MAX_SEARCH_TILES = 2000;

        private static readonly (int dx, int dz)[] Steps = { (0, 1), (1, 0), (0, -1), (-1, 0) };

        private EntityQuery _counters;
        private EntityQuery _chairs;
        private readonly List<Vector3> _inside = new List<Vector3>();
        private string _lastLineReport;

        protected override void Initialise()
        {
            base.Initialise();
            _counters = GetEntityQuery(new QueryHelper().All(typeof(CTycoonOrderingCounter), typeof(CPosition)));
            _chairs = GetEntityQuery(new QueryHelper().All(typeof(CApplianceChair), typeof(CPosition)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            InCharge = false;
            if (!HasSingleton<STycoonTakeOutOn>() || !HasSingleton<SQueueMarker>()) return;

            Entity marker = GetSingletonEntity<SQueueMarker>();
            DynamicBuffer<CQueue> queue = EntityManager.GetBuffer<CQueue>(marker);
            if (queue.IsEmpty) { InCharge = HasCounterChair(); return; }

            if (!BuildInsideLine(out Vector3 counterSpot)) return;
            InCharge = true;

            Vector3 doorOutside = GetFrontDoor(true);
            Vector3 doorInside = GetFrontDoor(false);
            Vector3 outward = doorOutside - doorInside; outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) outward = new Vector3(0f, 0f, -1f);
            outward.Normalize();
            Vector3 along = new Vector3(1f, 0f, 0f);
            Vector3 outsideStart = doorOutside + along * OUTSIDE_SPACING + outward * 0.5f;

            for (int i = queue.Length - 1; i >= 0; i--)
                if (!Has<CQueuePosition>(queue[i].Member)) queue.RemoveAt(i);

            var moves = new NativeList<Entity>(Allocator.Temp);
            var moveData = new NativeList<CMoveToLocation>(Allocator.Temp);
            Vector3 previous = counterSpot;
            for (int i = 0; i < queue.Length; i++)
            {
                Entity group = queue[i].Member;
                Vector3 spot = i < _inside.Count
                    ? _inside[i]
                    : outsideStart + along * (OUTSIDE_SPACING * (i - _inside.Count));
                Vector3 facing = previous;
                previous = spot;
                EntityManager.SetComponentData(group, new CQueuePosition { QueuePosition = i, Position = spot });

                if (!EntityManager.HasComponent<CGroupMember>(group)) continue;
                DynamicBuffer<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(group);
                for (int m = 0; m < members.Length; m++)
                {
                    Vector3 offset = Vector3.zero;
                    if (members.Length > 1)
                    {
                        float angle = 2f * Mathf.PI / members.Length * m;
                        offset = new Vector3(0.35f * Mathf.Sin(angle), 0f, 0.35f * Mathf.Cos(angle));
                    }
                    moves.Add(members[m]);
                    moveData.Add(new CMoveToLocation { Location = spot + offset, DesiredFacing = facing, StoppingDistance = 0.25f });
                }
            }
            for (int i = 0; i < moves.Length; i++)
            {
                if (!EntityManager.HasComponent<CMoveToLocation>(moves[i])) EntityManager.AddComponent<CMoveToLocation>(moves[i]);
                EntityManager.SetComponentData(moves[i], moveData[i]);
            }
            moves.Dispose();
            moveData.Dispose();
        }

        private bool HasCounterChair() => !_counters.IsEmpty && !_chairs.IsEmpty;

        private bool BuildInsideLine(out Vector3 counterSpot)
        {
            _inside.Clear();
            counterSpot = Vector3.zero;
            if (_counters.IsEmpty || _chairs.IsEmpty) return false;
            TileManager tiles = TileManager;
            if (tiles == null) return false;

            var counterChairs = new Dictionary<(int x, int z), Vector3>();
            using (NativeArray<CPosition> counters = _counters.ToComponentDataArray<CPosition>(Allocator.Temp))
            using (NativeArray<CPosition> chairs = _chairs.ToComponentDataArray<CPosition>(Allocator.Temp))
            {
                for (int k = 0; k < chairs.Length; k++)
                    for (int c = 0; c < counters.Length; c++)
                    {
                        Vector3 d = chairs[k].Position - counters[c].Position;
                        d.y = 0f;
                        if (d.sqrMagnitude < 0.5f || d.sqrMagnitude > 1.1f) continue;
                        counterChairs[(Mathf.RoundToInt(chairs[k].Position.x), Mathf.RoundToInt(chairs[k].Position.z))] = chairs[k].Position;
                        break;
                    }
            }
            if (counterChairs.Count == 0) return false;

            Vector3 doorPos = GetFrontDoor(false);
            var door = (x: Mathf.RoundToInt(doorPos.x), z: Mathf.RoundToInt(doorPos.z));
            int doorRoom = tiles.GetTile(new IntVector3(door.x, 0, door.z)).RoomID;

            Dictionary<(int x, int z), (int x, int z)> cameFrom = null;
            (int x, int z) chair = door;
            bool found = false, throughChairs = false;
            foreach (bool allowChairs in new[] { false, true })
            {
                cameFrom = Search(tiles, door, doorRoom, counterChairs, allowChairs, out chair, out found);
                if (found) { throughChairs = allowChairs; break; }
            }

            string report;
            LineTiles.Clear();
            if (found)
            {
                counterSpot = counterChairs[chair];
                for (var p = cameFrom[chair]; !p.Equals(door); p = cameFrom[p])
                    if (!IsChairTile(tiles, p)) _inside.Add(new Vector3(p.x, counterSpot.y, p.z));
                LineTiles.AddRange(_inside);
                LineTiles.Add(counterSpot);
                report = $"Queue: Ordering Counter spot at ({chair.x}, {chair.z}), {_inside.Count} place(s) in line inside{(throughChairs ? " (route passes chairs)" : "")}, then outside beside the door ({door.x}, {door.z}).";
            }
            else
            {
                counterSpot = doorPos;
                report = $"Queue: no route from the front door ({door.x}, {door.z}) to an Ordering Counter through the dining area; the line waits outside.";
                if (report != _lastLineReport) Mod.LogInfo("Queue map (D door, C counter spot, # appliance, h chair, . free, blank not customer area; top = +z):\n" + Map(tiles, door, doorRoom, counterChairs));
            }
            if (report != _lastLineReport) { _lastLineReport = report; Mod.LogInfo(report); }
            return true;
        }

        private Dictionary<(int x, int z), (int x, int z)> Search(TileManager tiles, (int x, int z) door, int doorRoom,
            Dictionary<(int x, int z), Vector3> counterChairs, bool allowChairs, out (int x, int z) chair, out bool found)
        {
            var cameFrom = new Dictionary<(int x, int z), (int x, int z)>();
            var open = new Queue<(int x, int z)>();
            cameFrom[door] = door;
            open.Enqueue(door);
            chair = door;
            found = false;
            while (open.Count > 0 && cameFrom.Count < MAX_SEARCH_TILES)
            {
                var here = open.Dequeue();
                if (counterChairs.ContainsKey(here)) { chair = here; found = true; break; }
                CLayoutRoomTile tile = tiles.GetTile(new IntVector3(here.x, 0, here.z));
                foreach ((int dx, int dz) in Steps)
                {
                    var next = (x: here.x + dx, z: here.z + dz);
                    if (cameFrom.ContainsKey(next)) continue;
                    if (!tile.Reachability[dx, dz]) continue;
                    if (!IsCustomerArea(tiles, next, doorRoom)) continue;
                    bool isCounterSpot = counterChairs.ContainsKey(next);
                    if (!isCounterSpot && tiles.GetOccupant(new IntVector3(next.x, 0, next.z), OccupancyLayer.Default) != Entity.Null
                        && !(allowChairs && IsChairTile(tiles, next))) continue;
                    cameFrom[next] = here;
                    open.Enqueue(next);
                }
            }
            return cameFrom;
        }

        private bool IsChairTile(TileManager tiles, (int x, int z) p)
        {
            Entity occupant = tiles.GetOccupant(new IntVector3(p.x, 0, p.z), OccupancyLayer.Default);
            return occupant != Entity.Null && EntityManager.Exists(occupant) && (Has<CApplianceChair>(occupant) || Has<CApplianceGhostChair>(occupant));
        }

        private string Map(TileManager tiles, (int x, int z) door, int doorRoom, Dictionary<(int x, int z), Vector3> counterChairs)
        {
            Bounds b = Bounds;
            var sb = new System.Text.StringBuilder();
            for (int z = Mathf.CeilToInt(b.max.z); z >= Mathf.FloorToInt(b.min.z); z--)
            {
                sb.Append($"{z,4} ");
                for (int x = Mathf.FloorToInt(b.min.x); x <= Mathf.CeilToInt(b.max.x); x++)
                {
                    var p = (x, z);
                    char c = ' ';
                    if (p.Equals(door)) c = 'D';
                    else if (counterChairs.ContainsKey(p)) c = 'C';
                    else if (IsCustomerArea(tiles, p, doorRoom))
                        c = tiles.GetOccupant(new IntVector3(x, 0, z), OccupancyLayer.Default) == Entity.Null ? '.' : IsChairTile(tiles, p) ? 'h' : '#';
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            sb.Append($"     x from {Mathf.FloorToInt(b.min.x)}");
            return sb.ToString();
        }

        private static bool IsCustomerArea(TileManager tiles, (int x, int z) p, int doorRoom)
        {
            CLayoutRoomTile tile = tiles.GetTile(new IntVector3(p.x, 0, p.z));
            if (tile.Type == RoomType.NoRoom) return false;
            return tile.RoomID == doorRoom || tile.Type == RoomType.Dining || tile.Type == RoomType.FrontEntrance;
        }
    }

    /// <summary>Per-group queue patience.</summary>
    public class TycoonQueuePatienceSystem : RestaurantSystem, IModSystem
    {
        public const float IMPATIENCE_PER_PLACE = 0f;
        public const float DEFAULT_SECONDS = 60f;

        private EntityQuery _queued;
        private EntityQuery _stale;

        protected override void Initialise()
        {
            base.Initialise();
            _queued = GetEntityQuery(new QueryHelper().All(typeof(CCustomerGroup), typeof(CQueuePosition)));
            _stale = GetEntityQuery(new QueryHelper().All(typeof(CTycoonQueuePatience)).None(typeof(CQueuePosition)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (!_stale.IsEmpty)
            {
                bool takeOut = Mod.TakeOutCard != null && TycoonCards.IsEnabled(EntityManager, Mod.TakeOutCard.ID);
                using NativeArray<Entity> stale = _stale.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < stale.Length; i++)
                {
                    Entity group = stale[i];
                    bool served = Has<CCustomerGroup>(group) && !Has<CGroupStartLeaving>(group) && !Has<CGroupLeaving>(group);
                    if (takeOut && served && !Has<CTycoonCarriedPatience>(group))
                    {
                        CTycoonQueuePatience left = EntityManager.GetComponentData<CTycoonQueuePatience>(group);
                        EntityManager.AddComponentData(group, new CTycoonCarriedPatience { Remaining = left.Remaining, Total = left.Total });
                    }
                    EntityManager.RemoveComponent<CTycoonQueuePatience>(group);
                }
            }
            if (!HasSingleton<SIsDayTime>() || _queued.IsEmpty) return;

            float total = GameData.Main?.Difficulty != null && GameData.Main.Difficulty.QueuePatienceTime > 0f
                ? GameData.Main.Difficulty.QueuePatienceTime : DEFAULT_SECONDS;
            float dt = Time.DeltaTime;
            int walkouts = 0;

            using NativeArray<Entity> groups = _queued.ToEntityArray(Allocator.Temp);
            HashSet<Entity> withFriendAhead = WithFriendAhead(groups);
            for (int i = 0; i < groups.Length; i++)
            {
                Entity group = groups[i];
                if (Has<CGroupStartLeaving>(group) || Has<CGroupLeaving>(group)) continue;

                float own = total + TycoonPromotions.QueuePatience;
                if (TycoonUnlocks.FiveStarPricesOn) own *= TycoonUnlocks.FIVE_STAR_PATIENCE;
                if (TycoonContractSystem.LuxuryActive) own *= TycoonContractSystem.LuxuryPatience;
                if (Has<CTycoonContractGroup>(group) && TycoonPromotions.ContractAt(EntityManager.GetComponentData<CTycoonContractGroup>(group).Slot) is TycoonPromotions.Contract contract)
                    own *= contract.Patience / 100f;
                CTycoonQueuePatience patience = Has<CTycoonQueuePatience>(group)
                    ? EntityManager.GetComponentData<CTycoonQueuePatience>(group)
                    : new CTycoonQueuePatience { Remaining = own, Total = own };

                int place = EntityManager.GetComponentData<CQueuePosition>(group).QueuePosition;
                if (!withFriendAhead.Contains(group))
                    patience.Remaining -= dt * (1f + IMPATIENCE_PER_PLACE * Mathf.Max(0, place));

                if (patience.Remaining <= 0f)
                {
                    EntityManager.AddComponent<CGroupStartLeaving>(group);
                    walkouts++;
                    continue;
                }
                if (Has<CTycoonQueuePatience>(group)) EntityManager.SetComponentData(group, patience);
                else EntityManager.AddComponentData(group, patience);
            }

            if (walkouts > 0 && HasSingleton<STycoonEconomy>())
            {
                STycoonEconomy economy = GetSingleton<STycoonEconomy>();
                economy.WalkoutsToday += walkouts;
                economy.WalkoutsTotal += walkouts;
                Set(economy);
                Mod.LogInfo($"Queue: {walkouts} customer(s) gave up waiting and left.");
            }
        }

        private HashSet<Entity> WithFriendAhead(NativeArray<Entity> queued)
        {
            var result = new HashSet<Entity>();
            var atCounter = new HashSet<int>();
            using (EntityQuery counter = EntityManager.CreateEntityQuery(typeof(CCustomerGroup), typeof(CTycoonParty), typeof(CAssignedTable)))
            using (NativeArray<Entity> served = counter.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < served.Length; i++)
                {
                    Entity g = served[i];
                    if (Has<CGroupStartLeaving>(g) || Has<CGroupLeaving>(g) || Has<CTycoonFastCasual>(g)) continue;
                    if (TycoonTakeOutCounterSystem.IsOrderingCounterSet(EntityManager, EntityManager.GetComponentData<CAssignedTable>(g).Table))
                        atCounter.Add(EntityManager.GetComponentData<CTycoonParty>(g).PartyID);
                }
            var partyAt = new Dictionary<int, int>();
            for (int i = 0; i < queued.Length; i++)
                if (Has<CTycoonParty>(queued[i]) && !Has<CGroupStartLeaving>(queued[i]) && !Has<CGroupLeaving>(queued[i]))
                    partyAt[EntityManager.GetComponentData<CQueuePosition>(queued[i]).QueuePosition] = EntityManager.GetComponentData<CTycoonParty>(queued[i]).PartyID;
            for (int i = 0; i < queued.Length; i++)
            {
                Entity g = queued[i];
                if (!Has<CTycoonParty>(g)) continue;
                int party = EntityManager.GetComponentData<CTycoonParty>(g).PartyID;
                int place = EntityManager.GetComponentData<CQueuePosition>(g).QueuePosition;
                bool ahead = place == 0 ? atCounter.Contains(party) : partyAt.TryGetValue(place - 1, out int front) && front == party;
                if (ahead) result.Add(g);
            }
            return result;
        }
    }

    /// <summary>The game's queue systems step aside where Tycoon takes over.</summary>
    [HarmonyPatch(typeof(ManageQueue), "OnUpdate")]
    public static class TycoonQueuePlacementPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonCounterQueueSystem.InCharge;
    }

    [HarmonyPatch(typeof(UpdateQueuePatience), "OnUpdate")]
    public static class TycoonQueuePatiencePatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonSession.IsActive;
    }
}
