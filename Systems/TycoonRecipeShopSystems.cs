using Kitchen;
using Kitchen.Layouts;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>HQ: the recipe shop in the old trophy room.</summary>
    public class TycoonRecipeShopSetupSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery _desks;
        private EntityQuery _carts;

        protected override void Initialise()
        {
            base.Initialise();
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonChefDesk)));
            _carts = GetEntityQuery(new QueryHelper().All(typeof(CItemPedestal), typeof(CItemHolderFilter)));
            _garageShelves = GetEntityQuery(new QueryHelper().All(typeof(CPersistentItemStorageLocation), typeof(CItemHolderFilter)).None(typeof(CCrateInbox)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            OpenCarts();
            if (!_desks.IsEmpty || Mod.ChefDesk == null) return;
            if (!HasSingleton<SSelectedLayoutPedestal>()) return;
            try { Build(); }
            catch (System.Exception e) { Mod.LogError("Recipe shop: could not build it: " + e); }
        }

        private EntityQuery _garageShelves;

        private void OpenCarts()
        {
            Open(_carts, "a loading bay cart");
            Open(_garageShelves, "a garage shelf");
        }

        private void Open(EntityQuery holders, string what)
        {
            if (holders.IsEmpty) return;
            using NativeArray<Entity> carts = holders.ToEntityArray(Allocator.Temp);
            foreach (Entity cart in carts)
            {
                CItemHolderFilter filter = EntityManager.GetComponentData<CItemHolderFilter>(cart);
                if (filter.AllowAny && !filter.NoDirectInsertion) continue;
                filter.AllowAny = true;
                filter.NoDirectInsertion = false;
                EntityManager.SetComponentData(cart, filter);
                Mod.LogInfo($"Recipe shop: {what} now takes recipe papers and blueprints.");
            }
        }

        private static readonly Vector3[] StatsPieces =
        {
            new Vector3(0f, 0f, -3f), new Vector3(-2.5f, 0f, -3.5f), new Vector3(-2.5f, 0f, -2f), new Vector3(2f, 0f, -1f),
            new Vector3(-2.5f, 0f, 0f), new Vector3(-2f, 0f, 0f), new Vector3(0f, 0f, -2f), new Vector3(-1f, 0f, -2f),
        };
        private static readonly Vector3[] Bedrooms = { new Vector3(10f, 0f, 6f), new Vector3(10f, 0f, 3f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, -3f) };

        internal const int NO_ROOM = int.MinValue;
        internal static int ShopRoom = NO_ROOM;
        internal static Vector3 ShopDoor;

        private CLayoutRoomTile Tile(int x, int z) => TileManager.GetTile(new IntVector3(x, 0, z));

        private bool FindTrophyRoom(out (int x, int z) start)
        {
            start = default;
            var bedrooms = new HashSet<int>();
            foreach (Vector3 b in Bedrooms)
            {
                CLayoutRoomTile t = Tile(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.z));
                if (t.Type != RoomType.NoRoom) bedrooms.Add(t.RoomID);
            }
            var votes = new Dictionary<int, int>();
            var first = new Dictionary<int, (int x, int z)>();
            Vector3 anchor = LobbyPositionAnchors.Stats;
            foreach (Vector3 offset in StatsPieces)
            {
                Vector3 p = anchor + offset;
                var key = (Mathf.FloorToInt(p.x + 0.5f), Mathf.FloorToInt(p.z + 0.5f));
                CLayoutRoomTile t = Tile(key.Item1, key.Item2);
                if (t.Type == RoomType.NoRoom || bedrooms.Contains(t.RoomID)) continue;
                votes[t.RoomID] = votes.TryGetValue(t.RoomID, out int n) ? n + 1 : 1;
                if (!first.ContainsKey(t.RoomID)) first[t.RoomID] = key;
            }
            LogRoomMap(bedrooms);
            int best = NO_ROOM, bestVotes = 0;
            foreach (KeyValuePair<int, int> v in votes) if (v.Value > bestVotes) { best = v.Key; bestVotes = v.Value; }
            if (bestVotes == 0) return false;
            start = first[best];
            Mod.LogInfo($"Recipe shop: the trophy room is room {best} ({bestVotes} of the old trophy room's pieces stood in it; bedrooms are {string.Join(",", bedrooms)}).");
            return true;
        }

        private void LogRoomMap(HashSet<int> bedrooms)
        {
            var map = new System.Text.StringBuilder("Recipe shop: HQ rooms around the trophy room (x -2..14 left to right, z 2 at the top to -12; B = bedroom, # = the anchor):\n");
            Vector3 a = LobbyPositionAnchors.Stats;
            for (int z = 2; z >= -12; z--)
            {
                map.Append(z.ToString().PadLeft(4)).Append(' ');
                for (int x = -2; x <= 14; x++)
                {
                    CLayoutRoomTile t = Tile(x, z);
                    char c = t.Type == RoomType.NoRoom ? '.' : bedrooms.Contains(t.RoomID) ? 'B' : (char)('a' + (t.RoomID % 26 + 26) % 26);
                    if (x == Mathf.RoundToInt(a.x) && z == Mathf.RoundToInt(a.z)) c = '#';
                    map.Append(c);
                }
                map.Append('\n');
            }
            Mod.LogInfo(map.ToString());
        }

        private void Build()
        {
            if (!FindTrophyRoom(out (int x, int z) start))
            {
                Mod.LogWarning("Recipe shop: the trophy room was not found; no shop.");
                return;
            }
            TycoonShopLayout plan = TycoonShopLayout.PlanDense(TileManager, start, TycoonRecipeShop.MAX_SHELVES)
                ?? TycoonShopLayout.Plan(TileManager, start, TycoonRecipeShop.MAX_SHELVES, protectOccupants: false);
            if (plan == null)
            {
                Mod.LogWarning("Recipe shop: the trophy room is too small; no shop.");
                return;
            }
            int roomID = plan.RoomID;
            ShopRoom = roomID;
            ShopDoor = plan.Door;
            List<(int x, int z)> shelves = plan.Shelves;
            Vector3 counterAt = plan.CounterAt, chefAt = plan.KeeperAt, away = plan.Away, centre = plan.Centre, door = plan.Door;
            var room = plan.Room;
            var doors = plan.Doors;

            TycoonCompany.EnsureLoaded();
            HashSet<int> owned = TycoonRecipeShop.Owned(out int day);
            var papers = new List<(int dish, bool paid)>();
            int inGarage = 0;
            foreach (int id in TycoonCompany.RecipesBought)
            {
                if (owned.Contains(id)) continue;
                Entity garageShelf = TycoonGarage.FreeShelf(EntityManager);
                if (garageShelf != Entity.Null) { TycoonGarage.Hold(EntityManager, garageShelf, CreatePaper(id, true)); inGarage++; }
                else papers.Add((id, true));
            }
            foreach (int id in TycoonCompany.RecipesBought) owned.Add(id);
            List<Dish> forSale = TycoonRecipeShop.ForSale(owned);

            Centre = centre;
            Door = door;
            var positions = shelves.ConvertAll(sp => new Vector3(sp.x, 0f, sp.z));
            foreach (Dish d in TycoonRecipeShop.Today(forSale, Mathf.Max(0, positions.Count - papers.Count), day)) papers.Add((d.ID, false));
            if (papers.Count > positions.Count) papers.RemoveRange(positions.Count, papers.Count - positions.Count);
            for (int i = 0; i < papers.Count; i++)
            {
                Shelf(positions[i], Vector3.forward, papers[i].dish, papers[i].paid);
            }
            Mod.LogInfo($"Recipe shop: {positions.Count} spot(s), {forSale.Count} recipe(s) to pick from.");

            Entity desk = Create(Mod.ChefDesk.ID, counterAt, away);
            EntityManager.AddComponent<CDoNotPersist>(desk);
            PlaceChef(chefAt, -away);

            var names = papers.ConvertAll(p => TycoonRecipeShop.NameOf(p.dish) + (p.paid ? " (bought)" : ""));
            Mod.LogInfo($"Recipe shop: room {roomID}, {room.Count} tiles around {centre} (door {door}, {doors.Count} doorway tile(s)); counter {counterAt}, Smokey {chefAt}; "
                + $"{plan.WallSpots} wall + {plan.InnerSpots} inner spot(s), {shelves.Count} shelf/shelves for day {day}; {forSale.Count} recipe(s) for sale; on the shelves: {string.Join(", ", names)}; {inGarage} bought recipe(s) in the garage.");
        }

        private Entity Shelf(Vector3 at, Vector3 facing, int dish, bool paid)
        {
            Entity shelf = Create(AssetReference.DishPedestal, at, facing);
            EntityManager.AddComponent<CTycoonRecipeShelf>(shelf);
            EntityManager.AddComponent<CDoNotPersist>(shelf);
            TycoonGarage.Hold(EntityManager, shelf, CreatePaper(dish, paid));
            return shelf;
        }

        private void Sign(Vector3 at, string text) =>
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, at + new Vector3(0f, 0.4f, 0f), text, 1000000f);

        internal static Vector3 Centre, Door;

        internal Entity CreatePaper(int dish, bool paid)
        {
            Entity paper = EntityManager.CreateEntity(typeof(CCreateItem), typeof(CHeldBy), typeof(CHome), typeof(CDishChoice), typeof(CTycoonRecipePaper), typeof(CDoNotPersist));
            bool main = GameData.Main.TryGet<Dish>(dish, out Dish d, false) && TycoonRecipeShop.IsMainDish(d) && (object)d.IconPrefab != null;
            EntityManager.SetComponentData(paper, new CCreateItem { ID = main || TycoonCardMarket.ItemID == 0 ? AssetReference.DishPaper : TycoonCardMarket.ItemID });
            EntityManager.SetComponentData(paper, new CDishChoice { Dish = dish });
            EntityManager.SetComponentData(paper, new CTycoonRecipePaper { Dish = dish, Paid = paid });
            return paper;
        }

        private void PlaceChef(Vector3 at, Vector3 facing)
        {
            Entity chef = EntityManager.CreateEntity(typeof(CTycoonChef), typeof(CCustomer), typeof(CCustomerState), typeof(CPosition),
                typeof(CPlayerCosmetics), typeof(CRequiresView), typeof(CMoveToLocation), typeof(CDoNotPersist));
            EntityManager.SetComponentData(chef, new CCustomer { Scale = 1f, Speed = 0f });
            EntityManager.SetComponentData(chef, new CPosition(at, Quaternion.LookRotation(facing)));
            EntityManager.SetComponentData(chef, new CMoveToLocation { Location = at, DesiredFacing = facing, StoppingDistance = 0f });
            Patches.SpecialCats.Mark(EntityManager, chef, Patches.SpecialCats.CHEF_HATS);
            EntityManager.SetComponentData(chef, new CRequiresView { Type = ViewType.CustomerCat });
        }
    }

    /// <summary>An unpaid recipe carried out of the recipe shop goes back to its shelf.</summary>
    public class TycoonRecipeShopGuardSystem : FranchiseSystem, IModSystem
    {
        private const float SPEECH_GAP = 3f;
        private static readonly Vector3 SPEECH_OFFSET = new Vector3(-2f, -1f, 0f);
        private EntityQuery _papers;
        private EntityQuery _chefs;
        private float _spokeAt = -100f;

        protected override void Initialise()
        {
            base.Initialise();
            _papers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonRecipePaper), typeof(CHeldBy), typeof(CHome)));
            _chefs = GetEntityQuery(new QueryHelper().All(typeof(CTycoonChef), typeof(CPosition)));
            _shelves = GetEntityQuery(new QueryHelper().All(typeof(CTycoonRecipeShelf), typeof(CItemHolderFilter)));
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

        protected override void OnUpdate()
        {
            if (TycoonSession.IsActive) OpenShelves();
            if (!TycoonSession.IsActive || _papers.IsEmpty || TycoonRecipeShopSetupSystem.ShopRoom == TycoonRecipeShopSetupSystem.NO_ROOM) return;
            using NativeArray<Entity> papers = _papers.ToEntityArray(Allocator.Temp);
            foreach (Entity paper in papers)
            {
                if (EntityManager.GetComponentData<CTycoonRecipePaper>(paper).Paid) continue;
                Entity holder = EntityManager.GetComponentData<CHeldBy>(paper).Holder;
                if (holder == Entity.Null || !EntityManager.Exists(holder) || Has<CTycoonRecipeShelf>(holder) || !Require(holder, out CPosition at)) continue;
                CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(at.Position.x), 0, Mathf.RoundToInt(at.Position.z)));
                if (tile.Type != RoomType.NoRoom && tile.RoomID == TycoonRecipeShopSetupSystem.ShopRoom) continue;

                Entity home = EntityManager.GetComponentData<CHome>(paper).Holder;
                if (home == Entity.Null || !EntityManager.Exists(home)) continue;
                HolderHelpers.GoHome(EntityManager, paper);
                Mod.LogInfo($"Recipe shop: an unpaid {TycoonRecipeShop.NameOf(EntityManager.GetComponentData<CTycoonRecipePaper>(paper).Dish)} left the shop; back on its shelf.");
                if (Time.RealTotalTime - _spokeAt > SPEECH_GAP && !_chefs.IsEmpty)
                {
                    _spokeAt = Time.RealTotalTime;
                    using NativeArray<CPosition> chef = _chefs.ToComponentDataArray<CPosition>(Allocator.Temp);
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, chef[0].Position + SPEECH_OFFSET, TycoonChefDialogue.Say("stop-thief"), 4f);
                }
            }
        }
    }

    /// <summary>Smokey's counter menu, the bank's plumbing.</summary>
    public class ManageChefIndicators : PlayerSpecificUIIndicator<CTycoonChefDesk, CDishSelectionInfo>, IModSystem
    {
        private const float SPEECH_SECONDS = 5f;
        private static readonly Vector3 SPEECH_OFFSET = new Vector3(-2f, -1f, 0f);

        private EntityQuery _chefs;

        protected override void Initialise()
        {
            base.Initialise();
            _chefs = GetEntityQuery(new QueryHelper().All(typeof(CTycoonChef), typeof(CPosition)));
        }

        protected override ViewType ViewType => ViewType.DishSelectorUI;

        protected override CDishSelectionInfo GetInfo(Entity source, CTycoonChefDesk selector, CTriggerPlayerSpecificUI trigger, CPlayer player)
        {
            var held = new UI.TycoonChefMenu.Held();
            if (Require(trigger.TriggerEntity, out CItemHolder hands) && hands.HeldItem != Entity.Null && Require(hands.HeldItem, out CTycoonRecipePaper paper))
                held = new UI.TycoonChefMenu.Held { Dish = paper.Dish, Paid = paper.Paid };
            if (held.Dish != 0)
            {
                TycoonCompany.EnsureLoaded();
                held.Paid |= TycoonCompany.RecipesBought.Contains(held.Dish);
                held.Owned = TycoonRecipeShop.Owned(out _).Contains(held.Dish);
            }
            UI.TycoonChefMenu.Holding[player.ID] = held;
            PurchasingDeskHelpers.SendDesk(player.ID, $"{(int)Patches.PurchasingDeskMenu.DeskKind.Chef},{held.Dish},{(held.Paid ? 1 : 0)},{(held.Owned ? 1 : 0)}");
            TycoonBank.Read(EntityManager);
            Mod.LogInfo($"Recipe shop: player {player.ID} talks to Smokey holding {(held.Dish == 0 ? "nothing" : TycoonRecipeShop.NameOf(held.Dish) + (held.Paid ? " (paid)" : ""))}.");
            return new CDishSelectionInfo { Player = player, PlayerEntity = trigger.TriggerEntity };
        }

        protected override bool ShouldDismiss(CDishSelectionInfo info)
        {
            if (info.Dish == 0) return base.ShouldDismiss(info);
            Buy(info.PlayerEntity, info.Dish);
            return true;
        }

        private void Buy(Entity player, int dish)
        {
            if (!Require(player, out CItemHolder hands) || hands.HeldItem == Entity.Null || !Require(hands.HeldItem, out CTycoonRecipePaper paper)
                || paper.Dish != dish || paper.Paid)
            {
                Mod.LogWarning($"Recipe shop: not sold, the player no longer holds the unpaid {TycoonRecipeShop.NameOf(dish)}.");
                return;
            }
            if (!GameData.Main.TryGet<Dish>(dish, out Dish recipe)) return;
            int price = TycoonRecipeShop.Price(recipe);
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.Cash < price)
            {
                Speak(TycoonChefDialogue.Say("too-poor").Replace("{recipe}", recipe.Name).Replace("{price}", price.ToString()));
                return;
            }
            TycoonCompany.Cash -= price;
            if (!TycoonCompany.RecipesBought.Contains(dish)) TycoonCompany.RecipesBought.Add(dish);
            TycoonCompany.Save();
            paper.Paid = true;
            EntityManager.SetComponentData(hands.HeldItem, paper);
            Speak(TycoonChefDialogue.Say("bought"));
            Mod.LogInfo($"Recipe shop: sold {recipe.Name} for {price} coins (wallet now {TycoonCompany.Cash}).");
        }

        private void Speak(string line)
        {
            if (_chefs.IsEmpty) return;
            using NativeArray<CPosition> positions = _chefs.ToComponentDataArray<CPosition>(Allocator.Temp);
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, positions[0].Position + SPEECH_OFFSET, line, SPEECH_SECONDS);
        }
    }

    /// <summary>Restaurant: recipes sent with the loading bay become cards, switched off, under the current star.</summary>
    public class TycoonRecipeDeliverySystem : RestaurantSystem, IModSystem
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
            if (TycoonCompany.RecipesToDeliver.Count == 0 || !GetSingleton<STycoonCards>().StartingCardsGiven) return;

            var owned = new HashSet<int>();
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                for (int i = 0; i < cards.Length; i++) owned.Add(cards[i].UnlockID);

            var added = new List<string>();
            foreach (int id in TycoonCompany.RecipesToDeliver)
            {
                if (owned.Contains(id) || !GameData.Main.TryGet<Dish>(id, out Dish dish)) continue;
                owned.Add(id);
                Entity card = EntityManager.CreateEntity(typeof(CTycoonCard));
                EntityManager.SetComponentData(card, new CTycoonCard { UnlockID = id, Star = Mathf.Max(1, TycoonEconomy.CurrentStars), Enabled = false, Vanilla = false });
                EntityManager.AddComponent<CTycoonCardBought>(card);
                added.Add(dish.Name);
            }
            TycoonCompany.RecipesToDeliver.Clear();
            TycoonCompany.Save();
            if (added.Count == 0) return;
            Mod.LogInfo($"Recipe shop: delivered {string.Join(", ", added)} as card(s), switched off.");
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false),
                $"New recipe card{(added.Count == 1 ? "" : "s")} at the Management Desk: {string.Join(", ", added)}", 10f);
        }
    }
}
