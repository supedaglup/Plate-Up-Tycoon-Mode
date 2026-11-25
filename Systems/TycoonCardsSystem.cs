using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Host side: the restaurant's cards.</summary>
    public class TycoonCardsSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _cards;
        private EntityQuery _requests;
        private EntityQuery _vanillaUnlocks;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            _requests = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardToggleRequest)));
            _vanillaUnlocks = GetEntityQuery(new QueryHelper().All(typeof(CProgressionUnlock)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            STycoonCards state = HasSingleton<STycoonCards>() ? GetSingleton<STycoonCards>() : default;
            if (!state.StartingCardsGiven && Mod.TakeOutCard != null)
            {
                STycoonRestaurantOrigin origin = TycoonNewRestaurant.Claim(EntityManager);
                AddCard(Mod.TakeOutCard.ID, 0, enabled: !origin.FromBlueprint, vanilla: false);
                AddCard(origin.StartingDish, 0, enabled: true, vanilla: false);
                if (!origin.FromBlueprint) AddCoffeeShopCard(enabled: true);
                state.StartingCardsGiven = true;
                Set(state);
                Mod.LogInfo($"Cards: the run starts with '{Customs.TakeOutCard.NAME}' ({(origin.FromBlueprint ? "off" : "on")}) and its starting recipe, {TycoonRecipeShop.NameOf(origin.StartingDish)} (on).");
            }
            else if (state.StartingCardsGiven && !TycoonNewRestaurant.Peek(EntityManager).FromBlueprint && !HasStartingRecipeCard()) AddStartingRecipeToOldRestaurant();

            if (state.StartingCardsGiven)
            {
                if (!TycoonNewRestaurant.Peek(EntityManager).FromBlueprint && !HasCard(Mod.CoffeeShopCard) && !_coffeeShopMissing)
                    AddCoffeeShopCard(enabled: Mod.TakeOutCard != null && TycoonCards.IsEnabled(EntityManager, Mod.TakeOutCard.ID));
                GiveFastCasualCard();
                GiveStarRewards();
                KeepPathCardsInStep();
            }

            RecordVanillaCards();

            if (!_requests.IsEmpty)
                HandleRequests();

            if (Time.RealTotalTime >= _nextPublish) { _nextPublish = Time.RealTotalTime + 0.5f; TycoonCards.Publish(EntityManager); }
        }

        private float _nextPublish;

        private Entity AddCard(int unlockID, int star, bool enabled, bool vanilla)
        {
            Entity card = EntityManager.CreateEntity(typeof(CTycoonCard));
            EntityManager.SetComponentData(card, new CTycoonCard { UnlockID = unlockID, Star = star, Enabled = enabled, Vanilla = vanilla });
            return card;
        }

        private bool _coffeeShopMissing;

        private bool HasCard(int unlockID)
        {
            using NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
            for (int i = 0; i < cards.Length; i++) if (cards[i].UnlockID == unlockID) return true;
            return false;
        }

        private void AddCoffeeShopCard(bool enabled)
        {
            if (!GameData.Main.TryGet<Unlock>(Mod.CoffeeShopCard, out Unlock coffeeShop, false))
            {
                _coffeeShopMissing = true;
                Mod.LogWarning($"Cards: the game's Coffee Shop card ({Mod.CoffeeShopCard}) was not found; the coffee stand goes without it.");
                return;
            }
            AddCard(coffeeShop.ID, 0, enabled: enabled, vanilla: false);
            Mod.LogInfo($"Cards: this coffee stand now has the '{coffeeShop.Name}' card ({(enabled ? "on" : "off")}).");
        }

        private void GiveFastCasualCard()
        {
            if (Mod.FastCasualCard == null || Mod.TakeOutCard == null) return;
            if (TycoonEconomy.CurrentStars < TycoonEconomy.FAST_CASUAL_STAR) return;
            if (HasCard(Mod.FastCasualCard.ID) || !HasCard(Mod.TakeOutCard.ID)) return;
            AddCard(Mod.FastCasualCard.ID, TycoonEconomy.CurrentStars, enabled: false, vanilla: false);
            Mod.LogInfo($"Cards: {TycoonEconomy.FAST_CASUAL_STAR} stars - '{Customs.FastCasualCard.NAME}' added to the Take-Out path (off).");
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), "New card: " + Customs.FastCasualCard.NAME);
        }

        private void GiveStarRewards()
        {
            int stars = TycoonEconomy.CurrentStars;
            if (stars >= TycoonUnlocks.UPGRADES_STAR && Mod.UpgradedAppliancesCard != null && !HasCard(Mod.UpgradedAppliancesCard.ID))
                Reward(Mod.UpgradedAppliancesCard.ID, TycoonUnlocks.UPGRADES_STAR, true, Customs.UpgradedAppliancesCard.NAME);
            if (stars >= TycoonUnlocks.PRICING_STAR && Mod.DishPricingCard != null && !HasCard(Mod.DishPricingCard.ID))
                Reward(Mod.DishPricingCard.ID, TycoonUnlocks.PRICING_STAR, true, Customs.DishPricingCard.NAME);
            if (stars >= TycoonUnlocks.QUALITY_STAR && Mod.IngredientQualityCard != null && !HasCard(Mod.IngredientQualityCard.ID))
                Reward(Mod.IngredientQualityCard.ID, TycoonUnlocks.QUALITY_STAR, true, Customs.IngredientQualityCard.NAME);
            if (stars >= TycoonUnlocks.HOURS_STAR && Mod.BusinessHoursCard != null && !HasCard(Mod.BusinessHoursCard.ID))
                Reward(Mod.BusinessHoursCard.ID, TycoonUnlocks.HOURS_STAR, true, Customs.BusinessHoursCard.NAME);
            if (stars >= TycoonUnlocks.THEMES_STAR)
                foreach (ThemeUnlock theme in PrimaryThemes())
                    if (!HasCard(theme.ID)) Reward(theme.ID, TycoonUnlocks.THEMES_STAR, false, theme.Name);
            if (Mod.FiveStarPricesCard != null)
            {
                bool has = HasCard(Mod.FiveStarPricesCard.ID);
                if (stars >= TycoonUnlocks.FIVE_STAR && !has) Reward(Mod.FiveStarPricesCard.ID, TycoonUnlocks.FIVE_STAR, true, Customs.FiveStarPricesCard.NAME);
                else if (stars < TycoonUnlocks.FIVE_STAR && has)
                {
                    using NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp);
                    foreach (Entity e in entities)
                        if (EntityManager.GetComponentData<CTycoonCard>(e).UnlockID == Mod.FiveStarPricesCard.ID) EntityManager.DestroyEntity(e);
                    Mod.LogInfo($"Cards: '{Customs.FiveStarPricesCard.NAME}' removed (the restaurant lost its fifth star).");
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), "Five-Star Menu Prices lost with the star");
                }
            }
        }

        private void Reward(int id, int star, bool on, string name)
        {
            AddCard(id, star, enabled: on, vanilla: false);
            Mod.LogInfo($"Cards: {star}-star reward '{name}' added ({(on ? "on" : "off")}).");
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), $"{star}-star reward: {name}");
        }

        private static List<ThemeUnlock> _themes;

        internal static List<ThemeUnlock> PrimaryThemes()
        {
            if (_themes != null) return _themes;
            _themes = new List<ThemeUnlock>();
            foreach (ThemeUnlock t in GameData.Main.Get<ThemeUnlock>()) if (t != null && t.IsPrimary) _themes.Add(t);
            _themes.Sort((a, b) => a.ID.CompareTo(b.ID));
            Mod.LogInfo($"Cards: primary theme cards: {string.Join(", ", _themes.ConvertAll(t => $"{t.Name} ({t.Type})"))}.");
            return _themes;
        }

        internal static bool IsPrimaryTheme(int id) => PrimaryThemes().Exists(t => t.ID == id);

        private void KeepPathCardsInStep()
        {
            KeepDiningCardsInStep();
            if (Mod.FastCasualCard == null || Mod.TakeOutCard == null) return;
            if (TycoonCards.IsEnabled(EntityManager, Mod.TakeOutCard.ID)) return;
            using NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                CTycoonCard c = EntityManager.GetComponentData<CTycoonCard>(entities[i]);
                if (c.UnlockID != Mod.FastCasualCard.ID || !c.Enabled) continue;
                c.Enabled = false;
                EntityManager.SetComponentData(entities[i], c);
                Mod.LogInfo($"Cards: '{Customs.FastCasualCard.NAME}' switched off with Take-Out (it needs Take-Out on).");
            }
        }

        private void KeepDiningCardsInStep()
        {
            if (TycoonCards.DiningPossible(EntityManager)) return;
            using NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                CTycoonCard c = EntityManager.GetComponentData<CTycoonCard>(entities[i]);
                if (!c.Enabled || c.Vanilla || !TycoonCards.IsDiningCard(c.UnlockID)) continue;
                c.Enabled = false;
                EntityManager.SetComponentData(entities[i], c);
                Mod.LogInfo($"Cards: '{TycoonCardMarket.NameOf(c.UnlockID)}' switched off: with Take-Out on and Fast Casual Dining off, nobody sits down to eat.");
            }
        }

        private bool HasStartingRecipeCard()
        {
            using NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
            for (int i = 0; i < cards.Length; i++) if (cards[i].UnlockID == Mod.STARTING_DISH) return true;
            return false;
        }

        private void AddStartingRecipeToOldRestaurant()
        {
            Entity card = AddCard(Mod.STARTING_DISH, 0, enabled: true, vanilla: false);
            int tagged = 0;
            using (EntityQuery menu = EntityManager.CreateEntityQuery(typeof(CMenuItem)))
            using (NativeArray<Entity> entities = menu.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < entities.Length; i++)
                {
                    if (EntityManager.GetComponentData<CMenuItem>(entities[i]).SourceDish != Mod.STARTING_DISH || Has<CTycoonCardEffect>(entities[i])) continue;
                    EntityManager.AddComponentData(entities[i], new CTycoonCardEffect { UnlockID = Mod.STARTING_DISH });
                    tagged++;
                }
            if (tagged > 0) EntityManager.AddComponent<CTycoonCardApplied>(card);
            Mod.LogInfo($"Cards: this restaurant's starting recipe is now a card ({tagged} menu item(s) already on the menu).");
        }

        private void RecordVanillaCards()
        {
            if (_vanillaUnlocks.IsEmpty) return;
            var known = new HashSet<int>();
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                for (int i = 0; i < cards.Length; i++) known.Add(cards[i].UnlockID);

            using NativeArray<CProgressionUnlock> unlocks = _vanillaUnlocks.ToComponentDataArray<CProgressionUnlock>(Allocator.Temp);
            for (int i = 0; i < unlocks.Length; i++)
            {
                int id = unlocks[i].ID;
                if (id == 0 || known.Contains(id)) continue;
                if (!GameData.Main.TryGet<Unlock>(id, out Unlock unlock) || unlock is Dish) continue;
                known.Add(id);
                AddCard(id, TycoonEconomy.CurrentStars, enabled: true, vanilla: true);
                Mod.LogInfo($"Cards: recorded '{unlock.Name}' under star {TycoonEconomy.CurrentStars}.");
            }
        }

        private void HandleRequests()
        {
            using NativeArray<Entity> requests = _requests.ToEntityArray(Allocator.Temp);
            using NativeArray<Entity> cardEntities = _cards.ToEntityArray(Allocator.Temp);
            for (int r = 0; r < requests.Length; r++)
            {
                CTycoonCardToggleRequest request = EntityManager.GetComponentData<CTycoonCardToggleRequest>(requests[r]);
                EntityManager.DestroyEntity(requests[r]);
                string player = TycoonPurchaseLog.PlayerName(request.PlayerID);

                Entity target = Entity.Null;
                CTycoonCard card = default;
                for (int i = 0; i < cardEntities.Length; i++)
                {
                    CTycoonCard c = EntityManager.GetComponentData<CTycoonCard>(cardEntities[i]);
                    if (c.UnlockID == request.UnlockID) { target = cardEntities[i]; card = c; break; }
                }
                if (target == Entity.Null) continue;
                string name = GameData.Main.TryGet<Unlock>(card.UnlockID, out Unlock unlock) ? unlock.Name : card.UnlockID.ToString();

                if (card.Vanilla) continue;
                if (Mod.IsRewardCard(card.UnlockID)) { Mod.LogInfo($"Cards: '{name}' is a star reward and stays on."); continue; }

                if (!card.Enabled && IsPrimaryTheme(card.UnlockID))
                    for (int i = 0; i < cardEntities.Length; i++)
                    {
                        if (cardEntities[i] == target) continue;
                        CTycoonCard other = EntityManager.GetComponentData<CTycoonCard>(cardEntities[i]);
                        if (!other.Enabled || !IsPrimaryTheme(other.UnlockID)) continue;
                        other.Enabled = false;
                        EntityManager.SetComponentData(cardEntities[i], other);
                        Mod.LogInfo($"Cards: theme '{TycoonCardMarket.NameOf(other.UnlockID)}' switched off (one theme at a time).");
                    }

                if (Mod.FastCasualCard != null && card.UnlockID == Mod.FastCasualCard.ID && !card.Enabled
                    && (Mod.TakeOutCard == null || !TycoonCards.IsEnabled(EntityManager, Mod.TakeOutCard.ID)))
                {
                    Mod.LogInfo($"Cards: {player} tried to switch '{name}' on, but Take-Out is off.");
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, request.Position, "Fast Casual Dining needs Take-Out on");
                    continue;
                }

                if (!card.Enabled && TycoonCards.IsDiningCard(card.UnlockID) && !TycoonCards.DiningPossible(EntityManager))
                {
                    Mod.LogInfo($"Cards: {player} tried to switch '{name}' on, but nobody sits down to eat (Take-Out without Fast Casual Dining).");
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, request.Position, name + " needs dine-in customers");
                    continue;
                }

                card.Enabled = !card.Enabled;
                EntityManager.SetComponentData(target, card);
                if (Has<CTycoonCardPending>(target)) EntityManager.RemoveComponent<CTycoonCardPending>(target);
                Mod.LogInfo($"Cards: {player} switched '{name}' {(card.Enabled ? "on" : "off")}.");
            }
        }
    }

    /// <summary>Card state lookups usable from any system or view.</summary>
    public static class TycoonCards
    {
        private const int MAX_VALUE = 52;
        private static string _published;

        public static void Publish(EntityManager em)
        {
            List<CTycoonCard> cards = All(em);
            HashSet<int> applied = Applied(em);
            HashSet<int> bought = Bought(em);
            var parts = new List<string>();
            var chunk = new System.Text.StringBuilder();
            foreach (CTycoonCard c in cards)
            {
                int flags = (c.Enabled ? 1 : 0) | (c.Vanilla ? 2 : 0) | (applied.Contains(c.UnlockID) ? 4 : 0) | (bought.Contains(c.UnlockID) ? 8 : 0);
                string one = $"{c.UnlockID}:{c.Star}:{flags}";
                if (chunk.Length > 0 && chunk.Length + 1 + one.Length > MAX_VALUE) { parts.Add(chunk.ToString()); chunk.Clear(); }
                if (chunk.Length > 0) chunk.Append(';');
                chunk.Append(one);
            }
            if (chunk.Length > 0) parts.Add(chunk.ToString());
            string all = string.Join("|", parts);
            if (all == _published) return;
            _published = all;
            for (int i = 0; i < parts.Count; i++) TycoonNetData.Set("cd" + i, parts[i]);
            TycoonNetData.Set("cn", parts.Count.ToString());
        }

        private static readonly HashSet<int> _boughtFromHost = new HashSet<int>();

        public static HashSet<int> Bought(EntityManager em)
        {
            if (!TycoonNetData.IsHost) { FromHost(); return new HashSet<int>(_boughtFromHost); }
            var set = new HashSet<int>();
            using EntityQuery cards = em.CreateEntityQuery(typeof(CTycoonCard), typeof(CTycoonCardBought));
            using NativeArray<CTycoonCard> all = cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
            for (int i = 0; i < all.Length; i++) set.Add(all[i].UnlockID);
            return set;
        }

        private static List<(CTycoonCard card, bool applied)> FromHost()
        {
            var list = new List<(CTycoonCard, bool)>();
            int[] count = TycoonNetData.Ints("cn");
            _boughtFromHost.Clear();
            if (count == null || count.Length == 0) return list;
            for (int i = 0; i < count[0]; i++)
            {
                string value = TycoonNetData.Get("cd" + i);
                if (string.IsNullOrEmpty(value)) continue;
                foreach (string one in value.Split(';'))
                {
                    string[] f = one.Split(':');
                    if (f.Length < 3 || !int.TryParse(f[0], out int id) || !int.TryParse(f[1], out int star) || !int.TryParse(f[2], out int flags)) continue;
                    list.Add((new CTycoonCard { UnlockID = id, Star = star, Enabled = (flags & 1) != 0, Vanilla = (flags & 2) != 0 }, (flags & 4) != 0));
                    if ((flags & 8) != 0) _boughtFromHost.Add(id);
                }
            }
            return list;
        }

        private static readonly string[] DINING_CARDS = { "Picky Eaters", "Leisurely Eating" };

        public static bool IsDiningCard(int unlockID)
        {
            if (!GameData.Main.TryGet<Unlock>(unlockID, out Unlock u, false) || string.IsNullOrEmpty(u.Name)) return false;
            foreach (string n in DINING_CARDS) if (string.Equals(u.Name.Trim(), n, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool DiningPossible(EntityManager em) =>
            Mod.TakeOutCard == null || !IsEnabled(em, Mod.TakeOutCard.ID) || (Mod.FastCasualCard != null && IsEnabled(em, Mod.FastCasualCard.ID));

        public static bool IsEnabled(EntityManager em, int unlockID)
        {
            if (!TycoonNetData.IsHost) return FromHost().Exists(c => c.card.UnlockID == unlockID && c.card.Enabled);
            using EntityQuery cards = em.CreateEntityQuery(typeof(CTycoonCard));
            using NativeArray<CTycoonCard> all = cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
            for (int i = 0; i < all.Length; i++)
                if (all[i].UnlockID == unlockID) return all[i].Enabled;
            return false;
        }

        public static HashSet<int> Applied(EntityManager em)
        {
            var set = new HashSet<int>();
            if (!TycoonNetData.IsHost)
            {
                foreach ((CTycoonCard card, bool applied) in FromHost()) if (applied) set.Add(card.UnlockID);
                return set;
            }
            using EntityQuery cards = em.CreateEntityQuery(typeof(CTycoonCard), typeof(CTycoonCardApplied));
            using NativeArray<CTycoonCard> all = cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
            for (int i = 0; i < all.Length; i++) set.Add(all[i].UnlockID);
            return set;
        }

        public static List<CTycoonCard> All(EntityManager em)
        {
            var list = new List<CTycoonCard>();
            if (!TycoonNetData.IsHost) foreach ((CTycoonCard card, bool _) in FromHost()) list.Add(card);
            else
            {
                using EntityQuery cards = em.CreateEntityQuery(typeof(CTycoonCard));
                using NativeArray<CTycoonCard> all = cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
                for (int i = 0; i < all.Length; i++) list.Add(all[i]);
            }

            var dishes = new HashSet<int>();
            var starOf = new Dictionary<int, int>();
            foreach (CTycoonCard c in list) { starOf[c.UnlockID] = c.Star; if (IsRecipe(c.UnlockID)) dishes.Add(c.UnlockID); }
            int PathKey(CTycoonCard c)
            {
                int root = PathRoot(c.UnlockID, dishes);
                return root == 0 ? -1 : (starOf.TryGetValue(root, out int s) ? s : 0);
            }
            list.Sort((a, b) =>
            {
                int ra = PathRoot(a.UnlockID, dishes), rb = PathRoot(b.UnlockID, dishes);
                if (ra != rb)
                {
                    int ka = PathKey(a), kb = PathKey(b);
                    return ka != kb ? ka.CompareTo(kb) : ra.CompareTo(rb);
                }
                bool aRoot = a.UnlockID == ra, bRoot = b.UnlockID == rb;
                if (aRoot != bRoot) return aRoot ? -1 : 1;
                return a.Star != b.Star ? a.Star.CompareTo(b.Star) : a.UnlockID.CompareTo(b.UnlockID);
            });
            return list;
        }

        public static bool IsRecipe(int unlockID) => GameData.Main.TryGet<Dish>(unlockID, out Dish _);

        public static int PathRoot(int unlockID, HashSet<int> cardDishes)
        {
            if (Mod.TakeOutCard != null && (unlockID == Mod.TakeOutCard.ID || (Mod.FastCasualCard != null && unlockID == Mod.FastCasualCard.ID)))
                return Mod.TakeOutCard.ID;
            if (!GameData.Main.TryGet<Dish>(unlockID, out Dish dish)) return 0;
            for (int depth = 0; depth < 8; depth++)
            {
                Dish parent = null;
                if (dish.Requires != null)
                    foreach (Unlock r in dish.Requires)
                        if (r is Dish d && d.ID != dish.ID && cardDishes.Contains(d.ID)) { parent = d; break; }
                if (parent == null) return dish.ID;
                dish = parent;
            }
            return dish.ID;
        }

        public static string PathName(int root) =>
            root == 0 ? "General"
            : Mod.TakeOutCard != null && root == Mod.TakeOutCard.ID ? "Take-Out path" : (GameData.Main.TryGet<Dish>(root, out Dish d) && !string.IsNullOrEmpty(d.Name) ? d.Name : root.ToString()) + " path";
    }
}
