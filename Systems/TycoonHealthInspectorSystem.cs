using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>The health inspector: a second special customer, a cat in glasses.</summary>
    public class TycoonHealthInspectorSystem : RestaurantSystem, IModSystem
    {
        public const int STAGE_NONE = 0, STAGE_BOOKED = 1, STAGE_TOUR = 2, STAGE_CUSTOMER = 3;
        private const float LOOK_SECONDS = 2.5f;
        private const float STOP_TIMEOUT = 20f;
        private const float ARRIVE_DISTANCE = 0.6f;
        private const int DINING_STOPS = 3;
        private const float SIGHT_RANGE = 4f, PEEK_RANGE = 1.6f;
        public const float GRACE_SECONDS_SOLO = 6f, GRACE_SECONDS_TEAM = 3f;
        private readonly Dictionary<Entity, float> _pendingMesses = new Dictionary<Entity, float>();
        private readonly Dictionary<Entity, float> _pendingBins = new Dictionary<Entity, float>();
        private int _messesFound, _binsFound;

        internal static Vector3 TourEndPosition;

        internal static bool DebugSendNow;
        internal static bool InspectorGroupPending;

        private EntityQuery _walkers;
        private EntityQuery _messes;
        private EntityQuery _bins;
        private EntityQuery _appliances;
        private EntityQuery _scheduled;
        private EntityQuery _inspectorGroups;
        private readonly List<Vector3> _stops = new List<Vector3>();
        private readonly HashSet<Entity> _messesSeen = new HashSet<Entity>();
        private readonly HashSet<Entity> _binsSeen = new HashSet<Entity>();
        private int _stop;
        private float _stopStartedAt;
        private float _arrivedAt;

        public const float STUCK_SECONDS = 8f, MOVED_DISTANCE = 0.35f;
        public const int TELEPORTS = 2;
        private Entity _watched = Entity.Null;
        private Vector3 _watchAnchor;
        private bool _anchored;
        private float _stillFor;
        private int _rescues;

        protected override void Initialise()
        {
            base.Initialise();
            _walkers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonInspectorWalker), typeof(CPosition)));
            _messes = GetEntityQuery(new QueryHelper().All(typeof(CMess), typeof(CPosition)));
            _bins = GetEntityQuery(new QueryHelper().All(typeof(CApplianceBin), typeof(CPosition)).None(typeof(CApplianceExternalBin)));
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance), typeof(CPosition)));
            _scheduled = GetEntityQuery(new QueryHelper().All(typeof(CScheduledCustomer), typeof(CCustomerType)));
            _inspectorGroups = GetEntityQuery(new QueryHelper().All(typeof(CTycoonInspector), typeof(CGroupMember)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STime>();
        }

        protected override void OnUpdate()
        {
            STycoonInspection inspection = HasSingleton<STycoonInspection>() ? GetSingleton<STycoonInspection>() : default;
            bool changed = !HasSingleton<STycoonInspection>();

            if (!HasSingleton<SIsDayTime>() || HasSingleton<SPracticeMode>())
            {
                _pendingMesses.Clear();
                _pendingBins.Clear();
                if (!_walkers.IsEmpty) EntityManager.DestroyEntity(_walkers);
                InspectorGroupPending = false;
                if (changed) Set(inspection);
                return;
            }

            if (HasSingleton<SIsDayFirstUpdate>())
            {
                Book(ref inspection);
                changed = true;
            }
            if (DebugSendNow)
            {
                DebugSendNow = false;
                inspection = new STycoonInspection { Stage = STAGE_BOOKED, ArrivesAt = 0f, InspectionsTotal = inspection.InspectionsTotal };
                changed = true;
                Mod.LogWarning("DEBUG: a health inspector is on the way now.");
            }

            float now = GetSingleton<STime>().TimeOfDay;
            if (inspection.Stage == STAGE_BOOKED && now >= inspection.ArrivesAt && !TycoonDayPhases.InOpening)
            {
                StartTour();
                inspection.Stage = STAGE_TOUR;
                inspection.InspectionsTotal++;
                changed = true;
            }
            else if (inspection.Stage == STAGE_TOUR && ReadyForNextStop())
            {
                NextStop(ref inspection);
                changed = true;
            }

            if (inspection.Stage == STAGE_TOUR || (inspection.Stage == STAGE_CUSTOMER && !inspection.Served))
            {
                if (LookAround(ref inspection)) changed = true;
            }
            if ((_pendingMesses.Count > 0 || _pendingBins.Count > 0) && ResolvePending(ref inspection)) changed = true;
            if (inspection.Stage == STAGE_CUSTOMER) WatchForStuck();

            if (changed || inspection.Stage != (HasSingleton<STycoonInspection>() ? GetSingleton<STycoonInspection>().Stage : -1)) Set(inspection);
        }

        private void Book(ref STycoonInspection inspection)
        {
            int total = inspection.InspectionsTotal;
            inspection = new STycoonInspection { InspectionsTotal = total };
            if (TycoonReputation.Current.CriticToday != TycoonReputation.CRITIC_NONE) return;
            float chance = TycoonReputation.INSPECTION_CHANCE + TycoonReputation.INSPECTION_CHANCE_PER_STAR * (TycoonEconomy.CurrentStars - TycoonEconomy.MIN_STARS);
            if (Random.value >= chance) return;
            inspection.Stage = STAGE_BOOKED;
            inspection.ArrivesAt = Random.Range(0.15f, 0.5f);
            Mod.LogInfo($"Health inspector: booked for {inspection.ArrivesAt:P0} of today's service (chance was {chance:P0}).");
        }

        private void StartTour()
        {
            _stops.Clear();
            _messesSeen.Clear();
            _binsSeen.Clear();
            _pendingMesses.Clear();
            _pendingBins.Clear();
            _messesFound = _binsFound = 0;
            Vector3 inside = GetFrontDoor(false), outside = GetFrontDoor(true);
            _stops.Add(outside);
            _stops.Add(inside);

            var dining = new List<Vector3>();
            using (NativeArray<Entity> appliances = _appliances.ToEntityArray(Allocator.Temp))
            using (NativeArray<CPosition> positions = _appliances.ToComponentDataArray<CPosition>(Allocator.Temp))
            {
                for (int i = 0; i < appliances.Length; i++)
                {
                    if (Has<CTycoonPurchasingDesk>(appliances[i]) || Has<CTycoonManagementDesk>(appliances[i]) || Has<CMess>(appliances[i])) continue;
                    Vector3 front = positions[i].Position + positions[i].Forward(1f);
                    Vector3 spot = new Vector3(Mathf.Round(front.x), 0f, Mathf.Round(front.z));
                    CLayoutRoomTile tile = TileManager.GetTile(new IntVector3((int)spot.x, 0, (int)spot.z));
                    if (tile.Type == Kitchen.Layouts.RoomType.NoRoom || tile.Type == Kitchen.Layouts.RoomType.Kitchen) continue;
                    if (!dining.Contains(spot)) dining.Add(spot);
                }
            }

            var kitchen = new List<Vector3>();
            Bounds bounds = Bounds;
            for (int x = Mathf.FloorToInt(bounds.min.x); x <= Mathf.CeilToInt(bounds.max.x); x++)
                for (int z = Mathf.FloorToInt(bounds.min.z); z <= Mathf.CeilToInt(bounds.max.z); z++)
                {
                    var p = new IntVector3(x, 0, z);
                    if (TileManager.GetTile(p).Type != Kitchen.Layouts.RoomType.Kitchen) continue;
                    if (TileManager.GetOccupant(p, OccupancyLayer.Default) != Entity.Null) continue;
                    kitchen.Add(new Vector3(x, 0f, z));
                }
            kitchen.Sort((a, b) => (b - inside).sqrMagnitude.CompareTo((a - inside).sqrMagnitude));
            int kitchenTiles = kitchen.Count;

            TakeStops(dining, 1);
            if (kitchen.Count > 0)
            {
                Vector3 far = kitchen[0];
                kitchen.RemoveAt(0);
                int half = Mathf.Max(1, kitchen.Count / 2);
                if (kitchen.Count > 0) _stops.Add(kitchen[Random.Range(0, Mathf.Min(half, kitchen.Count))]);
                _stops.Add(far);
            }
            TakeStops(dining, DINING_STOPS - 1);
            _stops.Add(inside);

            Vector3 street = new Vector3(bounds.min.x - 10f + Random.Range(-3, 3), 0f, -6f + Random.Range(-1, 1));
            Entity walker = EntityManager.CreateEntity(typeof(CTycoonInspectorWalker), typeof(CCustomer), typeof(CCustomerState), typeof(CPosition),
                typeof(CPlayerCosmetics), typeof(CRequiresView), typeof(CMoveToLocation), typeof(CDoNotPersist));
            EntityManager.SetComponentData(walker, new CCustomer { Scale = 1f, Speed = 1f });
            EntityManager.SetComponentData(walker, (CCustomerState)CCustomerState.State.Normal);
            EntityManager.SetComponentData(walker, new CPosition(street));
            Patches.SpecialCats.Mark(EntityManager, walker, Patches.SpecialCats.INSPECTOR_HATS);
            EntityManager.SetComponentData(walker, new CRequiresView { Type = ViewType.CustomerCat, PhysicsDriven = true });
            _stop = -1;
            _stopStartedAt = 0f;
            _arrivedAt = -1f;
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), "A health inspector has arrived!", 6f);
            Mod.LogInfo($"Health inspector: arrived on the street at {street}; {kitchenTiles} kitchen tile(s) free; tour of {_stops.Count} stop(s): {string.Join(" ", _stops)}.");
        }

        private void TakeStops(List<Vector3> from, int count)
        {
            for (int n = 0; n < count && from.Count > 0; n++)
            {
                int pick = Random.Range(0, from.Count);
                _stops.Add(from[pick]);
                from.RemoveAt(pick);
            }
        }

        private bool ReadyForNextStop()
        {
            if (_walkers.IsEmpty || _stop < 0 || _stop >= _stops.Count) return true;
            float t = Time.RealTotalTime;
            if (_arrivedAt < 0f)
            {
                Vector3 at = EntityManager.GetComponentData<CPosition>(_walkers.GetSingletonEntity()).Position;
                Vector3 gap = _stops[_stop] - at;
                gap.y = 0f;
                if (gap.magnitude <= ARRIVE_DISTANCE) _arrivedAt = t;
                else if (t >= _stopStartedAt + STOP_TIMEOUT)
                {
                    Mod.LogInfo($"Health inspector: could not reach stop {_stop + 1} at {_stops[_stop]} (stuck at {at}); moving on.");
                    return true;
                }
                return false;
            }
            return t >= _arrivedAt + LOOK_SECONDS;
        }

        private void NextStop(ref STycoonInspection inspection)
        {
            _stop++;
            if (_walkers.IsEmpty || _stop >= _stops.Count)
            {
                EndTour(ref inspection);
                return;
            }
            Entity walker = _walkers.GetSingletonEntity();
            Vector3 target = _stops[_stop];
            Vector3 from = EntityManager.GetComponentData<CPosition>(walker).Position;
            CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(target.x), 0, Mathf.RoundToInt(target.z)));
            Mod.LogInfo($"Health inspector: stop {_stop + 1}/{_stops.Count} at {target} ({tile.Type}); the cat is at {from}.");
            Vector3 facing = target - from;
            facing.y = 0f;
            EntityManager.SetComponentData(walker, new CMoveToLocation
            {
                Location = target,
                DesiredFacing = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.forward,
                StoppingDistance = 0.2f,
            });
            _stopStartedAt = Time.RealTotalTime;
            _arrivedAt = -1f;
        }

        private void EndTour(ref STycoonInspection inspection)
        {
            TourEndPosition = GetFrontDoor(true);
            if (!_walkers.IsEmpty) EntityManager.DestroyEntity(_walkers);
            inspection.Stage = STAGE_CUSTOMER;
            Mod.LogInfo($"Health inspector: tour done ({inspection.MessesSeen} mess(es), {inspection.FullBinsSeen} full bin(s) seen); joining the customers.");

            if (_scheduled.IsEmpty) { Mod.LogInfo("Health inspector: no customer type to copy (schedule empty), so they leave."); return; }
            Entity template;
            using (NativeArray<Entity> all = _scheduled.ToEntityArray(Allocator.Temp)) template = all[0];
            Entity scheduled = EntityManager.CreateEntity(typeof(CScheduledCustomer), typeof(CCustomerType));
            EntityManager.SetComponentData(scheduled, EntityManager.GetComponentData<CCustomerType>(template));
            EntityManager.SetComponentData(scheduled, new CScheduledCustomer { TimeOfDay = GetSingleton<STime>().TimeOfDay, GroupSize = 1, IsCat = true });
            InspectorGroupPending = true;
        }

        private void WatchForStuck()
        {
            if (_inspectorGroups.IsEmpty) { _watched = Entity.Null; return; }
            Entity group;
            using (NativeArray<Entity> groups = _inspectorGroups.ToEntityArray(Allocator.Temp)) group = groups[0];
            if (group != _watched) { _watched = group; _anchored = false; _stillFor = 0f; _rescues = 0; }

            bool shouldMove = Has<CGroupGoingToTable>(group)
                || !(Has<CQueuePosition>(group) || Has<CGroupWaitingForTable>(group) || Has<CAssignedTable>(group)
                     || Has<CGroupLeaving>(group) || Has<CGroupStartLeaving>(group));
            DynamicBuffer<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(group);
            if (!shouldMove || members.Length == 0 || !Has<CPosition>(members[0].Customer)) { _anchored = false; _stillFor = 0f; return; }

            Vector3 at = EntityManager.GetComponentData<CPosition>(members[0].Customer).Position;
            Vector3 moved = at - _watchAnchor;
            moved.y = 0f;
            if (!_anchored || moved.magnitude > MOVED_DISTANCE) { _watchAnchor = at; _anchored = true; _stillFor = 0f; return; }
            _stillFor += Time.DeltaTime;
            if (_stillFor < STUCK_SECONDS) return;
            _anchored = false;
            _stillFor = 0f;

            if (_rescues < TELEPORTS)
            {
                _rescues++;
                Vector3 door = GetFrontDoor(true);
                var memberList = new List<Entity>();
                for (int i = 0; i < members.Length; i++) memberList.Add(members[i].Customer);
                foreach (Entity m in memberList)
                    if (Has<CPosition>(m)) EntityManager.SetComponentData(m, new CPosition(door));
                Mod.LogWarning($"Health inspector: STUCK at {at} for {STUCK_SECONDS:0} s ({(Has<CGroupGoingToTable>(group) ? "going to his table" : "walking")}); put at the front door {door} (rescue {_rescues}/{TELEPORTS}).");
                return;
            }
            EntityManager.AddComponent<CGroupStartLeaving>(group);
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, at, "The health inspector couldn't reach a table and left");
            Mod.LogWarning($"Health inspector: still STUCK at {at} after {TELEPORTS} rescues; he leaves (no walkout penalty, no spotless bonus).");
        }

        internal static bool InspectorGroupExists(EntityManager em)
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(CTycoonInspector));
            return !q.IsEmpty;
        }

        private bool LookAround(ref STycoonInspection inspection)
        {
            var eyes = new List<Vector3>();
            if (!_walkers.IsEmpty)
                eyes.Add(EntityManager.GetComponentData<CPosition>(_walkers.GetSingletonEntity()).Position);
            using (NativeArray<Entity> groups = _inspectorGroups.ToEntityArray(Allocator.Temp))
                foreach (Entity group in groups)
                {
                    DynamicBuffer<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(group);
                    for (int i = 0; i < members.Length; i++)
                    {
                        Entity member = members[i];
                        if (EntityManager.HasComponent<CPosition>(member)) eyes.Add(EntityManager.GetComponentData<CPosition>(member).Position);
                    }
                }
            if (eyes.Count == 0) return false;

            float grace = TycoonKitchenLoad.Players > 1 ? GRACE_SECONDS_TEAM : GRACE_SECONDS_SOLO;
            float deadline = Time.TotalTime + grace;
            using (NativeArray<Entity> messes = _messes.ToEntityArray(Allocator.Temp))
            using (NativeArray<CPosition> positions = _messes.ToComponentDataArray<CPosition>(Allocator.Temp))
                for (int i = 0; i < messes.Length; i++)
                    if (!_messesSeen.Contains(messes[i]) && !TycoonMesses.IsWetFloor(EntityManager, messes[i]) && CanSee(eyes, positions[i].Position))
                    {
                        _messesSeen.Add(messes[i]);
                        _pendingMesses[messes[i]] = deadline;
                        Spotted(positions[i].Position, grace, "mess");
                    }
            using (NativeArray<Entity> bins = _bins.ToEntityArray(Allocator.Temp))
            using (NativeArray<CApplianceBin> contents = _bins.ToComponentDataArray<CApplianceBin>(Allocator.Temp))
            using (NativeArray<CPosition> positions = _bins.ToComponentDataArray<CPosition>(Allocator.Temp))
                for (int i = 0; i < bins.Length; i++)
                {
                    bool full = contents[i].Capacity > 0 && contents[i].CurrentAmount >= contents[i].Capacity;
                    if (full && !_binsSeen.Contains(bins[i]) && CanSee(eyes, positions[i].Position))
                    {
                        _binsSeen.Add(bins[i]);
                        _pendingBins[bins[i]] = deadline;
                        Spotted(positions[i].Position, grace, "full bin");
                    }
                }
            return false;
        }

        private void Spotted(Vector3 at, float grace, string what)
        {
            Mod.LogInfo($"Health inspector: spotted a {what} at {at}; {grace:0} s to clean it before it counts.");
        }

        private bool ResolvePending(ref STycoonInspection inspection)
        {
            float now = Time.TotalTime;
            bool changed = false;
            foreach (Entity e in new List<Entity>(_pendingMesses.Keys))
            {
                if (now < _pendingMesses[e]) continue;
                _pendingMesses.Remove(e);
                if (EntityManager.Exists(e) && EntityManager.HasComponent<CMess>(e)) { _messesFound++; changed = true; }
                else { _messesSeen.Remove(e); Mod.LogInfo("Health inspector: a spotted mess was cleaned in time (forgiven)."); }
            }
            foreach (Entity e in new List<Entity>(_pendingBins.Keys))
            {
                if (now < _pendingBins[e]) continue;
                _pendingBins.Remove(e);
                bool stillFull = false;
                if (EntityManager.Exists(e) && EntityManager.HasComponent<CApplianceBin>(e))
                {
                    CApplianceBin bin = EntityManager.GetComponentData<CApplianceBin>(e);
                    stillFull = bin.Capacity > 0 && bin.CurrentAmount >= bin.Capacity;
                }
                if (stillFull) { _binsFound++; changed = true; }
                else { _binsSeen.Remove(e); Mod.LogInfo("Health inspector: a spotted full bin was emptied in time (forgiven)."); }
            }
            if (!changed) return false;
            inspection.MessesSeen = _messesFound;
            inspection.FullBinsSeen = _binsFound;
            Mod.LogInfo($"Health inspector: a problem COUNTS; {_messesFound} mess(es), {_binsFound} full bin(s) so far ({_messes.CalculateEntityCount()} mess(es) in the restaurant).");
            return true;
        }

        private bool CanSee(List<Vector3> eyes, Vector3 target)
        {
            CLayoutRoomTile targetTile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(target.x), 0, Mathf.RoundToInt(target.z)));
            foreach (Vector3 eye in eyes)
            {
                Vector3 gap = target - eye;
                gap.y = 0f;
                float distance = gap.magnitude;
                if (distance <= PEEK_RANGE) return true;
                if (distance > SIGHT_RANGE) continue;
                CLayoutRoomTile eyeTile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(eye.x), 0, Mathf.RoundToInt(eye.z)));
                if (eyeTile.Type != Kitchen.Layouts.RoomType.NoRoom && eyeTile.RoomID == targetTile.RoomID) return true;
            }
            return false;
        }
    }
}
