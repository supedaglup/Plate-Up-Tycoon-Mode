using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Marketing cards and Contracts, read from editable text files.</summary>
    public static class TycoonPromotions
    {
        public const int MARKETING_SLOTS = 40, CONTRACT_SLOTS = 30;
        public const int MAX_ACTIVE_MARKETING = 3, MARKETING_OFFERS = 3, CONTRACT_OFFERS = 4;
        public const int MARKETING_STAR = 3, CONTRACTS_STAR = 4;
        public const int LUXURY_PAY_BONUS = 40;
        public const float LUXURY_REP_PER_SERVE = 1.5f;

        public class Marketing
        {
            public string Name, Flavour;
            public int Price, Days;
            public float Customers, RepServe, RepDaily, Tips, Price_, Patience, Decay;

            public string EffectText()
            {
                var parts = new List<string>();
                if (Customers != 0f) parts.Add($"{Customers:+0;-0}% customers");
                if (RepServe != 0f) parts.Add($"{RepServe:+0.##;-0.##} reputation per serve");
                if (RepDaily != 0f) parts.Add($"{RepDaily:+0.#;-0.#} reputation a day");
                if (Tips != 0f) parts.Add($"{Tips:+0;-0}% tip chance");
                if (Price_ != 0f) parts.Add($"{Price_:+0;-0}% prices");
                if (Patience != 0f) parts.Add($"{Patience:+0;-0} s queue patience");
                if (Decay != 0f) parts.Add($"{Decay:0}% less reputation decay");
                return string.Join(", ", parts);
            }
        }

        public class Contract
        {
            public string Name, Flavour;
            public float Arrival;
            public int Size, Patience, RewardCoins, RewardRep, FailRep, FailCoins;
            public int MinStars = CONTRACTS_STAR;
            public bool Luxury => MinStars >= 5;
            public string Outfit;

            public string Summary() => Luxury
                ? $"All day: every guest is a VIP (+{LUXURY_PAY_BONUS}% pay, +{LUXURY_REP_PER_SERVE:0.#} reputation a serve, {Patience}% patience). Deposit {Systems.TycoonContractSystem.DepositFor(this)} coins. At most {Size} walkout{(Size == 1 ? "" : "s")} all day: +{RewardCoins} coins, +{RewardRep} reputation, deposit back. One more: -{FailRep} reputation{(FailCoins > 0 ? $", -{FailCoins} coins" : "")}, the deposit, and all the extra the VIPs paid and earned."
                : $"{Size} guests, {Patience}% patience. Deposit {Systems.TycoonContractSystem.DepositFor(this)} coins. Serve them all: +{RewardCoins} coins, +{RewardRep} reputation, deposit back. Lose one: -{FailRep} reputation{(FailCoins > 0 ? $", -{FailCoins} coins" : "")}, the deposit, and everything the group paid and earned.";
        }

        private static List<Marketing> _marketing;
        private static List<Contract> _contracts;

        public static List<Marketing> MarketingCards => _marketing ?? (_marketing = LoadMarketing());
        public static List<Contract> Contracts => _contracts ?? (_contracts = LoadContracts());

        public static Marketing MarketingAt(int slot) => slot >= 0 && slot < MarketingCards.Count ? MarketingCards[slot] : null;
        public static Contract ContractAt(int slot) => slot >= 0 && slot < Contracts.Count ? Contracts[slot] : null;

        private static IEnumerable<string[]> Lines(string resource)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream == null) { Mod.LogWarning($"Promotions: {resource} not found."); yield break; }
            using var reader = new StreamReader(stream);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] parts = line.Split('|');
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
                yield return parts;
            }
        }

        private static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        private static int I(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        private static List<Marketing> LoadMarketing()
        {
            var list = new List<Marketing>();
            foreach (string[] p in Lines("PlateUpTycoon.MarketingCards.txt"))
            {
                if (p.Length < 4 || list.Count >= MARKETING_SLOTS) continue;
                var m = new Marketing { Name = p[0], Price = I(p[1]), Days = Mathf.Max(1, I(p[2])), Flavour = p.Length > 4 ? p[4] : "" };
                foreach (string effect in p[3].Split(','))
                {
                    string[] kv = effect.Split('=');
                    if (kv.Length != 2) continue;
                    float v = F(kv[1].Trim());
                    switch (kv[0].Trim().ToLowerInvariant())
                    {
                        case "customers": m.Customers = v; break;
                        case "rep_serve": m.RepServe = v; break;
                        case "rep_daily": m.RepDaily = v; break;
                        case "tips": m.Tips = v; break;
                        case "price": m.Price_ = v; break;
                        case "patience": m.Patience = v; break;
                        case "decay": m.Decay = v; break;
                        default: Mod.LogWarning($"Marketing: '{m.Name}' has an unknown effect '{kv[0].Trim()}'."); break;
                    }
                }
                list.Add(m);
            }
            Mod.LogInfo($"Marketing: {list.Count} card(s) loaded.");
            return list;
        }

        private static List<Contract> LoadContracts()
        {
            var list = new List<Contract>();
            foreach (string[] p in Lines("PlateUpTycoon.Contracts.txt"))
            {
                if (p.Length < 9 || list.Count >= CONTRACT_SLOTS) continue;
                list.Add(new Contract
                {
                    Name = p[0], Arrival = Mathf.Clamp(F(p[1]), 0.05f, 0.95f), Size = Mathf.Clamp(I(p[2]), 0, 12), Patience = Mathf.Max(10, I(p[3])),
                    RewardCoins = I(p[4]), RewardRep = I(p[5]), FailRep = I(p[6]), FailCoins = I(p[7]), Outfit = p[8], Flavour = p.Length > 9 ? p[9] : "",
                    MinStars = p.Length > 10 && p[10].Length > 0 ? Mathf.Clamp(I(p[10]), CONTRACTS_STAR, 5) : CONTRACTS_STAR,
                });
                if (!list[list.Count - 1].Luxury && list[list.Count - 1].Size < 1) list[list.Count - 1].Size = 1;
            }
            Mod.LogInfo($"Contracts: {list.Count} contract(s) loaded.");
            return list;
        }

        public static List<int> Offers(int count, int total, int day, int salt, ICollection<int> exclude)
        {
            var pool = new List<int>();
            for (int i = 0; i < total; i++) if (exclude == null || !exclude.Contains(i)) pool.Add(i);
            var random = new System.Random(day * 7349 + salt * 1013 + TycoonCompany.ActiveSlot * 31);
            var picks = new List<int>();
            while (picks.Count < count && pool.Count > 0)
            {
                int k = random.Next(pool.Count);
                picks.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return picks;
        }

        public static List<int> ContractOffers(int day, int stars)
        {
            var regular = new List<int>();
            var luxury = new List<int>();
            for (int i = 0; i < Contracts.Count; i++)
            {
                if (Contracts[i].MinStars > stars) continue;
                (Contracts[i].Luxury ? luxury : regular).Add(i);
            }
            var random = new System.Random(day * 7349 + 2 * 1013 + TycoonCompany.ActiveSlot * 31);
            int luxuryCount = luxury.Count > 0 ? Mathf.Min(luxury.Count, CONTRACT_OFFERS / 2) : 0;
            var picks = new List<int>();
            Pick(random, luxury, luxuryCount, picks);
            Pick(random, regular, CONTRACT_OFFERS - picks.Count, picks);
            Pick(random, luxury, CONTRACT_OFFERS - picks.Count, picks);
            return picks;
        }

        private static void Pick(System.Random random, List<int> pool, int count, List<int> into)
        {
            var left = new List<int>(pool);
            left.RemoveAll(into.Contains);
            while (count > 0 && left.Count > 0)
            {
                int k = random.Next(left.Count);
                into.Add(left[k]);
                left.RemoveAt(k);
                count--;
            }
        }

        public static float CustomersBonus, RepPerServe, RepDaily, TipChance, PriceBonus, QueuePatience, DecayCut;
    }
}
