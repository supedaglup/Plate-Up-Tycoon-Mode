using Kitchen;
using KitchenData;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Whiskers's discount appliance shop in the top of the HQ garage, where the vanilla workshop was.</summary>
    public static class TycoonDiscountShop
    {
        public const int MAX_STANDS = 20;
        public const int MIN_DISCOUNT = 25, MAX_DISCOUNT = 60, DISCOUNT_STEP = 5;
        public const float NEW_SHARE = 0.25f;
        public const int NEEDED_TRIES = 2;
        public const double NEEDED_CHANCE = 0.5;
        public static float RegionMinZ => LobbyPositionAnchors.Workshop.z;

        public struct Offer
        {
            public int Appliance;
            public int Discount;
            public int Full;
            public int Price;
        }

        public static string NameOf(int applianceID) =>
            GameData.Main.TryGet<Appliance>(applianceID, out Appliance a) && !string.IsNullOrEmpty(a.Name) ? a.Name : $"appliance {applianceID}";

        public static int CrateItemFor(int applianceID) =>
            GameData.Main.TryGet<Appliance>(applianceID, out Appliance a) && a.CrateItem != null ? a.CrateItem.ID : AssetReference.ApplianceCrate;

        public static int PriceFor(int full, int discount) =>
            Mathf.Max(5, Mathf.RoundToInt(full * (100 - discount) / 100f / 5f) * 5);

        public static List<Offer> Today(int stands, int day)
        {
            TycoonCompany.EnsureLoaded();
            var pool = new List<TycoonCatalogue.Entry>();
            foreach (TycoonCatalogue.Entry e in TycoonCatalogue.Entries)
                if (e.Cost > 0 && !TycoonDecorShop.IsDecor(e.ApplianceID) && !Mod.IsTycoonDesk(e.ApplianceID)) pool.Add(e);
            pool.Sort((a, b) => a.ApplianceID.CompareTo(b.ApplianceID));
            var random = new System.Random(day * 7919 + 61 + TycoonCompany.ShopReroll * 15485863);
            var offers = new List<Offer>();
            int steps = (MAX_DISCOUNT - MIN_DISCOUNT) / DISCOUNT_STEP;

            int slot = TycoonCompany.ActiveSlot;
            var owned = new HashSet<int>(TycoonCompany.OwnedAppliances.TryGetValue(slot, out List<int> o) ? o : new List<int>());
            var needed = new HashSet<int>(TycoonCompany.CardNeeds.TryGetValue(slot, out List<int> n) ? n : new List<int>());
            foreach (int id in TycoonCompany.CratesBought) { owned.Add(id); needed.Remove(id); }
            foreach (int id in TycoonCompany.CratesToDeliver) { owned.Add(id); needed.Remove(id); }
            int forCards = 0, forNew = Mathf.RoundToInt(stands * NEW_SHARE);
            for (int t = 0; t < NEEDED_TRIES; t++) if (random.NextDouble() < NEEDED_CHANCE) forCards++;
            int cardPicks = 0, newPicks = 0;
            void Take(System.Predicate<TycoonCatalogue.Entry> fits, int count, ref int picked)
            {
                var candidates = pool.FindAll(fits);
                while (picked < count && candidates.Count > 0 && offers.Count < stands)
                {
                    TycoonCatalogue.Entry e = candidates[random.Next(candidates.Count)];
                    candidates.Remove(e);
                    pool.Remove(e);
                    int discount = MIN_DISCOUNT + DISCOUNT_STEP * random.Next(steps + 1);
                    int today = TycoonShopPrices.OrderPrice(e);
                    offers.Add(new Offer { Appliance = e.ApplianceID, Discount = discount, Full = today, Price = PriceFor(today, discount) });
                    picked++;
                }
            }
            Take(e => needed.Contains(e.ApplianceID) && e.Category != TycoonCatalogue.Category.Ingredients, forCards, ref cardPicks);
            Take(e => !owned.Contains(e.ApplianceID), forNew, ref newPicks);
            int randomPicks = 0;
            Take(e => true, stands, ref randomPicks);
            for (int i = offers.Count - 1; i > 0; i--) { int j = random.Next(i + 1); Offer t = offers[i]; offers[i] = offers[j]; offers[j] = t; }
            Mod.LogInfo($"Whiskers's shop: {offers.Count} offer(s) for slot {slot}: {cardPicks} for recipe cards ({forCards} rolled, {needed.Count} needed), {newPicks} not owned yet, {randomPicks} random.");
            return offers;
        }
    }
}
