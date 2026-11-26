using Kitchen;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Components
{
    /// <summary>Today's curb sales, paid at closing.</summary>
    public struct STycoonCurbSalePending : IComponentData
    {
        public int Amount;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Selling appliances on the curb.</summary>
    public static class TycoonCurbSale
    {
        public const float SELL_SHARE = 0.5f;
        private const float POPUP_SECONDS = 1f;

        internal static int SellCurb(EntityManager em)
        {
            using (EntityQuery practice = em.CreateEntityQuery(typeof(SPracticeMode)))
                if (!practice.IsEmpty)
                {
                    Mod.LogInfo("Curb sale: practice mode, nothing is sold.");
                    return 0;
                }
            int total = 0;
            var sold = new List<string>();
            using EntityQuery marked = em.CreateEntityQuery(typeof(CDestroyApplianceAtDay), typeof(CAppliance));
            using NativeArray<Entity> entities = marked.ToEntityArray(Allocator.Temp);
            foreach (Entity e in entities)
            {
                int id = em.GetComponentData<CAppliance>(e).ID;
                if (Mod.IsTycoonDesk(id))
                {
                    em.RemoveComponent<CDestroyApplianceAtDay>(e);
                    Mod.LogInfo("Curb sale: a Tycoon desk was left outside; it stays (desks can't be sold).");
                    continue;
                }
                int inside = em.HasComponent<CLetterAppliance>(e) ? em.GetComponentData<CLetterAppliance>(e).ApplianceID : id;
                if (inside == 0 || !TycoonCatalogue.TryGetAny(inside, out TycoonCatalogue.Entry entry)) continue;
                int value = Mathf.RoundToInt(TycoonShopPrices.OrderPrice(entry) * SELL_SHARE);
                if (value <= 0) continue;
                total += value;
                sold.Add($"{Name(inside)} ({value})");
                if (em.HasComponent<CPosition>(e)) CoinPopup(em, em.GetComponentData<CPosition>(e).Position, value);
            }
            if (total <= 0) return 0;

            using (EntityQuery pending = em.CreateEntityQuery(typeof(STycoonCurbSalePending)))
            {
                if (pending.IsEmpty)
                {
                    Entity e = em.CreateEntity(typeof(STycoonCurbSalePending), typeof(CDoNotPersist));
                    em.SetComponentData(e, new STycoonCurbSalePending { Amount = total });
                }
                else
                {
                    STycoonCurbSalePending p = pending.GetSingleton<STycoonCurbSalePending>();
                    p.Amount += total;
                    pending.SetSingleton(p);
                }
            }
            Mod.LogInfo($"Curb sale: sold {string.Join(", ", sold)} for {total} coins in all, paid at closing.");
            return total;
        }

        internal static int TakePending(EntityManager em)
        {
            using EntityQuery pending = em.CreateEntityQuery(typeof(STycoonCurbSalePending));
            if (pending.IsEmpty) return 0;
            int amount = pending.GetSingleton<STycoonCurbSalePending>().Amount;
            em.DestroyEntity(pending.GetSingletonEntity());
            return amount;
        }

        private static void CoinPopup(EntityManager em, Vector3 at, int coins)
        {
            Entity popup = em.CreateEntity(typeof(CMoneyPopup), typeof(CPosition), typeof(CLifetime), typeof(CRequiresView));
            em.SetComponentData(popup, new CMoneyPopup { Change = coins });
            em.SetComponentData(popup, new CPosition(at));
            em.SetComponentData(popup, new CLifetime(POPUP_SECONDS));
            em.SetComponentData(popup, new CRequiresView { Type = ViewType.MoneyPopup });
        }

        private static string Name(int id) =>
            KitchenData.GameData.Main.TryGet<KitchenData.Appliance>(id, out KitchenData.Appliance a) && !string.IsNullOrEmpty(a.Name) ? a.Name : id.ToString();
    }
}
