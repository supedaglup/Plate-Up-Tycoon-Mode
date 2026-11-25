using HarmonyLib;
using Kitchen;

namespace PlateUpTycoon.Patches
{
    /// <summary>The game-over screen in Tycoon shows only the newspaper and the exit: no experience, no level rewards.</summary>
    [HarmonyPatch]
    public static class TycoonPostgamePatches
    {
        [HarmonyPatch(typeof(CreateEndgameExpReward), "OnUpdate"), HarmonyPrefix]
        private static bool SkipExp() => !TycoonSession.IsActive;

        [HarmonyPatch(typeof(CreateEndgameReward), "OnUpdate"), HarmonyPrefix]
        private static bool SkipRewards() => !TycoonSession.IsActive;

        [HarmonyPatch(typeof(CreateCardList), "OnUpdate"), HarmonyPrefix]
        private static bool SkipCards() => !TycoonSession.IsActive;

        [HarmonyPatch(typeof(CreateHeatItem), "OnUpdate"), HarmonyPrefix]
        private static bool SkipHeat() => !TycoonSession.IsActive;
    }
}
