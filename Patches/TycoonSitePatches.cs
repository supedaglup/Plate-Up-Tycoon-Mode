using HarmonyLib;
using Kitchen;
using KitchenData;
using PlateUpTycoon.Components;
using PlateUpTycoon.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>Helpers for the HQ's restaurant pieces.</summary>
    internal static class TycoonHq
    {
        internal static Entity HeldOn<T>(EntityManager em) where T : struct, IComponentData
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(T), typeof(CItemHolder));
            if (q.IsEmpty) return Entity.Null;
            Entity item = em.GetComponentData<CItemHolder>(q.GetSingletonEntity()).HeldItem;
            return item != Entity.Null && em.Exists(item) ? item : Entity.Null;
        }

        internal static bool SiteOnBoard(EntityManager em, out CTycoonBlueprint site)
        {
            site = default;
            Entity map = HeldOn<SSelectedLayoutPedestal>(em);
            if (map == Entity.Null || !em.HasComponent<CTycoonBlueprint>(map)) return false;
            site = em.GetComponentData<CTycoonBlueprint>(map);
            return site.Paid;
        }

        internal static int MainRecipeOnPedestal(EntityManager em, out int anyDish)
        {
            anyDish = 0;
            Entity paper = HeldOn<SDishPedestal>(em);
            if (paper == Entity.Null || !em.HasComponent<CDishChoice>(paper)) return 0;
            anyDish = em.GetComponentData<CDishChoice>(paper).Dish;
            return GameData.Main.TryGet<Dish>(anyDish, out Dish dish) && TycoonRecipeShop.IsMainDish(dish) ? anyDish : 0;
        }

        internal static bool EmptySlot(EntityManager em, out CLocationChoice slot)
        {
            slot = default;
            using EntityQuery q = em.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(CLocationChoice) }, None = new ComponentType[] { typeof(CTycoonAnnouncement) } });
            using NativeArray<CLocationChoice> slots = q.ToComponentDataArray<CLocationChoice>(Allocator.Temp);
            bool found = false;
            for (int i = 0; i < slots.Length; i++)
                if ((int)slots[i].State != TycoonResumeRestaurantSystem.STATE_SAVED && (!found || slots[i].Slot < slot.Slot)) { slot = slots[i]; found = true; }
            return found;
        }

        internal static bool SavedSelected(EntityManager em)
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(SSelectedLocation));
            if (q.IsEmpty) return false;
            SSelectedLocation selected = q.GetSingleton<SSelectedLocation>();
            return selected.Valid && (int)selected.Selected.State == TycoonResumeRestaurantSystem.STATE_SAVED;
        }
    }

    /// <summary>The HQ garage exists from the start.</summary>
    [HarmonyPatch(typeof(CreateGarage), "OnUpdate")]
    public static class TycoonGaragePatch
    {
        [HarmonyPrefix]
        private static void Prefix(CreateGarage __instance)
        {
            if (!TycoonSession.IsActive) return;
            EntityManager em = __instance.EntityManager;
            using EntityQuery garage = em.CreateEntityQuery(typeof(CUpgradeHasGarage));
            if (!garage.IsEmpty) return;
            em.CreateEntity(typeof(CUpgradeHasGarage), typeof(CTycoonGarageStandIn), typeof(CDoNotPersist));
            Mod.LogInfo("HQ: the garage is open (Tycoon).");
        }
    }

    /// <summary>No crafting workshop in Tycoon.</summary>
    [HarmonyPatch(typeof(CreateWorkshop), "OnUpdate")]
    public static class TycoonNoWorkshopPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonSession.IsActive;
    }

    /// <summary>What the loading bay needs.</summary>
    [HarmonyPatch(typeof(UpdateLoadoutStatus), "OnUpdate")]
    public static class TycoonLoadoutStatusPatch
    {
        private const int NO_LAYOUT = 0x2, NO_DISH = 0x4, NO_LOCATION = 0x10;

        [HarmonyPostfix]
        private static void Postfix(UpdateLoadoutStatus __instance)
        {
            if (!TycoonSession.IsActive) return;
            EntityManager em = __instance.EntityManager;
            using EntityQuery q = em.CreateEntityQuery(typeof(SLoadoutStatus));
            if (q.IsEmpty) return;
            SLoadoutStatus status = q.GetSingleton<SLoadoutStatus>();
            int required = (int)status.Required;
            if (TycoonHq.SiteOnBoard(em, out _))
            {
                required &= ~(NO_LOCATION | NO_LAYOUT);
                if (TycoonHq.MainRecipeOnPedestal(em, out _) == 0) required |= NO_DISH;
                if (!TycoonHq.EmptySlot(em, out _)) required |= NO_LOCATION;
            }
            else if (TycoonHq.SavedSelected(em)) required = 0;
            if (required == (int)status.Required) return;
            status.Required = (SLoadoutStatus.RequiredActions)required;
            q.SetSingleton(status);
        }
    }

    /// <summary>The blueprint preview shows the site's price in the HQ.</summary>
    [HarmonyPatch(typeof(SiteView), "UpdateData")]
    public static class TycoonSitePricePatch
    {
        private static readonly System.Text.RegularExpressions.Regex Sqm = new System.Text.RegularExpressions.Regex(@"(\d+)\s*sqm");

        [HarmonyPostfix]
        private static void Postfix(SiteView __instance)
        {
            if (!TycoonSession.IsActive || GameInfo.CurrentScene != SceneType.Franchise) return;
            try
            {
                Component label = AccessTools.Field(typeof(SiteView), "Setting")?.GetValue(__instance) as Component;
                object data = AccessTools.Field(typeof(SiteView), "Data")?.GetValue(__instance);
                if (label == null || data == null) return;
                System.Reflection.PropertyInfo text = label.GetType().GetProperty("text");
                string shown = text?.GetValue(label) as string;
                if (string.IsNullOrEmpty(shown) || shown.Contains("coins")) return;
                System.Text.RegularExpressions.Match m = Sqm.Match(shown);
                if (!m.Success || !int.TryParse(m.Groups[1].Value, out int sqm)) return;
                int setting = (int)AccessTools.Field(data.GetType(), "Setting").GetValue(data);
                text.SetValue(label, $"{shown}\n{TycoonSites.Price(setting, sqm)} coins");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Sites: could not add the price to the blueprint preview: " + e.Message);
            }
        }
    }

    /// <summary>The restaurant selector: the HQ's row of save slots.</summary>
    [HarmonyPatch(typeof(ActivateSaveReload), "IsPossible")]
    public static class TycoonSlotChoicePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ActivateSaveReload __instance, ref InteractionData data, ref bool __result)
        {
            if (!TycoonSession.IsActive) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ActivateSaveReload), "Perform")]
    public static class TycoonSlotSelectPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ActivateSaveReload __instance, ref InteractionData data)
        {
            if (!TycoonSession.IsActive) return true;
            EntityManager em = __instance.EntityManager;
            if (!em.HasComponent<CLocationChoice>(data.Target)) return false;
            CLocationChoice choice = em.GetComponentData<CLocationChoice>(data.Target);
            TycoonRestaurantSelectorPopup.Ask(data.Target, choice, false);
            return false;
        }
    }

    /// <summary>Registers the restaurant selector's pop-up manager, which the game would otherwise miss.</summary>
    [HarmonyPatch(typeof(ManagePopups), "OnUpdate")]
    public static class TycoonRegisterPopupManagerPatch
    {
        private static readonly System.Reflection.FieldInfo Managers = AccessTools.Field(typeof(ManagePopups), "Managers");
        private static bool _warned;

        [HarmonyPrefix]
        private static void Prefix(ManagePopups __instance)
        {
            if (!TycoonSession.IsActive || Managers == null) return;
            object list = Managers.GetValue(__instance);
            if (!(list is System.Collections.IDictionary managers))
            {
                if (!_warned) { _warned = true; Mod.LogWarning($"HQ: the game's pop-up manager list is a {list?.GetType().Name ?? "null"}; the selector's pop-up can't be registered."); }
                return;
            }
            if (managers.Contains(TycoonRestaurantSelectorPopup.TYPE)) return;
            TycoonRestaurantSelectorPopup manager = __instance.World.GetExistingSystem<TycoonRestaurantSelectorPopup>();
            if (manager == null) return;
            managers[TycoonRestaurantSelectorPopup.TYPE] = manager;
            Mod.LogInfo("HQ: the restaurant selector's pop-up is registered with the game.");
        }
    }

    /// <summary>The restaurant selector's pop-up borrows a game pop-up's text set.</summary>
    [HarmonyPatch(typeof(Kitchen.Modules.TextPanelElement), "SetText")]
    public static class TycoonSelectorPopupTextPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string title, ref string text)
        {
            if (TycoonRestaurantSelectorPopup.Title == null || string.IsNullOrEmpty(title)) return;
            try
            {
                if (title != GameData.Main.GlobalLocalisation[TycoonRestaurantSelectorPopup.TEXT_SET].Title) return;
            }
            catch (System.Exception) { return; }
            title = TycoonRestaurantSelectorPopup.Title;
            text = TycoonRestaurantSelectorPopup.Text;
        }
    }
}
