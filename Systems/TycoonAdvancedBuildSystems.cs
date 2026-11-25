using HarmonyLib;
using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Reflection;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Advanced build mode is unlocked in Tycoon.</summary>
    public static class TycoonAdvancedBuild
    {
        internal static void Ensure(EntityManager em)
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(CUpgradeAdvancedBuildMode));
            if (!q.IsEmpty) return;
            em.CreateEntity(typeof(CUpgradeAdvancedBuildMode), typeof(CDoNotPersist));
            Mod.LogInfo("Tycoon: advanced build mode unlocked.");
        }
    }

    public class TycoonAdvancedBuildSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _upgrade;

        protected override void Initialise()
        {
            base.Initialise();
            _upgrade = GetEntityQuery(new QueryHelper().All(typeof(CUpgradeAdvancedBuildMode)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (_upgrade.IsEmpty) TycoonAdvancedBuild.Ensure(EntityManager);
        }
    }

    [HarmonyPatch]
    public static class TycoonAdvancedBuildHqPatch
    {
        private static MethodBase Target => AccessTools.TypeByName("Kitchen.CreateLoadoutRoom") is System.Type t ? AccessTools.Method(t, "OnUpdate") : null;

        private static bool Prepare() => Target != null;

        private static MethodBase TargetMethod() => Target;

        [HarmonyPrefix]
        private static void Prefix(object __instance)
        {
            if (!TycoonSession.IsActive || !(__instance is ComponentSystemBase system)) return;
            try { TycoonAdvancedBuild.Ensure(system.EntityManager); }
            catch (System.Exception e) { Mod.LogWarning("Tycoon: could not unlock advanced build mode in the HQ: " + e.Message); }
        }
    }
}
