using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Bankruptcy, the Tycoon fail state.</summary>
    public class BankruptcySystem : GenericSystemBase, IModSystem
    {
        public const LossReason BANKRUPT = (LossReason)77;
        public const float SELL_BACK_SHARE = 0.5f;
        public const float SITE_SELL_BACK_SHARE = 0.5f;

        internal static string SaleSummary;

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STycoonEconomy>();
            RequireSingletonForUpdate<SKitchenMarker>();
        }

        internal static bool BankruptNow;

        protected override void OnUpdate()
        {
            if (HasSingleton<SGameOver>())
            {
                BankruptNow = false;
                return;
            }
            if (!BankruptNow) return;
            BankruptNow = false;
            STycoonEconomy economy = GetSingleton<STycoonEconomy>();
            int cash = HasSingleton<SMoney>() ? GetSingleton<SMoney>().Amount : 0;
            TycoonCompany.EnsureLoaded();
            int owed = TycoonCompany.Outstanding;

            var saved = new Dictionary<int, World>();
            int index = 0;
            try
            {
                foreach ((World world, SaveState state) in Persistence.FullWorld.All())
                {
                    index++;
                    if (state == SaveState.Loaded && world != null) saved[index] = world;
                }
            }
            catch (System.Exception e) { Mod.LogWarning("BANKRUPT: could not read the other restaurants: " + e.Message); }
            int current = saved.ContainsKey(TycoonCompany.ActiveSlot) ? TycoonCompany.ActiveSlot : saved.Count > 0 ? new List<int>(saved.Keys)[0] : TycoonCompany.ActiveSlot;
            var order = new List<int>(saved.Keys);
            order.Remove(current);
            int Rank(int slot) => TycoonCompany.Restaurants.FindIndex(r => r.slot == slot);
            order.Sort((a, b) => Rank(b) != Rank(a) ? Rank(b).CompareTo(Rank(a)) : b.CompareTo(a));
            order.Insert(0, current);

            int money = cash;
            var sold = new List<int>();
            var lines = new List<string>();
            foreach (int slot in order)
            {
                if (slot != current && money - owed >= 0) break;
                int count = 0;
                int appliances = slot == current ? ApplianceValue(EntityManager, out count) : TycoonCompany.ApplianceValues.TryGetValue(slot, out int worth) ? worth : 0;
                int sitePrice = TycoonCompany.Restaurants.Find(r => r.slot == slot).price;
                int value = Mathf.RoundToInt(appliances * SELL_BACK_SHARE) + Mathf.RoundToInt(sitePrice * SITE_SELL_BACK_SHARE);
                money += value;
                sold.Add(slot);
                lines.Add($"slot {slot}{(slot == current ? " (this one)" : "")}: {count} appliance(s) and the site for {value}");
            }
            int left = money - owed;
            int remaining = order.Count - sold.Count;
            Mod.LogWarning($"BANKRUPT: still below 0 at closing after {economy.NightsInDebt} night(s) in debt (cash {cash}, loan {owed}). "
                + $"The bank sold {string.Join("; ", lines)}; {(left >= 0 ? left + " left over" : -left + " written off")}; {remaining} restaurant(s) left.");

            foreach (int slot in sold)
                if (slot != current)
                    try { Persistence.FullWorld.Clear(slot); TycoonCompany.ApplianceValues.Remove(slot); TycoonCompany.LastTakings.Remove(slot); } catch (System.Exception e) { Mod.LogWarning($"BANKRUPT: could not clear slot {slot}: {e.Message}"); }

            if (remaining == 0)
            {
                int carried = Mathf.Max(0, left);
                SaleSummary = $"The bank sold {(sold.Count == 1 ? "the restaurant" : $"all {sold.Count} restaurants")} and everything in {(sold.Count == 1 ? "it" : "them")} for {money - cash} coins against {(cash < 0 ? -cash : 0) + owed} coins of debt. "
                    + (carried > 0 ? $"{carried} coins are left for a fresh start." : left < 0 ? $"The other {-left} coins are written off." : "Nothing is left over.");
                Set(new SGameOver { Reason = BANKRUPT });
                TycoonCompany.ResetMoney("bankruptcy", carried);
                return;
            }

            TycoonCompany.Cash = left;
            TycoonCompany.Outstanding = 0;
            TycoonCompany.DailyPayment = 0;
            TycoonCompany.Restaurants.RemoveAll(r => sold.Contains(r.slot));
            if (sold.Contains(TycoonCompany.ActiveSlot)) TycoonCompany.ActiveSlot = 0;
            TycoonCompany.Save();
            SaleSummary = sold.Count == 1
                ? $"The bank sold this restaurant and everything in it to pay the debts. The company keeps {remaining} restaurant{(remaining == 1 ? "" : "s")} and {left} coins."
                : $"The bank sold this restaurant and {sold.Count - 1} more to pay the debts. The company keeps {remaining} restaurant{(remaining == 1 ? "" : "s")} and {left} coins.";
            Set(new SGameOver { Reason = BANKRUPT });
        }

        internal static int ApplianceValue(EntityManager em, out int count)
        {
            count = 0;
            int value = 0;
            using EntityQuery q = em.CreateEntityQuery(typeof(CAppliance));
            using NativeArray<CAppliance> owned = q.ToComponentDataArray<CAppliance>(Allocator.Temp);
            for (int i = 0; i < owned.Length; i++)
            {
                if (!TycoonCatalogue.TryGetAny(owned[i].ID, out TycoonCatalogue.Entry entry)) continue;
                count++;
                value += entry.Cost;
            }
            return value;
        }
    }
}
