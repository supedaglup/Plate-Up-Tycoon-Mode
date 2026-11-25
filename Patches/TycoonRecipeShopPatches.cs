using HarmonyLib;
using Kitchen;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Patches
{
    /// <summary>Bought recipes leave on the loading bay carts.</summary>
    [HarmonyPatch(typeof(BeginRestaurant), "OnUpdate")]
    public static class TycoonCrateNoLoadoutPatch
    {
        [HarmonyPrefix]
        private static void Prefix(BeginRestaurant __instance)
        {
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                using EntityQuery q = em.CreateEntityQuery(typeof(CTycoonApplianceCrate), typeof(CProvidesLoadoutItem));
                if (!q.IsEmpty) em.RemoveComponent<CProvidesLoadoutItem>(q);
            }
            catch (System.Exception e) { Mod.LogWarning("Discount shop: could not keep a box out of the starting kit: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(BeginRestaurant), "OnUpdate")]
    public static class TycoonRecipeCartsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BeginRestaurant __instance)
        {
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                using (EntityQuery transitions = em.CreateEntityQuery(typeof(SPerformSceneTransition)))
                    if (transitions.IsEmpty) return;

                var sent = new List<string>();
                using EntityQuery carts = em.CreateEntityQuery(typeof(CItemPedestal), typeof(CItemHolder));
                using NativeArray<Entity> entities = carts.ToEntityArray(Allocator.Temp);
                TycoonCompany.EnsureLoaded();
                foreach (Entity cart in entities)
                {
                    Entity item = em.GetComponentData<CItemHolder>(cart).HeldItem;
                    if (item == Entity.Null || !em.Exists(item)) continue;
                    if (em.HasComponent<CTycoonApplianceCrate>(item))
                    {
                        CTycoonApplianceCrate crate = em.GetComponentData<CTycoonApplianceCrate>(item);
                        if (!crate.Paid) continue;
                        TycoonCompany.CratesBought.Remove(crate.Appliance);
                        TycoonCompany.CratesToDeliver.Add(crate.Appliance);
                        em.SetComponentData(cart, new CItemHolder { HeldItem = Entity.Null });
                        em.DestroyEntity(item);
                        sent.Add(TycoonDiscountShop.NameOf(crate.Appliance) + " (box)");
                        continue;
                    }
                    if (em.HasComponent<CTycoonCardItem>(item))
                    {
                        CTycoonCardItem card = em.GetComponentData<CTycoonCardItem>(item);
                        if (!card.Paid) continue;
                        TycoonCompany.CardsBought.Remove(card.Card);
                        if (!TycoonCompany.CardsToDeliver.Contains(card.Card)) TycoonCompany.CardsToDeliver.Add(card.Card);
                        em.SetComponentData(cart, new CItemHolder { HeldItem = Entity.Null });
                        em.DestroyEntity(item);
                        sent.Add(TycoonCardMarket.NameOf(card.Card) + " (card)");
                        continue;
                    }
                    if (!em.HasComponent<CTycoonRecipePaper>(item)) continue;
                    CTycoonRecipePaper paper = em.GetComponentData<CTycoonRecipePaper>(item);
                    if (!paper.Paid) continue;
                    TycoonCompany.RecipesBought.Remove(paper.Dish);
                    if (!TycoonCompany.RecipesToDeliver.Contains(paper.Dish)) TycoonCompany.RecipesToDeliver.Add(paper.Dish);
                    em.SetComponentData(cart, new CItemHolder { HeldItem = Entity.Null });
                    em.DestroyEntity(item);
                    sent.Add(TycoonRecipeShop.NameOf(paper.Dish));
                }
                bool changed = sent.Count > 0;
                if (sent.Count > 0) Mod.LogInfo($"Recipe shop: sent to the restaurant on the loading bay carts: {string.Join(", ", sent)}.");

                if (TycoonNewRestaurant.PendingFromBlueprint && TycoonNewRestaurant.PendingSiteKey != null)
                {
                    TycoonCompany.SitesBought.Remove(TycoonNewRestaurant.PendingSiteKey);
                    TycoonCompany.RecipesBought.Remove(TycoonNewRestaurant.PendingDish);
                    TycoonCompany.ActiveSlot = TycoonNewRestaurant.PendingSlot;
                    TycoonCompany.Restaurants.RemoveAll(r => r.slot == TycoonNewRestaurant.PendingSlot);
                    TycoonCompany.Restaurants.Add((TycoonNewRestaurant.PendingSlot, TycoonNewRestaurant.PendingSitePrice));
                    Mod.LogInfo($"Sites: site {TycoonNewRestaurant.PendingSiteKey} becomes restaurant slot {TycoonCompany.ActiveSlot}.");
                    TycoonNewRestaurant.PendingSiteKey = null;
                    changed = true;
                }
                if (changed) TycoonCompany.Save();
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Recipe shop: could not load the carts: " + e.Message);
            }
        }
    }
}
