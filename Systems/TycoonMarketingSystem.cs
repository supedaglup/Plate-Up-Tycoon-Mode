using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Components
{
    /// <summary>A running Marketing campaign.</summary>
    public struct CTycoonMarketing : IComponentData, IModComponent
    {
        public int Slot;
        public int DaysLeft;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Marketing: runs the bought campaigns and their effects.</summary>
    public class TycoonMarketingSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _campaigns;
        private int _nightDone = -1;
        private string _published = "";

        protected override void Initialise()
        {
            base.Initialise();
            _campaigns = GetEntityQuery(new QueryHelper().All(typeof(CTycoonMarketing)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            using NativeArray<Entity> entities = _campaigns.ToEntityArray(Allocator.Temp);

            if (HasSingleton<SIsNightFirstUpdate>() && _nightDone != day)
            {
                _nightDone = day;
                EndOfDay(entities);
                return;
            }

            float customers = 0f, repServe = 0f, repDaily = 0f, tips = 0f, price = 0f, patience = 0f, decay = 0f;
            var running = new List<string>();
            foreach (Entity e in entities)
            {
                CTycoonMarketing c = EntityManager.GetComponentData<CTycoonMarketing>(e);
                TycoonPromotions.Marketing m = TycoonPromotions.MarketingAt(c.Slot);
                if (m == null) continue;
                customers += m.Customers; repServe += m.RepServe; repDaily += m.RepDaily; tips += m.Tips;
                price += m.Price_; patience += m.Patience; decay += m.Decay;
                running.Add($"{c.Slot}:{c.DaysLeft}");
                if (!Has<CCustomerSpawnModifier>(e)) EntityManager.AddComponent<CCustomerSpawnModifier>(e);
                CCustomerSpawnModifier mod = EntityManager.GetComponentData<CCustomerSpawnModifier>(e);
                if (Mathf.Abs(mod.BaseCustomerMultiplier.Value - m.Customers / 100f) > 0.0001f)
                    EntityManager.SetComponentData(e, new CCustomerSpawnModifier { BaseCustomerMultiplier = new Factor { Value = m.Customers / 100f }, PerDayCustomerMultiplier = new Factor { Value = 0f } });
            }
            TycoonPromotions.CustomersBonus = customers;
            TycoonPromotions.RepPerServe = repServe;
            TycoonPromotions.RepDaily = repDaily;
            TycoonPromotions.TipChance = tips;
            TycoonPromotions.PriceBonus = price;
            TycoonPromotions.QueuePatience = patience;
            TycoonPromotions.DecayCut = Mathf.Clamp(decay, 0f, 90f);

            var active = new HashSet<int>();
            foreach (Entity e in entities) active.Add(EntityManager.GetComponentData<CTycoonMarketing>(e).Slot);
            List<int> offers = TycoonPromotions.Offers(TycoonPromotions.MARKETING_OFFERS, TycoonPromotions.MarketingCards.Count, day, 1, active);
            string publish = string.Join(";", running) + "|" + string.Join(",", offers);
            if (publish != _published)
            {
                _published = publish;
                TycoonNetData.Set("mk", string.Join(";", running));
                TycoonNetData.Set("mo", string.Join(",", offers));
            }
        }

        private void EndOfDay(NativeArray<Entity> entities)
        {
            if (entities.Length == 0) return;
            float repDaily = 0f;
            var ended = new List<string>();
            foreach (Entity e in entities)
            {
                CTycoonMarketing c = EntityManager.GetComponentData<CTycoonMarketing>(e);
                TycoonPromotions.Marketing m = TycoonPromotions.MarketingAt(c.Slot);
                if (m != null) repDaily += m.RepDaily;
                c.DaysLeft--;
                if (c.DaysLeft <= 0 || m == null)
                {
                    ended.Add(m != null ? m.Name : $"slot {c.Slot}");
                    EntityManager.DestroyEntity(e);
                }
                else EntityManager.SetComponentData(e, c);
            }
            if (repDaily != 0f && HasSingleton<STycoonReputation>())
            {
                STycoonReputation rep = GetSingleton<STycoonReputation>();
                TycoonReputation.Add(ref rep, repDaily);
                TycoonReputationReport.Note("Marketing campaigns", repDaily);
                Set(rep);
                TycoonReputation.Current = rep;
                Mod.LogInfo($"Marketing: +{repDaily:0.#} reputation from campaigns.");
            }
            if (ended.Count > 0)
            {
                Mod.LogInfo($"Marketing: ended - {string.Join(", ", ended)}.");
                ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), ended.Count == 1 ? $"Campaign ended: {Short(ended[0])}" : $"{ended.Count} marketing campaigns ended");
            }
        }

        private static string Short(string s) => s.Length > 40 ? s.Substring(0, 40) : s;
    }

    /// <summary>The Management Desk's Marketing and Contracts answers, handled on the host by ManageManagementDeskIndicators.</summary>
    public static class TycoonPromotionDesk
    {
        public const int CANCEL_CONTRACT = -79500;
        public static int BuyMarketing(int slot) => -78000 - slot;
        public static int BookContract(int slot) => -79000 - slot;

        public static bool Handle(EntityManager em, int code, int playerID, Vector3 position, float now)
        {
            if (code <= -78000 && code > -78000 - TycoonPromotions.MARKETING_SLOTS) { Buy(em, -78000 - code, playerID, position, now); return true; }
            if (code == CANCEL_CONTRACT) { TycoonContractSystem.Cancel(em, position, now); return true; }
            if (code <= -79000 && code > -79000 - TycoonPromotions.CONTRACT_SLOTS) { TycoonContractSystem.Book(em, -79000 - code, position, now); return true; }
            return false;
        }

        internal static void Say(EntityManager em, float now, Vector3 at, string text) =>
            ManageTycoonAnnouncements.Announce(em, now, at, text.Length > 58 ? text.Substring(0, 58) : text);

        private static void Buy(EntityManager em, int slot, int playerID, Vector3 at, float now)
        {
            TycoonPromotions.Marketing m = TycoonPromotions.MarketingAt(slot);
            if (m == null) return;
            if (TycoonEconomy.CurrentStars < TycoonPromotions.MARKETING_STAR) { Say(em, now, at, "Marketing opens at 3 stars"); return; }
            using EntityQuery campaigns = em.CreateEntityQuery(typeof(CTycoonMarketing));
            using NativeArray<CTycoonMarketing> running = campaigns.ToComponentDataArray<CTycoonMarketing>(Allocator.Temp);
            if (running.Length >= TycoonPromotions.MAX_ACTIVE_MARKETING) { Say(em, now, at, $"Already {TycoonPromotions.MAX_ACTIVE_MARKETING} campaigns running"); return; }
            foreach (CTycoonMarketing r in running) if (r.Slot == slot) { Say(em, now, at, "That campaign is already running"); return; }
            using EntityQuery moneyQuery = em.CreateEntityQuery(typeof(SMoney));
            if (moneyQuery.IsEmpty) return;
            SMoney money = moneyQuery.GetSingleton<SMoney>();
            if (money.Amount < m.Price) { Say(em, now, at, $"Not enough money for {m.Name} ({m.Price})"); return; }
            money.Amount -= m.Price;
            moneyQuery.SetSingleton(money);
            Entity e = em.CreateEntity(typeof(CTycoonMarketing), typeof(CCustomerSpawnModifier));
            em.SetComponentData(e, new CTycoonMarketing { Slot = slot, DaysLeft = m.Days });
            em.SetComponentData(e, new CCustomerSpawnModifier { BaseCustomerMultiplier = new Factor { Value = m.Customers / 100f }, PerDayCustomerMultiplier = new Factor { Value = 0f } });
            int day = 0;
            using (EntityQuery d = em.CreateEntityQuery(typeof(SDay))) if (!d.IsEmpty) day = d.GetSingleton<SDay>().Day;
            string player = TycoonPurchaseLog.PlayerName(playerID);
            TycoonPurchaseLog.Add(day, player, "Marketing: " + m.Name, m.Price);
            Say(em, now, at, $"{m.Name} running for {m.Days} day{(m.Days == 1 ? "" : "s")}");
            Mod.LogInfo($"Marketing: {player} bought '{m.Name}' for {m.Price} ({m.EffectText()}, {m.Days} day(s)). Cash now {money.Amount}.");
        }
    }
}
