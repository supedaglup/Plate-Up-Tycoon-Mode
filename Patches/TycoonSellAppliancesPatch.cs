using HarmonyLib;
using Kitchen;
using PlateUpTycoon.Systems;

namespace PlateUpTycoon.Patches
{
    /// <summary>Appliances left on the curb are sold for half their price at the start of the day.</summary>
    [HarmonyPatch(typeof(DestroyAppliancesAtDay), "OnUpdate")]
    public static class TycoonSellAppliancesPatch
    {
        [HarmonyPrefix]
        private static void Prefix(DestroyAppliancesAtDay __instance)
        {
            if (!TycoonSession.IsActive) return;
            try { TycoonCurbSale.SellCurb(__instance.EntityManager); }
            catch (System.Exception e) { Mod.LogWarning("Curb sale: could not sell the street's appliances: " + e.Message); }
        }
    }
}
