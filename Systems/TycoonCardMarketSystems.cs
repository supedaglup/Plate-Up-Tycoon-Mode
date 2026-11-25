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
    /// <summary>HQ: the Card Market in the office, left of Smokey's recipe shop.</summary>
    public class TycoonCardMarketSetupSystem : FranchiseSystem, IModSystem
    {
        internal const int NO_ROOM = int.MinValue;
        internal static int MarketRoom = NO_ROOM;
        internal static Vector3 MarketDoor;
        internal static Vector3 DealerAt;

        private EntityQuery _desks;

        protected override void Initialise()
        {
            base.Initialise();
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardDesk)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            if (!_desks.IsEmpty || Mod.CardDesk == null) return;
            if (!HasSingleton<SSelectedLayoutPedestal>()) return;
            try { Build(); }
            catch (System.Exception e) { Mod.LogError("Card Market: could not build it: " + e); }
        }

        private void Build()
        {
            MarketRoom = NO_ROOM;
            Vector3 anchor = LobbyPositionAnchors.Office;
            (int x, int z) start = TycoonShopLayout.Key(anchor);
            CLayoutRoomTile startTile = TileManager.GetTile(new IntVector3(start.x, 0, start.z));
            if (startTile.Type == RoomType.NoRoom)
            {
                Mod.LogWarning($"Card Market: the office anchor {anchor} is not in a room; no market.");
                Create(Mod.CardDesk.ID, anchor, Vector3.forward);
                return;
            }
            var solids = new List<Vector3>();
            using (EntityQuery appliances = EntityManager.CreateEntityQuery(typeof(CAppliance), typeof(CPosition)))
            using (NativeArray<CPosition> positions = appliances.ToComponentDataArray<CPosition>(Allocator.Temp))
                foreach (CPosition p in positions) solids.Add(p.Position);
            TycoonShopLayout plan = TycoonShopLayout.PlanDisplay(TileManager, start, TycoonCardMarket.MAX_SHELVES, solids);
            if (plan == null)
            {
                Mod.LogWarning("Card Market: the office is too small; no market.");
                return;
            }
            MarketRoom = plan.RoomID;
            MarketDoor = plan.Door;
            DealerAt = plan.KeeperAt;

            TycoonCompany.EnsureLoaded();
            HashSet<int> owned = TycoonCardMarket.Owned(out int day);
            var cards = new List<(int card, bool paid)>();
            int inGarage = 0;
            foreach (int id in TycoonCompany.CardsBought)
            {
                Entity garageShelf = TycoonGarage.FreeShelf(EntityManager);
                if (garageShelf != Entity.Null) { TycoonGarage.Hold(EntityManager, garageShelf, CreateCard(id, true)); inGarage++; }
                else cards.Add((id, true));
            }
            List<Unlock> forSale = TycoonCardMarket.ForSale(owned);
            List<Unlock> heatForSale = TycoonCardMarket.HeatForSale(owned);
            foreach (Unlock u in TycoonCardMarket.Today(forSale, heatForSale, Mathf.Max(0, plan.Shelves.Count - cards.Count), day)) cards.Add((u.ID, false));
            if (cards.Count > plan.Shelves.Count) cards.RemoveRange(plan.Shelves.Count, cards.Count - plan.Shelves.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                (int x, int z) s = plan.Shelves[i];
                Shelf(new Vector3(s.x, 0f, s.z), cards[i].card, cards[i].paid);
            }

            Entity desk = Create(Mod.CardDesk.ID, plan.CounterAt, plan.Away);
            EntityManager.AddComponent<CDoNotPersist>(desk);
            PlaceDealer(plan.KeeperAt, -plan.Away);

            Mod.LogInfo("Card Market layout: " + plan.Map(solids));
            var names = cards.ConvertAll(c => TycoonCardMarket.NameOf(c.card) + (c.paid ? " (bought)" : ""));
            Mod.LogInfo($"Card Market: room {plan.RoomID}, {plan.Room.Count} tiles around {plan.Centre} (door {plan.Door}, {plan.Doors.Count} doorway tile(s), "
                + $"{plan.Occupied} appliance(s) already there kept reachable); desk {plan.CounterAt}, Chubs {plan.KeeperAt}; {plan.WallSpots} wall + {plan.InnerSpots} inner spot(s), "
                + $"{plan.Shelves.Count} pedestal(s) for day {day}; {forSale.Count} card(s) for sale; on the pedestals: {string.Join(", ", names)}; {inGarage} bought card(s) in the garage.");
        }

        private void Shelf(Vector3 at, int card, bool paid)
        {
            Entity shelf = Create(AssetReference.DishPedestal, at, Vector3.forward);
            EntityManager.AddComponent<CTycoonCardShelf>(shelf);
            EntityManager.AddComponent<CDoNotPersist>(shelf);
            if (Require(shelf, out CItemHolderFilter filter))
            {
                filter.AllowAny = true;
                filter.NoDirectInsertion = false;
                Set(shelf, filter);
            }
            TycoonGarage.Hold(EntityManager, shelf, CreateCard(card, paid));
        }

        internal Entity CreateCard(int card, bool paid)
        {
            Entity item = EntityManager.CreateEntity(typeof(CCreateItem), typeof(CHeldBy), typeof(CHome), typeof(CTycoonCardItem), typeof(CDoNotPersist), typeof(CFranchiseNonKitchenItem));
            EntityManager.SetComponentData(item, new CCreateItem { ID = TycoonCardMarket.ItemID });
            EntityManager.SetComponentData(item, new CTycoonCardItem { Card = card, Paid = paid });
            return item;
        }

        private void PlaceDealer(Vector3 at, Vector3 facing)
        {
            Entity cat = EntityManager.CreateEntity(typeof(CTycoonCardDealer), typeof(CCustomer), typeof(CCustomerState), typeof(CPosition),
                typeof(CPlayerCosmetics), typeof(CRequiresView), typeof(CMoveToLocation), typeof(CDoNotPersist));
            EntityManager.SetComponentData(cat, new CCustomer { Scale = 1f, Speed = 0f });
            EntityManager.SetComponentData(cat, new CPosition(at, Quaternion.LookRotation(facing)));
            EntityManager.SetComponentData(cat, new CMoveToLocation { Location = at, DesiredFacing = facing, StoppingDistance = 0f });
            Patches.SpecialCats.Mark(EntityManager, cat, Patches.SpecialCats.DEALER_HATS);
            EntityManager.SetComponentData(cat, new CRequiresView { Type = ViewType.CustomerCat });
        }
    }

    /// <summary>Chubs's speech bubbles.</summary>
    internal static class TycoonCardDealerSpeech
    {
        internal static readonly Vector3 OFFSET = new Vector3(0f, -1f, 0f);

        internal static void Say(EntityManager em, float now, EntityQuery dealers, string line, float seconds)
        {
            if (dealers.IsEmpty) return;
            using NativeArray<CPosition> at = dealers.ToComponentDataArray<CPosition>(Allocator.Temp);
            ManageTycoonAnnouncements.Announce(em, now, at[0].Position + OFFSET, line, seconds);
        }
    }

    /// <summary>An unpaid card carried out of the Card Market goes back to its pedestal.</summary>
    public class TycoonCardMarketGuardSystem : FranchiseSystem, IModSystem
    {
        private const float SPEECH_GAP = 3f;
        private EntityQuery _cards;
        private EntityQuery _dealers;
        private float _spokeAt = -100f;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardItem), typeof(CHeldBy), typeof(CHome)));
            _dealers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardDealer), typeof(CPosition)));
            _shelves = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardShelf), typeof(CItemHolderFilter)));
        }

        private EntityQuery _shelves;

        private void OpenShelves()
        {
            if (_shelves.IsEmpty) return;
            using NativeArray<Entity> shelves = _shelves.ToEntityArray(Allocator.Temp);
            foreach (Entity shelf in shelves)
            {
                CItemHolderFilter filter = EntityManager.GetComponentData<CItemHolderFilter>(shelf);
                if (filter.AllowAny && !filter.NoDirectInsertion) continue;
                filter.AllowAny = true;
                filter.NoDirectInsertion = false;
                EntityManager.SetComponentData(shelf, filter);
            }
        }

        private int _lastCount = -1, _lastShelves = -1;

        private void WatchCount()
        {
            int count, shelves;
            using (EntityQuery q = EntityManager.CreateEntityQuery(typeof(CTycoonCardItem))) count = q.CalculateEntityCount();
            using (EntityQuery q = EntityManager.CreateEntityQuery(typeof(CTycoonCardShelf))) shelves = q.CalculateEntityCount();
            if (count == _lastCount && shelves == _lastShelves) return;
            if (_lastCount >= 0) Mod.LogInfo($"Card Market: {count} card item(s) (was {_lastCount}), {shelves} pedestal(s) (was {_lastShelves}).");
            _lastCount = count;
            _lastShelves = shelves;
        }

        protected override void OnUpdate()
        {
            if (TycoonSession.IsActive) WatchCount();
            if (TycoonSession.IsActive) OpenShelves();
            if (!TycoonSession.IsActive || _cards.IsEmpty || TycoonCardMarketSetupSystem.MarketRoom == TycoonCardMarketSetupSystem.NO_ROOM) return;
            using NativeArray<Entity> cards = _cards.ToEntityArray(Allocator.Temp);
            foreach (Entity card in cards)
            {
                CTycoonCardItem data = EntityManager.GetComponentData<CTycoonCardItem>(card);
                if (data.Paid) continue;
                Entity holder = EntityManager.GetComponentData<CHeldBy>(card).Holder;
                if (holder == Entity.Null || !EntityManager.Exists(holder) || Has<CTycoonCardShelf>(holder) || !Require(holder, out CPosition at)) continue;
                CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(at.Position.x), 0, Mathf.RoundToInt(at.Position.z)));
                if (tile.Type != RoomType.NoRoom && tile.RoomID == TycoonCardMarketSetupSystem.MarketRoom) continue;

                Entity home = EntityManager.GetComponentData<CHome>(card).Holder;
                if (home == Entity.Null || !EntityManager.Exists(home)) continue;
                HolderHelpers.GoHome(EntityManager, card);
                Mod.LogInfo($"Card Market: an unpaid {TycoonCardMarket.NameOf(data.Card)} left the market; back on its pedestal.");
                if (Time.RealTotalTime - _spokeAt > SPEECH_GAP)
                {
                    _spokeAt = Time.RealTotalTime;
                    TycoonCardDealerSpeech.Say(EntityManager, Time.RealTotalTime, _dealers, TycoonCardDialogue.Say("stop-thief"), 4f);
                }
            }
        }
    }

    /// <summary>What a card is: while a player looks at a card's pedestal or carries the card.</summary>
    public class TycoonCardInfoLabelSystem : FranchiseSystem, IModSystem
    {
        private const float KEEP_ALIVE = 0.3f;
        private EntityQuery _cards;
        private EntityQuery _recipes;
        private readonly Dictionary<Entity, Entity> _labels = new Dictionary<Entity, Entity>();
        private World _world;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardItem), typeof(CHeldBy)));
            _recipes = GetEntityQuery(new QueryHelper().All(typeof(CTycoonRecipePaper), typeof(CHeldBy)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            if (_world != World) { _world = World; _labels.Clear(); }
            Labels(_cards);
            Labels(_recipes);
        }

        private void Labels(EntityQuery query)
        {
            if (query.IsEmpty) return;
            float now = Time.RealTotalTime;
            using NativeArray<Entity> cards = query.ToEntityArray(Allocator.Temp);
            foreach (Entity card in cards)
            {
                Entity holder = EntityManager.GetComponentData<CHeldBy>(card).Holder;
                if (holder == Entity.Null || !EntityManager.Exists(holder) || !Require(holder, out CPosition at)) continue;
                bool show = Has<CPlayer>(holder) || Has<CBeingLookedAt>(holder);
                if (!show) continue;

                string text = Require(card, out CTycoonCardItem data) ? Text(data)
                    : Require(card, out CTycoonRecipePaper paper) ? Text(paper) : null;
                if (text == null) continue;
                if (_labels.TryGetValue(card, out Entity label) && EntityManager.Exists(label) && Require(label, out CTycoonAnnouncement a) && a.ExpiresAt > now)
                {
                    a.ExpiresAt = now + KEEP_ALIVE;
                    Set(label, a);
                    EntityManager.SetComponentData(label, new CPosition(ManageTycoonAnnouncements.ItemLabelAt(at.Position)));
                    continue;
                }
                _labels[card] = ManageTycoonAnnouncements.AnnounceItemLabel(EntityManager, now, at.Position, text, KEEP_ALIVE);
            }
        }

        private static string Text(CTycoonRecipePaper paper)
        {
            string name = TycoonRecipeShop.NameOf(paper.Dish);
            if (name.Length > 24) name = name.Substring(0, 24);
            string rep = TycoonReputation.CardValue(paper.Dish).ToString("0.#");
            KitchenData.GameData.Main.TryGet<Dish>(paper.Dish, out Dish dish);
            if (paper.Paid) return $"{name}\nBought, +{rep} rep/serve";
            int price = dish != null ? TycoonRecipeShop.Price(dish) : 0;
            return $"{name}\n{price} coins, +{rep} rep/serve";
        }

        private static string Text(CTycoonCardItem data)
        {
            string name = TycoonCardMarket.NameOf(data.Card);
            if (name.Length > 24) name = name.Substring(0, 24);
            string rep = TycoonReputation.CardValue(data.Card).ToString("0.#");
            return data.Paid ? $"{name}\nBought, +{rep} rep/serve" : $"{name}\n{TycoonCardMarket.Price(data.Card)} coins, +{rep} rep/serve";
        }
    }

    /// <summary>Chubs's desk menu, Smokey's plumbing: when it opens, the card the player holds is noted for the menu.</summary>
    public class ManageCardDealerIndicators : PlayerSpecificUIIndicator<CTycoonCardDesk, CDishSelectionInfo>, IModSystem
    {
        private const float SPEECH_SECONDS = 5f;
        private EntityQuery _dealers;

        protected override void Initialise()
        {
            base.Initialise();
            _dealers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardDealer), typeof(CPosition)));
        }

        protected override ViewType ViewType => ViewType.DishSelectorUI;

        protected override CDishSelectionInfo GetInfo(Entity source, CTycoonCardDesk selector, CTriggerPlayerSpecificUI trigger, CPlayer player)
        {
            var held = new UI.TycoonCardMarketMenu.Held();
            if (Require(trigger.TriggerEntity, out CItemHolder hands) && hands.HeldItem != Entity.Null && Require(hands.HeldItem, out CTycoonCardItem card))
                held = new UI.TycoonCardMarketMenu.Held { Card = card.Card, Paid = card.Paid };
            if (held.Card != 0)
            {
                TycoonCompany.EnsureLoaded();
                held.Paid |= TycoonCompany.CardsBought.Contains(held.Card);
                held.Owned = !held.Paid && TycoonCardMarket.Owned(out _).Contains(held.Card);
            }
            UI.TycoonCardMarketMenu.Holding[player.ID] = held;
            PurchasingDeskHelpers.SendDesk(player.ID, $"{(int)Patches.PurchasingDeskMenu.DeskKind.Cards},{held.Card},{(held.Paid ? 1 : 0)},{(held.Owned ? 1 : 0)}");
            TycoonBank.Read(EntityManager);
            Mod.LogInfo($"Card Market: player {player.ID} talks to Chubs holding {(held.Card == 0 ? "nothing" : TycoonCardMarket.NameOf(held.Card) + (held.Paid ? " (paid)" : ""))}.");
            return new CDishSelectionInfo { Player = player, PlayerEntity = trigger.TriggerEntity };
        }

        protected override bool ShouldDismiss(CDishSelectionInfo info)
        {
            if (info.Dish == 0) return base.ShouldDismiss(info);
            Buy(info.PlayerEntity, info.Dish);
            return true;
        }

        private void Buy(Entity player, int cardID)
        {
            if (!Require(player, out CItemHolder hands) || hands.HeldItem == Entity.Null || !Require(hands.HeldItem, out CTycoonCardItem card)
                || card.Card != cardID || card.Paid)
            {
                Mod.LogWarning($"Card Market: not sold, the player no longer holds the unpaid {TycoonCardMarket.NameOf(cardID)}.");
                return;
            }
            if (!GameData.Main.TryGet<Unlock>(cardID, out Unlock unlock)) return;
            int price = TycoonCardMarket.Price(unlock);
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.Cash < price)
            {
                TycoonCardDealerSpeech.Say(EntityManager, Time.RealTotalTime, _dealers, "That's " + price + " coins. Come back richer!", SPEECH_SECONDS);
                return;
            }
            TycoonCompany.Cash -= price;
            if (!TycoonCompany.CardsBought.Contains(cardID)) TycoonCompany.CardsBought.Add(cardID);
            TycoonCompany.Save();
            card.Paid = true;
            EntityManager.SetComponentData(hands.HeldItem, card);
            TycoonCardDealerSpeech.Say(EntityManager, Time.RealTotalTime, _dealers, TycoonCardDialogue.Say("bought"), SPEECH_SECONDS);
            Mod.LogInfo($"Card Market: sold {unlock.Name} for {price} coins (wallet now {TycoonCompany.Cash}).");
        }
    }

    /// <summary>HQ: the Card Market's tutorial, the first time a player comes near Chubs.</summary>
    public class TycoonCardTutorialSystem : FranchiseSystem, IModSystem
    {
        public const float TUTORIAL_NEAR_DEALER = 3.5f;

        private EntityQuery _players;
        private EntityQuery _desks;
        private EntityQuery _recipePopups;
        private int _nextPage = -1;

        protected override void Initialise()
        {
            base.Initialise();
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CPosition)));
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardDesk)));
            _recipePopups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _desks.IsEmpty || TycoonCardMarketSetupSystem.MarketRoom == TycoonCardMarketSetupSystem.NO_ROOM) return;

            if (_nextPage >= 0)
            {
                if (!_recipePopups.IsEmpty) return;
                if (_nextPage >= Customs.CardTutorialDish.PageCount || _nextPage >= Mod.CardTutorialPages.Count) { _nextPage = -1; return; }
                PopupUtilities.RequestManagedPopup(PopupType.Recipe, new CPopupRecipe { ID = Mod.CardTutorialPages[_nextPage] });
                _nextPage++;
                return;
            }

            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.CardTutorialSeen || !TycoonPreferences.ShowCharacterTutorials || Mod.CardTutorialPages.Count == 0 || !_recipePopups.IsEmpty) return;

            using NativeArray<CPosition> positions = _players.ToComponentDataArray<CPosition>(Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i].Position;
                CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(p.x), 0, Mathf.RoundToInt(p.z)));
                if (tile.Type == RoomType.NoRoom || tile.RoomID != TycoonCardMarketSetupSystem.MarketRoom) continue;
                Vector3 toDealer = p - TycoonCardMarketSetupSystem.DealerAt;
                toDealer.y = 0f;
                if (toDealer.magnitude > TUTORIAL_NEAR_DEALER) continue;

                TycoonCompany.CardTutorialSeen = true;
                TycoonCompany.Save();
                _nextPage = 0;
                Mod.LogInfo($"Card Market: a player walked into the market for the first time; showing its tutorial ({Customs.CardTutorialDish.PageCount} page(s)).");
                return;
            }
        }
    }

    /// <summary>Restaurant: cards sent with the loading bay become Tycoon cards, switched off, under the current star.</summary>
    public class TycoonCardDeliverySystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _cards;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STycoonCards>();
        }

        protected override void OnUpdate()
        {
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.CardsToDeliver.Count == 0 || !GetSingleton<STycoonCards>().StartingCardsGiven) return;

            var owned = new HashSet<int>();
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                for (int i = 0; i < cards.Length; i++) owned.Add(cards[i].UnlockID);

            var added = new List<string>();
            foreach (int id in TycoonCompany.CardsToDeliver)
            {
                if (owned.Contains(id) || !GameData.Main.TryGet<Unlock>(id, out Unlock unlock)) continue;
                owned.Add(id);
                Entity card = EntityManager.CreateEntity(typeof(CTycoonCard));
                EntityManager.SetComponentData(card, new CTycoonCard { UnlockID = id, Star = Mathf.Max(1, TycoonEconomy.CurrentStars), Enabled = false, Vanilla = false });
                EntityManager.AddComponent<CTycoonCardBought>(card);
                added.Add(unlock.Name);
            }
            TycoonCompany.CardsToDeliver.Clear();
            TycoonCompany.Save();
            if (added.Count == 0) return;
            Mod.LogInfo($"Card Market: delivered {string.Join(", ", added)} as card(s), switched off.");
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false),
                $"New card{(added.Count == 1 ? "" : "s")} at the Management Desk: {string.Join(", ", added)}", 10f);
        }
    }
}
