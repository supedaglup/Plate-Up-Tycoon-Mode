using HarmonyLib;
using Kitchen;
using Unity.Collections;

namespace PlateUpTycoon.Patches
{
    /// <summary>Disposable Plates count as a cleaning process for the impossible-menu check.</summary>
    [HarmonyPatch(typeof(CheckSellingRequiredAppliance), "IsMissingCleaningProcess")]
    public static class TycoonCleaningCheckPatch
    {
        [HarmonyPostfix]
        private static void Postfix(NativeArray<CAppliance> current_appliances, NativeArray<CLetterAppliance> current_parcels, ref bool __result)
        {
            if (!__result || !TycoonSession.IsActive || Mod.TakeOutPlates == null) return;
            int rack = Mod.TakeOutPlates.ID;
            try
            {
                if (current_appliances.IsCreated)
                    foreach (CAppliance a in current_appliances) if (a.ID == rack) { __result = false; return; }
                if (current_parcels.IsCreated)
                    foreach (CLetterAppliance p in current_parcels) if (p.ApplianceID == rack) { __result = false; return; }
            }
            catch (System.Exception e) { Mod.LogWarning("Cleaning check: " + e.Message); }
        }
    }
}
