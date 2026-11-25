using HarmonyLib;
using Kitchen;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Floor messes in Tycoon.</summary>
    public static class TycoonMesses
    {
        public const float WET_FLOOR_WEIGHT = 0.25f;

        public static bool IsWetFloor(int applianceID) =>
            applianceID != 0 && (applianceID == KitchenLib.References.ApplianceReferences.MopWater
                || applianceID == KitchenLib.References.ApplianceReferences.MopWaterLong);

        public static void Count(EntityManager em, EntityQuery messes, out int dirty, out int wet)
        {
            dirty = wet = 0;
            using NativeArray<Entity> all = messes.ToEntityArray(Allocator.Temp);
            foreach (Entity e in all)
            {
                if (em.HasComponent<CAppliance>(e) && IsWetFloor(em.GetComponentData<CAppliance>(e).ID)) wet++;
                else dirty++;
            }
        }

        public static bool IsWetFloor(EntityManager em, Entity e) =>
            em.HasComponent<CAppliance>(e) && IsWetFloor(em.GetComponentData<CAppliance>(e).ID);

        public static bool IsHidden(TileManager tiles, Vector3 at)
        {
            Vector3 p = at.Rounded();
            try { return !tiles.CanReach(p, p + new Vector3(0f, 0f, -1f), false); }
            catch (System.Exception) { return false; }
        }
    }
}

namespace PlateUpTycoon.Patches
{
    /// <summary>New messes only appear where customers and players can reach them.</summary>
    [HarmonyPatch(typeof(CreateNewMesses), "FindMessLocation")]
    public static class TycoonVisibleMessPatch
    {
        private const int RETRIES = 6;
        private static bool _retrying;
        private static readonly MethodInfo Find = AccessTools.Method(typeof(CreateNewMesses), "FindMessLocation");
        private static readonly PropertyInfo Tiles = AccessTools.Property(typeof(GenericSystemBase), "TileManager");

        [HarmonyPrefix]
        private static void Prefix(Vector3 output, Entity occ, bool overwrite, out (Vector3 output, Entity occ, bool overwrite) __state) =>
            __state = (output, occ, overwrite);

        [HarmonyPostfix]
        private static void Postfix(CreateNewMesses __instance, Vector3 location, Vector3 front_door, ref Vector3 output, ref Entity occ,
            int mess, ref bool overwrite, ref bool __result, (Vector3 output, Entity occ, bool overwrite) __state)
        {
            if (_retrying || !__result || !TycoonSession.IsActive || Find == null) return;
            if (!(Tiles?.GetValue(__instance) is TileManager tiles) || !TycoonMesses.IsHidden(tiles, output)) return;
            _retrying = true;
            try
            {
                for (int i = 0; i < RETRIES; i++)
                {
                    object[] args = { location, front_door, __state.output, __state.occ, mess, __state.overwrite };
                    if ((bool)Find.Invoke(__instance, args) && !TycoonMesses.IsHidden(tiles, (Vector3)args[2]))
                    {
                        output = (Vector3)args[2];
                        occ = (Entity)args[3];
                        overwrite = (bool)args[5];
                        return;
                    }
                }
                __result = false;
                Mod.LogInfo($"Messes: no mess near {location} (every free tile there is hidden behind a wall).");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Messes: could not re-roll a hidden mess tile: " + e.Message);
            }
            finally { _retrying = false; }
        }
    }
}
