using KitchenData;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>The HQ Card Market: Chubs the card dealer cat sells the game's customer cards in the office.</summary>
    public static class TycoonCardMarket
    {
        public const int BASE_PRICE = 100, PRICE_PER_REPUTATION = 70;

        public static readonly string[] HEAT_CARD_NAMES = { "Health Concerns", "Flaming Knives", "Soft Touch", "Overheating", "Friction Burns", "Hotheads" };
        public const float HEAT_CARD_VALUE = 16f;
        public const float HEAT_PRICE_MULTIPLIER = 1f;
        public const float HEAT_CHANCE = 0.2f, SECOND_HEAT_CHANCE = 0.25f;

        public static bool IsHeatCard(Unlock card)
        {
            if (card == null || string.IsNullOrEmpty(card.Name)) return false;
            foreach (string n in HEAT_CARD_NAMES) if (string.Equals(card.Name.Trim(), n, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool _heatLogged;

        public static List<Unlock> HeatForSale(HashSet<int> owned)
        {
            var list = new List<Unlock>();
            var found = new List<string>();
            foreach (Unlock u in GameData.Main.Get<Unlock>())
            {
                if (u == null || u is Dish || !IsHeatCard(u)) continue;
                found.Add(u.Name);
                if (owned.Contains(u.ID)) continue;
                bool blocked = false;
                if (u.BlockedBy != null) foreach (Unlock b in u.BlockedBy) if (b != null && owned.Contains(b.ID)) { blocked = true; break; }
                if (!blocked) list.Add(u);
            }
            if (!_heatLogged)
            {
                _heatLogged = true;
                Mod.LogInfo($"Card Market: heat cards found in the game: {(found.Count == 0 ? "none" : string.Join(", ", found))} (of {HEAT_CARD_NAMES.Length} named).");
            }
            list.Sort((a, b) => a.ID.CompareTo(b.ID));
            return list;
        }
        public const int MAX_SHELVES = 16;
        public static int ItemID => Mod.CardItem != null ? Mod.CardItem.ID : AssetReference.DishPaper;

        public static int Price(Unlock card) => TycoonShopPrices.Apply(FullPrice(card));

        public static float FullPrice(Unlock card) => TycoonCustomerCards.Of(card != null ? card.ID : 0) is TycoonCustomerCards.Card custom ? custom.Price :
            Mathf.RoundToInt((BASE_PRICE + PRICE_PER_REPUTATION * TycoonReputation.CardValue(card)) * (IsHeatCard(card) ? HEAT_PRICE_MULTIPLIER : 1f) / 10f) * 10;

        public static int Price(int cardID) => GameData.Main.TryGet<Unlock>(cardID, out Unlock u) ? Price(u) : 0;

        public static string NameOf(int cardID) =>
            GameData.Main.TryGet<Unlock>(cardID, out Unlock u) && !string.IsNullOrEmpty(u.Name) ? u.Name : $"card {cardID}";

        public static HashSet<int> Owned(out int day)
        {
            HashSet<int> owned = TycoonRecipeShop.Owned(out day);
            TycoonCompany.EnsureLoaded();
            foreach (int id in TycoonCompany.CardsToDeliver) owned.Add(id);
            foreach (int id in TycoonCompany.CardsBought) owned.Add(id);
            return owned;
        }

        public static List<Unlock> ForSale(HashSet<int> owned)
        {
            var list = new List<Unlock>();
            foreach (Unlock u in GameData.Main.Get<Unlock>())
            {
                if (u == null || u is Dish || string.IsNullOrEmpty(u.Name) || owned.Contains(u.ID)) continue;
                if (!u.IsUnlockable || u.UnlockGroup != UnlockGroup.Generic || u.CardType != CardType.Default || u.HasSubOptions) continue;
                if (Mod.TakeOutCard != null && u.ID == Mod.TakeOutCard.ID) continue;
                if (Mod.FastCasualCard != null && u.ID == Mod.FastCasualCard.ID) continue;
                if (IsHeatCard(u)) continue;
                if (TycoonCustomerCards.IsExcluded(u.Name)) continue;
                bool ok = true;
                if (u.Requires != null)
                    foreach (Unlock r in u.Requires) if (r == null || !owned.Contains(r.ID)) { ok = false; break; }
                if (ok && u.BlockedBy != null)
                    foreach (Unlock b in u.BlockedBy) if (b != null && owned.Contains(b.ID)) { ok = false; break; }
                if (ok) list.Add(u);
            }
            for (int i = 0; i < TycoonCustomerCards.Cards.Count && i < Mod.CustomerCardIDs.Length; i++)
            {
                int id = Mod.CustomerCardIDs[i];
                if (id == 0 || owned.Contains(id) || !GameData.Main.TryGet<Unlock>(id, out Unlock custom)) continue;
                list.Add(custom);
            }
            list.Sort((a, b) => a.ID.CompareTo(b.ID));
            if (!_listWritten)
            {
                _listWritten = true;
                var sold = new HashSet<int>();
                foreach (Unlock u in list) sold.Add(u.ID);
                TycoonCustomerCards.WriteList(GameData.Main.Get<Unlock>(), u => sold.Contains(u.ID) || owned.Contains(u.ID));
            }
            return list;
        }

        private static bool _listWritten;

        public static List<Unlock> Today(List<Unlock> forSale, List<Unlock> heatForSale, int shelves, int day)
        {
            TycoonCompany.EnsureLoaded();
            var random = new System.Random(day * 6151 + 29 + TycoonCompany.ShopReroll * 92821);
            var picks = new List<Unlock>();
            int heat = 0;
            if (heatForSale.Count > 0 && random.NextDouble() < HEAT_CHANCE) heat = random.NextDouble() < SECOND_HEAT_CHANCE ? 2 : 1;
            var heatPool = new List<Unlock>(heatForSale);
            while (picks.Count < Mathf.Min(heat, shelves) && heatPool.Count > 0)
            {
                int i = random.Next(heatPool.Count);
                picks.Add(heatPool[i]);
                heatPool.RemoveAt(i);
            }
            var pool = new List<Unlock>(forSale);
            pool.RemoveAll(picks.Contains);
            while (picks.Count < shelves && pool.Count > 0)
            {
                int i = random.Next(pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }
    }
}
