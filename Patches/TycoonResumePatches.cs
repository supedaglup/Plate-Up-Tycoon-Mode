using HarmonyLib;
using Kitchen;
using PlateUpTycoon.Components;
using PlateUpTycoon.Systems;
using Unity.Entities;

namespace PlateUpTycoon.Patches
{
    /// <summary>Starts a new or saved restaurant when the loading bay fills.</summary>
    [HarmonyPatch(typeof(BeginRestaurant), "OnUpdate")]
    public static class TycoonResumeRestaurantPatch
    {
        private static bool _warned;

        [HarmonyPrefix]
        private static bool Prefix(BeginRestaurant __instance)
        {
            if (!TycoonSession.IsActive) return true;
            EntityManager em = __instance.EntityManager;

            using (EntityQuery transitions = em.CreateEntityQuery(typeof(SPerformSceneTransition)))
                if (!transitions.IsEmpty) return false;

            if (TycoonHq.SiteOnBoard(em, out CTycoonBlueprint site))
            {
                int dish = TycoonHq.MainRecipeOnPedestal(em, out int anyDish);
                if (dish == 0 || !TycoonHq.EmptySlot(em, out CLocationChoice empty))
                {
                    if (!_warned)
                    {
                        _warned = true;
                        Mod.LogWarning(dish == 0
                            ? $"HQ: a new restaurant needs a main recipe on the recipe pedestal ({(anyDish == 0 ? "nothing there" : TycoonRecipeShop.NameOf(anyDish) + " is not a main dish")})."
                            : $"HQ: no free save slot for a new restaurant (at most {TycoonSites.MAX_RESTAURANTS}).");
                    }
                    return false;
                }
                _warned = false;
                using (EntityQuery q = em.CreateEntityQuery(typeof(SSelectedLocation)))
                    if (!q.IsEmpty) q.SetSingleton(new SSelectedLocation { Valid = true, Selected = empty });
                TycoonNewRestaurant.PendingFromBlueprint = true;
                TycoonNewRestaurant.PendingDish = dish;
                TycoonNewRestaurant.PendingSiteKey = TycoonSites.Key(site.Source, site.Layout, site.Setting);
                TycoonNewRestaurant.PendingSlot = empty.Slot;
                TycoonNewRestaurant.PendingSitePrice = site.Price;
                Mod.LogInfo($"HQ: opening a new restaurant in slot {empty.Slot}: {TycoonSites.SettingName(site.Setting)}, {site.Size} sqm, starting recipe {TycoonRecipeShop.NameOf(dish)}.");
                return true;
            }

            TycoonNewRestaurant.PendingFromBlueprint = false;
            TycoonNewRestaurant.PendingSiteKey = null;
            using EntityQuery locations = em.CreateEntityQuery(typeof(SSelectedLocation));
            if (locations.IsEmpty) return true;
            SSelectedLocation selected = locations.GetSingleton<SSelectedLocation>();
            if (!selected.Valid || (int)selected.Selected.State != TycoonResumeRestaurantSystem.STATE_SAVED) return true;

            Entity transition = em.CreateEntity(typeof(SPerformSceneTransition), typeof(CDoNotPersist));
            em.SetComponentData(transition, new SPerformSceneTransition { NextScene = SceneType.LoadFullAutosave });
            Mod.LogInfo($"HQ: loading the saved restaurant (slot {selected.Selected.Slot}, day {selected.Selected.Day}).");
            return false;
        }
    }
}
