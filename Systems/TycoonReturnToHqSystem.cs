using HarmonyLib;
using Kitchen;
using KitchenMods;
using System.Collections.Generic;
using Unity.Collections;
using PlateUpTycoon.Components;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Save and Return to HQ: the Bus Stop zone beside the restaurant at night.</summary>
    public class TycoonReturnToHqSystem : RestaurantSystem, IModSystem
    {
        private const ViewType SELECTOR_VIEW = (ViewType)14;
        private static readonly Vector3 ZONE_OFFSET = new Vector3(5.5f, 0f, -0.5f);
        private static readonly Vector3 ZONE_SIZE = new Vector3(2f, 10f, 2f);

        private EntityQuery _selectors;
        private EntityQuery _activated;
        private bool _leaving;

        protected override void Initialise()
        {
            base.Initialise();
            _selectors = GetEntityQuery(new QueryHelper().All(typeof(CTycoonReturnSelector)));
            _activated = GetEntityQuery(new QueryHelper().All(typeof(CTycoonReturnSelector), typeof(CSelectorActivated)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (_leaving)
            {
                _leaving = false;
                StartSceneTransition(SceneType.Franchise);
                TycoonBusArrival.Arrived();
                return;
            }
            if (!_activated.IsEmpty)
            {
                EntityManager.DestroyEntity(_selectors);
                Entity save = EntityManager.CreateEntity(typeof(CRequestSave));
                EntityManager.SetComponentData(save, new CRequestSave { SaveType = SaveType.AutoFull });
                _leaving = true;
                Mod.LogInfo("Return to HQ: everyone is in the zone, saving and going back.");
                return;
            }

            bool wanted = HasSingleton<SIsNightTime>() && !HasSingleton<SIsDayTime>() && !HasSingleton<SPracticeMode>() && !HasSingleton<SGameOver>() && !HasSingleton<SPerformSceneTransition>();
            if (!wanted)
            {
                if (HasSingleton<SIsDayTime>()) TycoonBusArrival.Clear();
                if (!_selectors.IsEmpty) EntityManager.DestroyEntity(_selectors);
                return;
            }
            if (!_selectors.IsEmpty) return;

            Vector3 centre = GetFrontDoor(true) + ZONE_OFFSET;
            Entity zone = EntityManager.CreateEntity(typeof(CTycoonReturnSelector), typeof(CSelectorEnabled), typeof(CDoNotPersist));
            EntityManager.AddComponentData(zone, new CGroupSelector { Bounds = new Bounds(centre, ZONE_SIZE) });
            EntityManager.AddComponentData(zone, new CRequiresView { Type = SELECTOR_VIEW });
            Mod.LogInfo($"Return to HQ zone placed at {centre}.");
        }
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>After the Return to HQ zone, the players start at the HQ loading bay.</summary>
    public static class TycoonBusArrival
    {
        internal const float EXPIRE_SECONDS = 60f;
        private static bool _pending;
        private static float _setAt;

        internal static void Arrived() { _pending = true; _setAt = UnityEngine.Time.realtimeSinceStartup; Mod.LogInfo("Return to HQ: players will start at the loading bay."); }
        internal static void Clear() => _pending = false;

        internal static bool Pending
        {
            get
            {
                if (_pending && UnityEngine.Time.realtimeSinceStartup - _setAt > EXPIRE_SECONDS) { _pending = false; Mod.LogInfo("Return to HQ: loading bay start expired."); }
                return _pending;
            }
        }
    }

    public class TycoonBusArrivalSystem : FranchiseSystem, IModSystem
    {
        private const float SETTLE_SECONDS = 0.5f;
        private EntityQuery _players, _appliances;
        private float _seenAt = -1f;

        protected override void Initialise()
        {
            base.Initialise();
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CPosition)));
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance), typeof(CPosition)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || !TycoonBusArrival.Pending) { _seenAt = -1f; return; }
            if (_players.IsEmpty) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_seenAt < 0f) { _seenAt = now; return; }
            if (now - _seenAt < SETTLE_SECONDS) return;
            _seenAt = -1f;
            TycoonBusArrival.Clear();
            try
            {
                List<Vector3> spots = Spots();
                using NativeArray<Entity> players = _players.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < players.Length; i++)
                {
                    Vector3 to = spots[i % spots.Count];
                    CPosition pos = EntityManager.GetComponentData<CPosition>(players[i]);
                    Vector3 from = pos.Position;
                    pos.Position = to;
                    pos.ForceSnap = true;
                    EntityManager.SetComponentData(players[i], pos);
                    Mod.LogInfo($"Return to HQ: player moved from {from} to the loading bay at {to}.");
                }
            }
            catch (System.Exception e) { Mod.LogWarning("Return to HQ: could not move the players to the loading bay: " + e.Message); }
        }

        private List<Vector3> Spots()
        {
            var taken = new HashSet<(int, int)>();
            using (NativeArray<CPosition> at = _appliances.ToComponentDataArray<CPosition>(Allocator.Temp))
                foreach (CPosition p in at) taken.Add((Mathf.RoundToInt(p.Position.x), Mathf.RoundToInt(p.Position.z)));
            int room = TileManager.GetTile(new IntVector3(-9, 0, -7)).RoomID;
            var spots = new List<Vector3>();
            int[] xs = { -9, -8, -10, -7, -11, -6 };
            for (int z = -6; z <= -4 && spots.Count < 4; z++)
                foreach (int x in xs)
                {
                    if (taken.Contains((x, z))) continue;
                    CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(x, 0, z));
                    if (tile.Type == Kitchen.Layouts.RoomType.NoRoom || tile.RoomID != room) continue;
                    spots.Add(new Vector3(x, 0f, z));
                    if (spots.Count >= 4) break;
                }
            if (spots.Count == 0)
                foreach (int x in xs) spots.Add(new Vector3(x, 0f, -7f));
            return spots;
        }
    }
}
