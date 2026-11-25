using HarmonyLib;
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
    /// <summary>The Ordering Counter has no visible chair: customers stand at it.</summary>
    public class TycoonHideCounterChairsSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _counters;
        private EntityQuery _ghostChairs;
        private EntityQuery _chairs;
        private int _loggedDay = -1;

        protected override void Initialise()
        {
            base.Initialise();
            _counters = GetEntityQuery(new QueryHelper().All(typeof(CTycoonOrderingCounter), typeof(CPosition)));
            _ghostChairs = GetEntityQuery(new QueryHelper().All(typeof(CApplianceGhostChair)));
            _chairs = GetEntityQuery(new QueryHelper().All(typeof(CApplianceChair), typeof(CPosition)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireForUpdate(_counters);
        }

        protected override void OnUpdate()
        {
            var tables = new HashSet<Entity>();
            var spots = new List<Vector3>();
            int hidden = 0;
            using (NativeArray<Entity> counters = _counters.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < counters.Length; i++)
                {
                    Entity counter = counters[i];
                    UpgradeOldCounter(counter);
                    tables.Add(counter);
                    CPosition pos = EntityManager.GetComponentData<CPosition>(counter);
                    Unity.Mathematics.float3 offset = Customs.OrderingCounter.CustomerOffset(Customs.OrderingCounter.CustomerSide);
                    spots.Add(pos.Position + (Vector3)Unity.Mathematics.math.mul(pos.Rotation, offset));

                    if (!Require(counter, out CPartOfTableSet part) || part.TableSet == Entity.Null || !EntityManager.Exists(part.TableSet)) continue;
                    Entity set = part.TableSet;
                    tables.Add(set);
                    if (EntityManager.HasComponent<CTablePlace>(set))
                    {
                        DynamicBuffer<CTablePlace> places = EntityManager.GetBuffer<CTablePlace>(set);
                        for (int p = 0; p < places.Length; p++) hidden += Hide(places[p].Chair);
                    }
                    if (EntityManager.HasComponent<CTableSetParts>(set))
                    {
                        DynamicBuffer<CTableSetParts> parts = EntityManager.GetBuffer<CTableSetParts>(set);
                        for (int p = 0; p < parts.Length; p++)
                        {
                            Entity e = parts[p];
                            if (EntityManager.Exists(e) && (Has<CApplianceChair>(e) || Has<CApplianceGhostChair>(e))) hidden += Hide(e);
                        }
                    }
                }
            }

            if (!_ghostChairs.IsEmpty)
            {
                using NativeArray<Entity> ghosts = _ghostChairs.ToEntityArray(Allocator.Temp);
                using NativeArray<CApplianceGhostChair> data = _ghostChairs.ToComponentDataArray<CApplianceGhostChair>(Allocator.Temp);
                for (int i = 0; i < ghosts.Length; i++)
                    if (tables.Contains(data[i].Table)) hidden += Hide(ghosts[i]);
            }

            if (!_chairs.IsEmpty)
            {
                using NativeArray<Entity> chairs = _chairs.ToEntityArray(Allocator.Temp);
                using NativeArray<CPosition> chairPos = _chairs.ToComponentDataArray<CPosition>(Allocator.Temp);
                for (int i = 0; i < chairs.Length; i++)
                    foreach (Vector3 spot in spots)
                    {
                        Vector3 d = chairPos[i].Position - spot; d.y = 0f;
                        if (d.sqrMagnitude < 0.2f) { hidden += Hide(chairs[i]); break; }
                    }
            }

            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (HasSingleton<SIsDayFirstUpdate>() && day != _loggedDay)
            {
                _loggedDay = day;
                Mod.LogInfo($"Ordering Counter check: {spots.Count} counter(s), customers on the {Customs.OrderingCounter.CustomerSide} side, {hidden} chair(s) newly hidden this frame.");
            }
        }

        private void UpgradeOldCounter(Entity counter)
        {
            if (!Has<CApplianceRequiresPathable>(counter)) EntityManager.AddComponent<CApplianceRequiresPathable>(counter);
            if (!Require(counter, out CApplianceTable table)) return;
            Orientation side = Customs.OrderingCounter.CustomerSide;
            bool up = side != Orientation.Up, down = side != Orientation.Down, left = side != Orientation.Left, right = side != Orientation.Right;
            if (table.PreventSittingUp == up && table.PreventSittingDown == down && table.PreventSittingLeft == left && table.PreventSittingRight == right) return;
            table.PreventSittingUp = up; table.PreventSittingDown = down; table.PreventSittingLeft = left; table.PreventSittingRight = right;
            EntityManager.SetComponentData(counter, table);
            Mod.LogInfo("Ordering Counter: updated a counter from an older build to serve one side only (takes effect at the next table refresh, e.g. tonight).");
        }

        private int Hide(Entity chair)
        {
            if (chair == Entity.Null || !EntityManager.Exists(chair) || Has<CHideView>(chair)) return 0;
            EntityManager.AddComponent<CHideView>(chair);
            return 1;
        }
    }

    /// <summary>Customers at an Ordering Counter stand instead of sitting.</summary>
    [HarmonyPatch(typeof(MembersUpdateState), "OnUpdate")]
    public static class TycoonStandAtCounterPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MembersUpdateState __instance)
        {
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                using EntityQuery takeOut = em.CreateEntityQuery(typeof(STycoonTakeOutOn));
                if (takeOut.IsEmpty) return;
                using (EntityQuery seated = em.CreateEntityQuery(typeof(CCustomerGroup), typeof(CGroupMember), typeof(CTycoonFastCasual)))
                using (NativeArray<Entity> seatedGroups = seated.ToEntityArray(Allocator.Temp))
                    for (int i = 0; i < seatedGroups.Length; i++)
                    {
                        int fcState = em.GetComponentData<CTycoonFastCasual>(seatedGroups[i]).State;
                        if (fcState != TycoonFastCasual.STATE_SEATED && fcState != TycoonFastCasual.STATE_EATING && fcState != TycoonFastCasual.STATE_DONE) continue;
                        DynamicBuffer<CGroupMember> sitting = em.GetBuffer<CGroupMember>(seatedGroups[i]);
                        for (int m = 0; m < sitting.Length; m++)
                            if (em.HasComponent<CCustomerState>(sitting[m])) em.SetComponentData(sitting[m], (CCustomerState)CCustomerState.State.AtTable);
                    }
                using EntityQuery groups = em.CreateEntityQuery(typeof(CCustomerGroup), typeof(CGroupMember), typeof(CAssignedTable));
                if (groups.IsEmpty) return;
                using NativeArray<Entity> entities = groups.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity table = em.GetComponentData<CAssignedTable>(entities[i]).Table;
                    if (!TycoonTakeOutCounterSystem.IsOrderingCounterSet(em, table)) continue;
                    DynamicBuffer<CGroupMember> members = em.GetBuffer<CGroupMember>(entities[i]);
                    for (int m = 0; m < members.Length; m++)
                    {
                        Entity member = members[m];
                        if (!em.HasComponent<CCustomerState>(member)) continue;
                        CCustomerState.State state = em.GetComponentData<CCustomerState>(member);
                        if (state == CCustomerState.State.AtTable)
                            em.SetComponentData(member, (CCustomerState)CCustomerState.State.Normal);
                    }
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Stand-at-counter: " + e.Message);
            }
        }
    }

    /// <summary>No dirty cups left on the Ordering Counter after a group eats.</summary>
    [HarmonyPatch(typeof(SpawnTableDirt), "OnUpdate")]
    public static class TycoonNoCounterDirtPatch
    {
        [HarmonyPrefix]
        private static void Prefix(SpawnTableDirt __instance)
        {
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                using EntityQuery dirty = em.CreateEntityQuery(typeof(CTableSpawnDirt));
                if (dirty.IsEmpty) return;
                using NativeArray<Entity> sets = dirty.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < sets.Length; i++)
                    if (TycoonTakeOutCounterSystem.IsOrderingCounterSet(em, sets[i]))
                        em.RemoveComponent<CTableSpawnDirt>(sets[i]);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("No-counter-dirt: " + e.Message);
            }
        }
    }

    /// <summary>Take-away mess: customers served at an Ordering Counter don't mess up the counter.</summary>
    public class TycoonTakeOutMessSystem : RestaurantSystem, IModSystem
    {
        public const float MESS_CHANCE = 0.3f;
        public const float DROP_AFTER_MIN = 0.4f, DROP_AFTER_MAX = 2.5f;

        private EntityQuery _groups;
        private readonly HashSet<Entity> _servedAtCounter = new HashSet<Entity>();
        private readonly Dictionary<Entity, float> _dropAt = new Dictionary<Entity, float>();
        private readonly List<Entity> _done = new List<Entity>();

        protected override void Initialise()
        {
            base.Initialise();
            _groups = GetEntityQuery(new QueryHelper().All(typeof(CCustomerGroup), typeof(CGroupMember)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SIsDayTime>()) { _servedAtCounter.Clear(); _dropAt.Clear(); return; }
            float now = Time.TotalTime;

            using (NativeArray<Entity> groups = _groups.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < groups.Length; i++)
                {
                    Entity g = groups[i];
                    if (Has<CGroupEating>(g) && Require(g, out CAssignedTable table)
                        && TycoonTakeOutCounterSystem.IsOrderingCounterSet(EntityManager, table.Table)
                        && !TycoonFastCasual.IsFastCasual(EntityManager, g))
                    {
                        if (_servedAtCounter.Add(g))
                            foreach (CGroupMember m in EntityManager.GetBuffer<CGroupMember>(g))
                                if (EntityManager.Exists(m.Customer) && !Has<CCustomerHasLeftoversBag>(m.Customer))
                                    EntityManager.AddComponent<CCustomerHasLeftoversBag>(m.Customer);
                    }

                    if (_servedAtCounter.Contains(g) && Has<CGroupLeaving>(g))
                    {
                        _servedAtCounter.Remove(g);
                        if (UnityEngine.Random.value < MESS_CHANCE) _dropAt[g] = now + UnityEngine.Random.Range(DROP_AFTER_MIN, DROP_AFTER_MAX);
                    }
                }
            }

            _done.Clear();
            foreach (KeyValuePair<Entity, float> pending in _dropAt)
            {
                Entity g = pending.Key;
                if (!EntityManager.Exists(g) || !EntityManager.HasComponent<CGroupMember>(g)) { _done.Add(g); continue; }
                if (now < pending.Value) continue;
                _done.Add(g);
                DynamicBuffer<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(g);
                if (members.Length == 0) continue;
                Entity member = members[UnityEngine.Random.Range(0, members.Length)];
                if (!Require(member, out CPosition position)) continue;
                if (TileManager.GetTile(position.Position).Type == Kitchen.Layouts.RoomType.NoRoom) continue;

                Entity request = EntityManager.CreateEntity(typeof(CPosition), typeof(CMessRequest));
                EntityManager.SetComponentData(request, new CPosition(position.Position));
                EntityManager.SetComponentData(request, new CMessRequest { ID = AssetReference.CustomerMess });
            }
            foreach (Entity g in _done) _dropAt.Remove(g);
            if (_servedAtCounter.Count > 0) _servedAtCounter.RemoveWhere(e => !EntityManager.Exists(e));
        }
    }

    /// <summary>A small patience bar over every customer waiting in a Tycoon queue: full when they join, empties as they wait.</summary>
    public class TycoonQueuePatienceBarsSystem : RestaurantSystem, IModSystem
    {
        public const float BAR_HEIGHT = 0.8f;
        public const float WARNING_BELOW = 0.3f;

        private EntityQuery _queued;
        private EntityQuery _bars;
        private readonly Dictionary<Entity, Entity> _barOf = new Dictionary<Entity, Entity>();
        private int _barsLogged;

        protected override void Initialise()
        {
            base.Initialise();
            _queued = GetEntityQuery(new QueryHelper().All(typeof(CCustomerGroup), typeof(CQueuePosition), typeof(CTycoonQueuePatience), typeof(CGroupMember)));
            _bars = GetEntityQuery(new QueryHelper().All(typeof(CTycoonPatienceBar)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            _barOf.Clear();
            using (NativeArray<Entity> bars = _bars.ToEntityArray(Allocator.Temp))
            using (NativeArray<CTycoonPatienceBar> barData = _bars.ToComponentDataArray<CTycoonPatienceBar>(Allocator.Temp))
            {
                for (int i = 0; i < bars.Length; i++)
                {
                    Entity group = barData[i].Group;
                    bool keep = HasSingleton<SIsDayTime>() && group != Entity.Null && EntityManager.Exists(group)
                        && Has<CQueuePosition>(group) && Has<CTycoonQueuePatience>(group)
                        && !Has<CGroupStartLeaving>(group) && !Has<CGroupLeaving>(group) && !_barOf.ContainsKey(group);
                    if (keep) _barOf[group] = bars[i];
                    else EntityManager.DestroyEntity(bars[i]);
                }
            }
            if (!HasSingleton<SIsDayTime>() || _queued.IsEmpty) return;

            using NativeArray<Entity> groups = _queued.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < groups.Length; i++)
            {
                Entity group = groups[i];
                if (Has<CGroupStartLeaving>(group) || Has<CGroupLeaving>(group)) continue;
                DynamicBuffer<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(group);
                if (members.Length == 0 || !Require(members[0], out CPosition head)) continue;

                CTycoonQueuePatience patience = EntityManager.GetComponentData<CTycoonQueuePatience>(group);
                float left = patience.Total > 0f ? Mathf.Clamp01(patience.Remaining / patience.Total) : 1f;

                if (!_barOf.TryGetValue(group, out Entity bar))
                {
                    bar = EntityManager.CreateEntity(typeof(CTycoonPatienceBar), typeof(CRequiresView), typeof(CPosition), typeof(CProgressIndicator), typeof(CDoNotPersist));
                    EntityManager.SetComponentData(bar, new CTycoonPatienceBar { Group = group });
                    EntityManager.SetComponentData(bar, new CRequiresView { Type = ViewType.ProgressView });
                    _barOf[group] = bar;
                    if (_barsLogged < 3) { _barsLogged++; Mod.LogInfo($"Queue patience bar created for customer {group.Index} at {head.Position} ({left:P0} patience left)."); }
                }
                EntityManager.SetComponentData(bar, new CPosition(head.Position + new Vector3(0f, BAR_HEIGHT, 0f)));
                EntityManager.SetComponentData(bar, new CProgressIndicator { Progress = left, IsBad = left < WARNING_BELOW, Process = Patches.PatienceBarViewPatch.PATIENCE_BAR_PROCESS });
            }
        }
    }
}
