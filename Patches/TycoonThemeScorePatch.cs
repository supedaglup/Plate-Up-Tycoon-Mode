using HarmonyLib;
using Kitchen;
using KitchenData;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Patches
{
    /// <summary>Theme points count only for the theme whose card is on.</summary>
    [HarmonyPatch(typeof(DetermineDecorationLevels), "OnUpdate")]
    public static class TycoonThemeScorePatch
    {
        private static bool _failed;

        [HarmonyPostfix]
        private static void Postfix(DetermineDecorationLevels __instance)
        {
            if (_failed || !TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                DecorationType on = 0;
                using (EntityQuery status = em.CreateEntityQuery(typeof(SGlobalStatusList)))
                    if (!status.IsEmpty) on = status.GetSingleton<SGlobalStatusList>().Theme;
                using EntityQuery scores = em.CreateEntityQuery(typeof(CDecorationScore));
                using NativeArray<Entity> entities = scores.ToEntityArray(Allocator.Temp);
                foreach (Entity e in entities)
                {
                    DynamicBuffer<CDecorationScore> buffer = em.GetBuffer<CDecorationScore>(e);
                    for (int i = buffer.Length - 1; i >= 0; i--)
                        if ((buffer[i].Theme & on) == 0) buffer.RemoveAt(i);
                }
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("Theme points: could not keep only the theme card's points: " + e.Message);
            }
        }
    }
}
