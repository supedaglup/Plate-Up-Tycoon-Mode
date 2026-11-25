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
    /// <summary>The garage as storage: its shelves are the game's persistent storage holders.</summary>
    internal static class TycoonGarage
    {
        internal static Entity FreeShelf(EntityManager em)
        {
            using EntityQuery shelves = em.CreateEntityQuery(typeof(CPersistentItemStorageLocation), typeof(CItemHolder), typeof(CPosition));
            using NativeArray<Entity> all = shelves.ToEntityArray(Allocator.Temp);
            foreach (Entity shelf in all)
                if (!em.HasComponent<CCrateInbox>(shelf) && em.GetComponentData<CItemHolder>(shelf).HeldItem == Entity.Null) return shelf;
            return Entity.Null;
        }

        internal static void Hold(EntityManager em, Entity holder, Entity item)
        {
            if (em.HasComponent<CHeldBy>(item)) em.SetComponentData(item, new CHeldBy { Holder = holder });
            else em.AddComponentData(item, new CHeldBy { Holder = holder });
            if (em.HasComponent<CHome>(item)) em.SetComponentData(item, new CHome { Holder = holder });
            else em.AddComponentData(item, new CHome { Holder = holder });
            if (em.HasComponent<CItemHolder>(holder)) em.SetComponentData(holder, new CItemHolder { HeldItem = item });
            else em.AddComponentData(holder, new CItemHolder { HeldItem = item });
        }
    }

    /// <summary>HQ: the bank's restaurant sites.</summary>
    public class TycoonSiteSetupSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery _bankDesks;
        private Entity _builtFor = Entity.Null;
        private Entity _waitingFor = Entity.Null;
        private float _waitUntil;
        private const float WAIT_FOR_SAVES = 8f;

        protected override void Initialise()
        {
            base.Initialise();
            _bankDesks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBankDesk)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _bankDesks.IsEmpty || TycoonBankSetupSystem.OfficeRoom == int.MinValue) return;
            Entity desk = _bankDesks.GetSingletonEntity();
            if (desk == _builtFor) return;
            if (desk != _waitingFor) { _waitingFor = desk; _waitUntil = Time.RealTotalTime + WAIT_FOR_SAVES; }
            if (TycoonSites.SavedRestaurants() == 0 && Time.RealTotalTime < _waitUntil) return;
            _builtFor = desk;
            try { Build(); }
            catch (System.Exception e) { Mod.LogError("Sites: could not set out the blueprints: " + e); }
        }

        private void Build()
        {
            int restaurants = TycoonSites.SavedRestaurants();
            TycoonCompany.EnsureLoaded();
            PlaceBoughtSites();
            if (restaurants == 0) Mod.LogInfo("Sites: a brand-new company; blueprints are for sale from day 0.");

            Vector3 side = new Vector3(TycoonBankSetupSystem.BankerSide.z, 0f, -TycoonBankSetupSystem.BankerSide.x);
            var spots = new List<Vector3>();
            for (int k = 1; k <= 3 && spots.Count < TycoonSites.ON_OFFER; k++)
                foreach (float sign in new[] { 1f, -1f })
                {
                    Vector3 at = TycoonBankSetupSystem.BankerAt + side * (k * sign);
                    at = new Vector3(Mathf.Round(at.x), 0f, Mathf.Round(at.z));
                    var tile = new IntVector3((int)at.x, 0, (int)at.z);
                    CLayoutRoomTile t = TileManager.GetTile(tile);
                    if (t.Type == RoomType.NoRoom || t.RoomID != TycoonBankSetupSystem.OfficeRoom) continue;
                    if (TileManager.GetOccupant(tile, OccupancyLayer.Default) != Entity.Null) continue;
                    if (spots.Count < TycoonSites.ON_OFFER) spots.Add(at);
                }

            TycoonRecipeShop.Owned(out int day);
            List<Entity> offers = TycoonSites.TodaysOffers(EntityManager, day, restaurants + TycoonCompany.SitesBought.Count, spots.Count);
            var names = new List<string>();
            for (int i = 0; i < spots.Count; i++)
            {
                Entity shelf = Create(AssetReference.LayoutPedestal, spots[i], Vector3.forward);
                EntityManager.AddComponent<CTycoonBlueprintShelf>(shelf);
                EntityManager.AddComponent<CDoNotPersist>(shelf);
                if (i >= offers.Count) continue;
                CTycoonBlueprint site = EntityManager.GetComponentData<CTycoonBlueprint>(offers[i]);
                if (TycoonCompany.SitesBought.Contains(TycoonSites.Key(site.Source, site.Layout, site.Setting)))
                {
                    TycoonSites.Destroy(EntityManager, offers[i]);
                    names.Add("(sold)");
                    continue;
                }
                TycoonGarage.Hold(EntityManager, shelf, offers[i]);
                names.Add($"{TycoonSites.SettingName(site.Setting)} {site.Size} sqm for {site.Price}");
            }
            Mod.LogInfo($"Sites: {spots.Count} blueprint pedestal(s) beside the banker for day {day}: {string.Join("; ", names)}.");
        }

        private void PlaceBoughtSites()
        {
            foreach (string key in TycoonCompany.SitesBought)
            {
                if (!TycoonSites.ParseKey(key, out int source, out int layout, out int setting)) continue;
                Entity shelf = TycoonGarage.FreeShelf(EntityManager);
                if (shelf == Entity.Null) { Mod.LogWarning($"Sites: no free garage shelf for the bought site {key}."); continue; }
                Entity map = TycoonSites.Make(EntityManager, source, layout, setting, true);
                TycoonGarage.Hold(EntityManager, shelf, map);
                Mod.LogInfo($"Sites: the bought {TycoonSites.SettingName(setting)} site is on a garage shelf.");
            }
        }
    }

    /// <summary>No walking out of the bank with an unpaid blueprint.</summary>
    public class TycoonSiteGuardSystem : FranchiseSystem, IModSystem
    {
        private const float SPEECH_GAP = 3f;
        private static readonly Vector3 SPEECH_OFFSET = new Vector3(0f, -1f, 0f);
        private EntityQuery _blueprints;
        private EntityQuery _bankers;
        private float _spokeAt = -100f;

        protected override void Initialise()
        {
            base.Initialise();
            _blueprints = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBlueprint), typeof(CHeldBy), typeof(CHome)));
            _bankers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBanker), typeof(CPosition)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _blueprints.IsEmpty || TycoonBankSetupSystem.OfficeRoom == int.MinValue) return;
            using NativeArray<Entity> blueprints = _blueprints.ToEntityArray(Allocator.Temp);
            foreach (Entity blueprint in blueprints)
            {
                Entity holder = EntityManager.GetComponentData<CHeldBy>(blueprint).Holder;
                if (holder != Entity.Null && EntityManager.Exists(holder) && Has<CItemPedestal>(holder))
                {
                    Entity cartHome = EntityManager.GetComponentData<CHome>(blueprint).Holder;
                    if (cartHome != Entity.Null && EntityManager.Exists(cartHome) && cartHome != holder)
                    {
                        HolderHelpers.GoHome(EntityManager, blueprint);
                        Mod.LogInfo("Sites: a blueprint was put on a loading bay cart; plans stay in the HQ, so it went back.");
                    }
                    continue;
                }
                if (EntityManager.GetComponentData<CTycoonBlueprint>(blueprint).Paid) continue;
                if (holder == Entity.Null || !EntityManager.Exists(holder) || Has<CTycoonBlueprintShelf>(holder) || !Require(holder, out CPosition at)) continue;
                CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(at.Position.x), 0, Mathf.RoundToInt(at.Position.z)));
                if (tile.Type != RoomType.NoRoom && tile.RoomID == TycoonBankSetupSystem.OfficeRoom) continue;
                Entity home = EntityManager.GetComponentData<CHome>(blueprint).Holder;
                if (home == Entity.Null || !EntityManager.Exists(home)) continue;
                HolderHelpers.GoHome(EntityManager, blueprint);
                Mod.LogInfo("Sites: an unpaid blueprint left the bank; back on its pedestal.");
                if (Time.RealTotalTime - _spokeAt > SPEECH_GAP && !_bankers.IsEmpty)
                {
                    _spokeAt = Time.RealTotalTime;
                    using NativeArray<CPosition> banker = _bankers.ToComponentDataArray<CPosition>(Allocator.Temp);
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, banker[0].Position + SPEECH_OFFSET, TycoonBankerDialogue.Say("stop-thief"), 4f);
                }
            }
        }
    }
}
