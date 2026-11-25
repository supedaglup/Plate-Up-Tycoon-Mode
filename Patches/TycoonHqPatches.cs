using HarmonyLib;
using Kitchen;

namespace PlateUpTycoon.Patches
{
    /// <summary>In Tycoon Mode the HQ is built without these rooms' contents.</summary>
    internal static class HqSkip
    {
        internal static bool RunVanilla(string what)
        {
            if (!TycoonSession.IsActive) return true;
            Mod.LogInfo($"HQ: skipped {what}.");
            return false;
        }
    }

    [HarmonyPatch(typeof(CreateLayoutSlots), "OnUpdate")]
    public static class SkipLayoutSlots { [HarmonyPrefix] private static bool Prefix() => HqSkip.RunVanilla("layout pedestals"); }

    [HarmonyPatch(typeof(CreateDishOptions), "OnUpdate")]
    public static class SkipDishOptions { [HarmonyPrefix] private static bool Prefix() => HqSkip.RunVanilla("dish cabinets"); }

    [HarmonyPatch(typeof(CreateSeededRuns), "OnUpdate")]
    public static class SkipSeededRuns { [HarmonyPrefix] private static bool Prefix() => HqSkip.RunVanilla("seeded runs"); }

    [HarmonyPatch(typeof(CreateStatsRoom), "OnUpdate")]
    public static class SkipStatsRoom { [HarmonyPrefix] private static bool Prefix() => HqSkip.RunVanilla("stats/achievement room"); }

    [HarmonyPatch(typeof(CreateContractRoom), "OnUpdate")]
    public static class SkipContractRoom { [HarmonyPrefix] private static bool Prefix() => HqSkip.RunVanilla("franchise/contract room"); }

    [HarmonyPatch(typeof(CreateExpGrants), "OnUpdate")]
    public static class SkipExpGrants { [HarmonyPrefix] private static bool Prefix() => HqSkip.RunVanilla("experience grants"); }
}
