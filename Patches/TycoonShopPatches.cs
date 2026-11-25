using HarmonyLib;
using Kitchen;
using PlateUpTycoon.Components;
using KitchenData;
using KitchenLib.References;
using System.Collections.Generic;
using Unity.Entities;

namespace PlateUpTycoon.Patches
{
    /// <summary>No random blueprint drops in Tycoon Mode.</summary>
    [HarmonyPatch(typeof(CreateShopRequests), "OnUpdate")]
    public static class NoRandomBlueprintsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonSession.IsActive;
    }

    /// <summary>The Tycoon starting loadout: the bare minimum to run a coffee stand.</summary>
    [HarmonyPatch(typeof(CreateStartingItems), "OnUpdate")]
    public static class TycoonStartingItemsPatch
    {
        [HarmonyPrefix]
        private static void Prefix(CreateStartingItems __instance)
        {
            if (!TycoonSession.IsActive) return;

            EntityManager em = __instance.EntityManager;
            using EntityQuery scenes = em.CreateEntityQuery(typeof(SSceneData));
            if (scenes.IsEmpty) return;
            Entity scene = scenes.GetSingletonEntity();
            if (em.HasComponent<CTycoonStartingItemsAdded>(scene) || !em.HasComponent<CStartingItem>(scene)) return;

            DynamicBuffer<CStartingItem> items = em.GetBuffer<CStartingItem>(scene);
            var dropped = new List<string>();
            for (int i = 0; i < items.Length; i++) dropped.Add(Name(items[i].ID));

            STycoonRestaurantOrigin origin = TycoonNewRestaurant.Claim(em);
            var ids = new List<int>();
            if (origin.FromBlueprint)
            {
                for (int i = 0; i < items.Length; i++)
                    if (GameData.Main.TryGet<Appliance>(items[i].ID, out Appliance _)) ids.Add(items[i].ID);
                dropped.Clear();
                if (!ids.Exists(Systems.TycoonCardEffectsSystem.IsDiningTable))
                {
                    int table = Systems.TycoonCardEffectsSystem.DiningTable();
                    if (table != 0) ids.Add(table);
                }
                if (GameData.Main.TryGet<Dish>(origin.StartingDish, out Dish startDish, false))
                {
                    var needed = Systems.TycoonCardEffectsSystem.NeededFor(startDish, ids);
                    foreach (int id in needed) if (!ids.Contains(id) && GameData.Main.TryGet<Appliance>(id, out Appliance _)) ids.Add(id);
                    Mod.LogInfo($"Tycoon starting kit: {needed.Count} appliance(s) added for {TycoonRecipeShop.NameOf(startDish.ID)}.");
                }
                ids.Add(Mod.PurchasingDesk.ID);
                ids.Add(Mod.ManagementDesk.ID);
            }
            else ids.AddRange(Mod.StartingKit);

            items.Clear();
            var kit = new List<string>();
            foreach (int id in ids)
            {
                items.Add(new CStartingItem { ID = id, SkipFirstTimeInfo = true });
                kit.Add(Name(id));
            }

            em.AddComponent<CTycoonStartingItemsAdded>(scene);
            Mod.LogInfo($"Tycoon starting kit: {string.Join(", ", kit)}. "
                + $"Removed from the vanilla kit: {(dropped.Count == 0 ? "nothing" : string.Join(", ", dropped))}.");
        }

        private static string Name(int id) =>
            GameData.Main.TryGet<Appliance>(id, out Appliance a) && !string.IsNullOrEmpty(a.Name) ? a.Name : id.ToString();
    }

    /// <summary>No free table each night when none is placed.</summary>
    [HarmonyPatch(typeof(GrantNecessaryAppliances), "OnUpdate")]
    public static class NoGrantedAppliancesPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonSession.IsActive;
    }

    /// <summary>No free Booking Desk parcel in Tycoon.</summary>
    [HarmonyPatch(typeof(ProvideStartingEnvelopes), "OnUpdate")]
    public static class NoStartingBookingDeskPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonSession.IsActive;
    }
}
