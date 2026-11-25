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
    /// <summary>HQ: Whiskers's discount appliance shop in the top of the garage, where the vanilla workshop was.</summary>
    public class TycoonDiscountShopSetupSystem : FranchiseSystem, IModSystem
    {
        internal const int NO_ROOM = int.MinValue;
        internal static int ShopRoom = NO_ROOM;
        internal static HashSet<(int x, int z)> ShopTiles = new HashSet<(int x, int z)>();
        internal static Vector3 SellerAt;

        private EntityQuery _desks;

        protected override void Initialise()
        {
            base.Initialise();
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonDiscountDesk)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            if (!_desks.IsEmpty || Mod.DiscountDesk == null) return;
            if (!HasSingleton<SSelectedLayoutPedestal>()) return;
            using (EntityQuery garage = EntityManager.CreateEntityQuery(typeof(CPersistentItemStorageLocation)))
                if (garage.IsEmpty) return;
            try { Build(); }
            catch (System.Exception e) { Mod.LogError("Discount shop: could not build it: " + e); }
        }

        internal static bool InShop(TileManager tiles, Vector3 p)
        {
            if (ShopRoom == NO_ROOM) return false;
            var k = (Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
            return ShopTiles.Contains(k);
        }

        private void Build()
        {
            ShopRoom = NO_ROOM;
            ShopTiles = new HashSet<(int x, int z)>();
            Vector3 anchor = LobbyPositionAnchors.Workshop + new Vector3(-2f, 0f, 2f);
            (int x, int z) start = TycoonShopLayout.Key(anchor);
            CLayoutRoomTile startTile = TileManager.GetTile(new IntVector3(start.x, 0, start.z));
            if (startTile.Type == RoomType.NoRoom)
            {
                Mod.LogWarning($"Discount shop: the old workshop {anchor} is not in a room; no shop.");
                Create(Mod.DiscountDesk.ID, anchor, Vector3.forward);
                return;
            }
            var solids = new List<Vector3>();
            using (EntityQuery appliances = EntityManager.CreateEntityQuery(typeof(CAppliance), typeof(CPosition)))
            using (NativeArray<CPosition> positions = appliances.ToComponentDataArray<CPosition>(Allocator.Temp))
                foreach (CPosition p in positions) solids.Add(p.Position);
            TycoonShopLayout plan = TycoonShopLayout.PlanTopCorner(TileManager, start, TycoonDiscountShop.MAX_STANDS, solids, TycoonDiscountShop.RegionMinZ);
            if (plan == null)
            {
                Mod.LogWarning("Discount shop: no room for it in the top of the garage; no shop.");
                Create(Mod.DiscountDesk.ID, anchor, Vector3.forward);
                return;
            }
            ShopRoom = plan.RoomID;
            ShopTiles = plan.Region;
            SellerAt = plan.KeeperAt;

            TycoonCompany.EnsureLoaded();
            TycoonRecipeShop.Owned(out int day);
            var crates = new List<CTycoonApplianceCrate>();
            int inGarage = 0;
            foreach (int id in TycoonCompany.CratesBought)
            {
                var bought = new CTycoonApplianceCrate { Appliance = id, Paid = true };
                Entity garageShelf = TycoonGarage.FreeShelf(EntityManager);
                if (garageShelf != Entity.Null) { TycoonGarage.Hold(EntityManager, garageShelf, CreateCrate(bought)); inGarage++; }
                else crates.Add(bought);
            }
            foreach (TycoonDiscountShop.Offer o in TycoonDiscountShop.Today(Mathf.Max(0, plan.Shelves.Count - crates.Count), day))
                crates.Add(new CTycoonApplianceCrate { Appliance = o.Appliance, Price = o.Price, Discount = o.Discount });
            if (crates.Count > plan.Shelves.Count) crates.RemoveRange(plan.Shelves.Count, crates.Count - plan.Shelves.Count);

            for (int i = 0; i < crates.Count; i++)
            {
                (int x, int z) s = plan.Shelves[i];
                Stand(new Vector3(s.x, 0f, s.z), crates[i]);
            }

            Entity desk = Create(Mod.DiscountDesk.ID, plan.CounterAt, plan.Away);
            EntityManager.AddComponent<CDoNotPersist>(desk);
            PlaceSeller(plan.KeeperAt, -plan.Away);

            Mod.LogInfo("Discount shop layout (the whole garage room; the shop is z >= " + TycoonDiscountShop.RegionMinZ + "): " + plan.Map(solids));
            var names = crates.ConvertAll(c => TycoonDiscountShop.NameOf(c.Appliance) + (c.Paid ? " (bought)" : $" {c.Price} (-{c.Discount}%)"));
            Mod.LogInfo($"Discount shop: room {plan.RoomID}, {plan.Region.Count} shop tile(s) of {plan.Room.Count}; desk {plan.CounterAt}, Whiskers {plan.KeeperAt}; "
                + $"{plan.InnerSpots} spot(s), {plan.Shelves.Count} stand(s) for day {day}; on the stands: {string.Join(", ", names)}; {inGarage} bought box(es) in the garage.");
        }

        private void Stand(Vector3 at, CTycoonApplianceCrate crate)
        {
            Entity stand = Create(AssetReference.DishPedestal, at, Vector3.forward);
            EntityManager.AddComponent<CTycoonDiscountStand>(stand);
            EntityManager.AddComponent<CDoNotPersist>(stand);
            TycoonGarage.Hold(EntityManager, stand, CreateCrate(crate));
        }

        internal Entity CreateCrate(CTycoonApplianceCrate crate)
        {
            Entity item = EntityManager.CreateEntity(typeof(CCreateItem), typeof(CHeldBy), typeof(CHome), typeof(CTycoonApplianceCrate), typeof(CDoNotPersist),
                typeof(CCrate), typeof(CCrateAppliance), typeof(CFranchiseNonKitchenItem));
            EntityManager.SetComponentData(item, new CCreateItem { ID = TycoonDiscountShop.CrateItemFor(crate.Appliance) });
            EntityManager.SetComponentData(item, crate);
            EntityManager.SetComponentData(item, new CCrateAppliance { Appliance = crate.Appliance });
            return item;
        }

        private void PlaceSeller(Vector3 at, Vector3 facing)
        {
            Entity cat = EntityManager.CreateEntity(typeof(CTycoonDiscountSeller), typeof(CCustomer), typeof(CCustomerState), typeof(CPosition),
                typeof(CPlayerCosmetics), typeof(CRequiresView), typeof(CMoveToLocation), typeof(CDoNotPersist));
            EntityManager.SetComponentData(cat, new CCustomer { Scale = 1f, Speed = 0f });
            EntityManager.SetComponentData(cat, new CPosition(at, Quaternion.LookRotation(facing)));
            EntityManager.SetComponentData(cat, new CMoveToLocation { Location = at, DesiredFacing = facing, StoppingDistance = 0f });
            Patches.SpecialCats.Mark(EntityManager, cat, Patches.SpecialCats.WHISKERS_HATS);
            EntityManager.SetComponentData(cat, new CRequiresView { Type = ViewType.CustomerCat });
        }
    }

    /// <summary>Whiskers's speech bubbles.</summary>
    internal static class TycoonWhiskersSpeech
    {
        internal static readonly Vector3 OFFSET = new Vector3(0f, -1f, -1f);

        internal static void Say(EntityManager em, float now, EntityQuery sellers, string line, float seconds)
        {
            if (sellers.IsEmpty) return;
            using NativeArray<CPosition> at = sellers.ToComponentDataArray<CPosition>(Allocator.Temp);
            ManageTycoonAnnouncements.Announce(em, now, at[0].Position + OFFSET, line, seconds);
        }
    }

    /// <summary>An unpaid crate carried out of Whiskers's corner goes back to its stand.</summary>
    public class TycoonDiscountShopGuardSystem : FranchiseSystem, IModSystem
    {
        private const float SPEECH_GAP = 3f;
        private EntityQuery _crates, _sellers, _stands;
        private float _spokeAt = -100f;

        protected override void Initialise()
        {
            base.Initialise();
            _crates = GetEntityQuery(new QueryHelper().All(typeof(CTycoonApplianceCrate), typeof(CHeldBy), typeof(CHome)));
            _sellers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonDiscountSeller), typeof(CPosition)));
            _stands = GetEntityQuery(new QueryHelper().All(typeof(CTycoonDiscountStand), typeof(CItemHolderFilter)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            if (!_stands.IsEmpty)
                using (NativeArray<Entity> stands = _stands.ToEntityArray(Allocator.Temp))
                    foreach (Entity stand in stands)
                    {
                        CItemHolderFilter filter = EntityManager.GetComponentData<CItemHolderFilter>(stand);
                        if (filter.AllowAny && !filter.NoDirectInsertion) continue;
                        filter.AllowAny = true;
                        filter.NoDirectInsertion = false;
                        EntityManager.SetComponentData(stand, filter);
                    }
            if (_crates.IsEmpty || TycoonDiscountShopSetupSystem.ShopRoom == TycoonDiscountShopSetupSystem.NO_ROOM) return;
            using NativeArray<Entity> crates = _crates.ToEntityArray(Allocator.Temp);
            foreach (Entity crate in crates)
            {
                CTycoonApplianceCrate data = EntityManager.GetComponentData<CTycoonApplianceCrate>(crate);
                if (data.Paid) continue;
                Entity holder = EntityManager.GetComponentData<CHeldBy>(crate).Holder;
                if (holder == Entity.Null || !EntityManager.Exists(holder) || Has<CTycoonDiscountStand>(holder) || !Require(holder, out CPosition at)) continue;
                if (TycoonDiscountShopSetupSystem.InShop(TileManager, at.Position)) continue;
                Entity home = EntityManager.GetComponentData<CHome>(crate).Holder;
                if (home == Entity.Null || !EntityManager.Exists(home)) continue;
                HolderHelpers.GoHome(EntityManager, crate);
                Mod.LogInfo($"Discount shop: an unpaid {TycoonDiscountShop.NameOf(data.Appliance)} left the shop; back on its stand.");
                if (Time.RealTotalTime - _spokeAt > SPEECH_GAP)
                {
                    _spokeAt = Time.RealTotalTime;
                    TycoonWhiskersSpeech.Say(EntityManager, Time.RealTotalTime, _sellers, TycoonDiscountDialogue.Say("stop-thief"), 4f);
                }
            }
        }
    }

    /// <summary>What a box holds: while a player looks at a crate's stand / shelf / cart or carries it.</summary>
    public class TycoonCrateInfoLabelSystem : FranchiseSystem, IModSystem
    {
        private const float KEEP_ALIVE = 0.3f;
        private EntityQuery _crates;
        private readonly Dictionary<Entity, Entity> _labels = new Dictionary<Entity, Entity>();
        private World _world;

        protected override void Initialise()
        {
            base.Initialise();
            _crates = GetEntityQuery(new QueryHelper().All(typeof(CTycoonApplianceCrate), typeof(CHeldBy)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _crates.IsEmpty) return;
            if (_world != World) { _world = World; _labels.Clear(); }
            float now = Time.RealTotalTime;
            using NativeArray<Entity> crates = _crates.ToEntityArray(Allocator.Temp);
            foreach (Entity crate in crates)
            {
                Entity holder = EntityManager.GetComponentData<CHeldBy>(crate).Holder;
                if (holder == Entity.Null || !EntityManager.Exists(holder) || !Require(holder, out CPosition at)) continue;
                if (!Has<CPlayer>(holder) && !Has<CBeingLookedAt>(holder)) continue;
                CTycoonApplianceCrate data = EntityManager.GetComponentData<CTycoonApplianceCrate>(crate);
                if (data.Paid) continue;
                string name = TycoonDiscountShop.NameOf(data.Appliance);
                if (name.Length > 26) name = name.Substring(0, 26);
                string text = data.Paid ? $"{name}\nBought" : $"{name}\n{data.Price} coins ({data.Discount}% off)";
                if (_labels.TryGetValue(crate, out Entity label) && EntityManager.Exists(label) && Require(label, out CTycoonAnnouncement a) && a.ExpiresAt > now)
                {
                    a.ExpiresAt = now + KEEP_ALIVE;
                    Set(label, a);
                    EntityManager.SetComponentData(label, new CPosition(ManageTycoonAnnouncements.ItemLabelAt(at.Position)));
                    continue;
                }
                _labels[crate] = ManageTycoonAnnouncements.AnnounceItemLabel(EntityManager, now, at.Position, text, KEEP_ALIVE);
            }
        }
    }

    /// <summary>Whiskers's desk menu: the box the player holds is noted for the menu.</summary>
    public class ManageDiscountIndicators : PlayerSpecificUIIndicator<CTycoonDiscountDesk, CDishSelectionInfo>, IModSystem
    {
        private const float SPEECH_SECONDS = 5f;
        private EntityQuery _sellers;

        protected override void Initialise()
        {
            base.Initialise();
            _sellers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonDiscountSeller), typeof(CPosition)));
        }

        protected override ViewType ViewType => ViewType.DishSelectorUI;

        protected override CDishSelectionInfo GetInfo(Entity source, CTycoonDiscountDesk selector, CTriggerPlayerSpecificUI trigger, CPlayer player)
        {
            var held = new UI.TycoonDiscountMenu.Held();
            if (Require(trigger.TriggerEntity, out CItemHolder hands) && hands.HeldItem != Entity.Null && Require(hands.HeldItem, out CTycoonApplianceCrate crate))
                held = new UI.TycoonDiscountMenu.Held { Appliance = crate.Appliance, Price = crate.Price, Discount = crate.Discount, Paid = crate.Paid };
            UI.TycoonDiscountMenu.Holding[player.ID] = held;
            PurchasingDeskHelpers.SendDesk(player.ID, $"{(int)Patches.PurchasingDeskMenu.DeskKind.Discount},{held.Appliance},{held.Price},{held.Discount},{(held.Paid ? 1 : 0)}");
            TycoonBank.Read(EntityManager);
            Mod.LogInfo($"Discount shop: player {player.ID} talks to Whiskers holding {(held.Appliance == 0 ? "nothing" : TycoonDiscountShop.NameOf(held.Appliance) + (held.Paid ? " (paid)" : $" ({held.Price} coins)"))}.");
            return new CDishSelectionInfo { Player = player, PlayerEntity = trigger.TriggerEntity };
        }

        protected override bool ShouldDismiss(CDishSelectionInfo info)
        {
            if (info.Dish == 0) return base.ShouldDismiss(info);
            Buy(info.PlayerEntity, info.Dish);
            return true;
        }

        private void Buy(Entity player, int applianceID)
        {
            if (!Require(player, out CItemHolder hands) || hands.HeldItem == Entity.Null || !Require(hands.HeldItem, out CTycoonApplianceCrate crate)
                || crate.Appliance != applianceID || crate.Paid)
            {
                Mod.LogWarning($"Discount shop: not sold, the player no longer holds the unpaid {TycoonDiscountShop.NameOf(applianceID)}.");
                return;
            }
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.Cash < crate.Price)
            {
                TycoonWhiskersSpeech.Say(EntityManager, Time.RealTotalTime, _sellers, $"Um... that's {crate.Price} coins. Sorry!", SPEECH_SECONDS);
                return;
            }
            TycoonCompany.Cash -= crate.Price;
            TycoonCompany.CratesBought.Add(applianceID);
            TycoonCompany.Save();
            crate.Paid = true;
            EntityManager.SetComponentData(hands.HeldItem, crate);
            int day = 0;
            TycoonRecipeShop.Owned(out day);
            TycoonPurchaseLog.Add(day, TycoonPurchaseLog.PlayerName(Require(player, out CPlayer p) ? p.ID : 0), "Whiskers: " + TycoonDiscountShop.NameOf(applianceID), crate.Price);
            TycoonWhiskersSpeech.Say(EntityManager, Time.RealTotalTime, _sellers, TycoonDiscountDialogue.Say("bought"), SPEECH_SECONDS);
            Mod.LogInfo($"Discount shop: sold {TycoonDiscountShop.NameOf(applianceID)} for {crate.Price} coins ({crate.Discount}% off; wallet now {TycoonCompany.Cash}).");
        }
    }

    /// <summary>HQ: Whiskers's tutorial: the first time a player in her corner comes within TUTORIAL_NEAR tiles of her.</summary>
    public class TycoonDiscountTutorialSystem : FranchiseSystem, IModSystem
    {
        public const float TUTORIAL_NEAR = 3.5f;
        private EntityQuery _players, _desks, _recipePopups;
        private int _nextPage = -1;

        protected override void Initialise()
        {
            base.Initialise();
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CPosition)));
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonDiscountDesk)));
            _recipePopups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _desks.IsEmpty || TycoonDiscountShopSetupSystem.ShopRoom == TycoonDiscountShopSetupSystem.NO_ROOM) return;
            if (_nextPage >= 0)
            {
                if (!_recipePopups.IsEmpty) return;
                if (_nextPage >= Customs.DiscountTutorialDish.PageCount || _nextPage >= Mod.DiscountTutorialPages.Count) { _nextPage = -1; return; }
                PopupUtilities.RequestManagedPopup(PopupType.Recipe, new CPopupRecipe { ID = Mod.DiscountTutorialPages[_nextPage] });
                _nextPage++;
                return;
            }
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.DiscountTutorialSeen || !TycoonPreferences.ShowCharacterTutorials || Mod.DiscountTutorialPages.Count == 0 || !_recipePopups.IsEmpty) return;
            using NativeArray<CPosition> positions = _players.ToComponentDataArray<CPosition>(Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i].Position;
                if (!TycoonDiscountShopSetupSystem.InShop(TileManager, p)) continue;
                Vector3 to = p - TycoonDiscountShopSetupSystem.SellerAt;
                to.y = 0f;
                if (to.magnitude > TUTORIAL_NEAR) continue;
                TycoonCompany.DiscountTutorialSeen = true;
                TycoonCompany.Save();
                _nextPage = 0;
                Mod.LogInfo($"Discount shop: a player came near Whiskers for the first time; showing her tutorial ({Customs.DiscountTutorialDish.PageCount} page(s)).");
                return;
            }
        }
    }

    /// <summary>Restaurant: boxes sent with the loading bay arrive as appliance parcels at the post, at night.</summary>
    public class TycoonCrateDeliverySystem : RestaurantSystem, IModSystem
    {
        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SIsNightTime>();
        }

        protected override void OnUpdate()
        {
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.CratesToDeliver.Count == 0) return;
            List<Vector3> postTiles = GetPostTiles(false);
            var envelopes = World.GetExistingSystem<ProvideStartingEnvelopes>();
            if (envelopes == null) return;
            int placedTile = 0;
            var delivered = new List<string>();
            while (TycoonCompany.CratesToDeliver.Count > 0)
            {
                int id = TycoonCompany.CratesToDeliver[0];
                if (!GameData.Main.TryGet<Appliance>(id, out Appliance _)) { TycoonCompany.CratesToDeliver.RemoveAt(0); continue; }
                if (!envelopes.FindTile(ref placedTile, postTiles, out Vector3 position)) { Mod.LogWarning("Discount shop: no free post tile; the other boxes arrive tomorrow."); break; }
                PostHelpers.CreateApplianceParcel(EntityManager, position, id);
                TycoonCompany.CratesToDeliver.RemoveAt(0);
                delivered.Add(TycoonDiscountShop.NameOf(id));
            }
            TycoonCompany.Save();
            if (delivered.Count == 0) return;
            Mod.LogInfo($"Discount shop: delivered {string.Join(", ", delivered)} as parcel(s).");
            string text = delivered.Count == 1 ? $"Whiskers's box arrived: {delivered[0]}" : $"{delivered.Count} boxes from Whiskers arrived";
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), text.Length > 58 ? text.Substring(0, 58) : text, 8f);
        }
    }
}
