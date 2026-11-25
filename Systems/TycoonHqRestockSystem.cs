using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Debug: restocks everything sold at the HQ.</summary>
    public class TycoonHqRestockSystem : FranchiseSystem, IModSystem
    {
        internal static bool Requested;

        protected override void OnUpdate()
        {
            if (!Requested || !TycoonSession.IsActive) return;
            Requested = false;
            try
            {
                int recipes = RestockRecipes(), cards = RestockCards(), sites = RestockSites(), boxes = RestockBoxes();
                Mod.LogInfo($"Debug: HQ restocked (reroll {TycoonCompany.ShopReroll}): {recipes} recipe(s), {cards} card(s), {sites} site(s), {boxes} appliance box(es).");
            }
            catch (System.Exception e) { Mod.LogError("Debug: HQ restock failed: " + e); }
        }

        private List<Entity> Clear<TShelf, TItem>(System.Func<TItem, bool> paid) where TShelf : struct, IComponentData where TItem : struct, IComponentData
        {
            var empty = new List<Entity>();
            using EntityQuery q = EntityManager.CreateEntityQuery(typeof(TShelf), typeof(CItemHolder));
            using NativeArray<Entity> shelves = q.ToEntityArray(Allocator.Temp);
            foreach (Entity shelf in shelves)
            {
                Entity item = EntityManager.GetComponentData<CItemHolder>(shelf).HeldItem;
                if (item != Entity.Null && EntityManager.Exists(item))
                {
                    if (!EntityManager.HasComponent<TItem>(item) || paid(EntityManager.GetComponentData<TItem>(item))) continue;
                    EntityManager.DestroyEntity(item);
                    EntityManager.SetComponentData(shelf, new CItemHolder { HeldItem = Entity.Null });
                }
                empty.Add(shelf);
            }
            return empty;
        }

        private int RestockRecipes()
        {
            List<Entity> shelves = Clear<CTycoonRecipeShelf, CTycoonRecipePaper>(p => p.Paid);
            if (shelves.Count == 0) return 0;
            HashSet<int> owned = TycoonRecipeShop.Owned(out int day);
            foreach (int id in TycoonCompany.RecipesBought) owned.Add(id);
            using (EntityQuery q = EntityManager.CreateEntityQuery(typeof(CTycoonRecipePaper)))
            using (NativeArray<CTycoonRecipePaper> left = q.ToComponentDataArray<CTycoonRecipePaper>(Allocator.Temp))
                foreach (CTycoonRecipePaper p in left) owned.Add(p.Dish);
            List<Dish> picks = TycoonRecipeShop.Today(TycoonRecipeShop.ForSale(owned), shelves.Count, day);
            var maker = World.GetExistingSystem<TycoonRecipeShopSetupSystem>();
            for (int i = 0; i < picks.Count && i < shelves.Count; i++) TycoonGarage.Hold(EntityManager, shelves[i], maker.CreatePaper(picks[i].ID, false));
            return picks.Count;
        }

        private int RestockCards()
        {
            List<Entity> shelves = Clear<CTycoonCardShelf, CTycoonCardItem>(c => c.Paid);
            if (shelves.Count == 0) return 0;
            HashSet<int> owned = TycoonCardMarket.Owned(out int day);
            using (EntityQuery q = EntityManager.CreateEntityQuery(typeof(CTycoonCardItem)))
            using (NativeArray<CTycoonCardItem> left = q.ToComponentDataArray<CTycoonCardItem>(Allocator.Temp))
                foreach (CTycoonCardItem c in left) owned.Add(c.Card);
            List<Unlock> picks = TycoonCardMarket.Today(TycoonCardMarket.ForSale(owned), TycoonCardMarket.HeatForSale(owned), shelves.Count, day);
            var maker = World.GetExistingSystem<TycoonCardMarketSetupSystem>();
            for (int i = 0; i < picks.Count && i < shelves.Count; i++) TycoonGarage.Hold(EntityManager, shelves[i], maker.CreateCard(picks[i].ID, false));
            return picks.Count;
        }

        private int RestockBoxes()
        {
            List<Entity> stands = Clear<CTycoonDiscountStand, CTycoonApplianceCrate>(c => c.Paid);
            if (stands.Count == 0) return 0;
            TycoonRecipeShop.Owned(out int day);
            List<TycoonDiscountShop.Offer> picks = TycoonDiscountShop.Today(stands.Count, day);
            var maker = World.GetExistingSystem<TycoonDiscountShopSetupSystem>();
            for (int i = 0; i < picks.Count && i < stands.Count; i++)
                TycoonGarage.Hold(EntityManager, stands[i], maker.CreateCrate(new CTycoonApplianceCrate { Appliance = picks[i].Appliance, Price = picks[i].Price, Discount = picks[i].Discount }));
            return picks.Count;
        }

        private int RestockSites()
        {
            List<Entity> shelves = Clear<CTycoonBlueprintShelf, CTycoonBlueprint>(b => b.Paid);
            if (shelves.Count == 0) return 0;
            TycoonRecipeShop.Owned(out int day);
            List<Entity> offers = TycoonSites.TodaysOffers(EntityManager, day, TycoonSites.SavedRestaurants() + TycoonCompany.SitesBought.Count, shelves.Count);
            int placed = 0;
            for (int i = 0; i < offers.Count; i++)
            {
                if (i >= shelves.Count) { TycoonSites.Destroy(EntityManager, offers[i]); continue; }
                TycoonGarage.Hold(EntityManager, shelves[i], offers[i]);
                placed++;
            }
            return placed;
        }
    }
}
