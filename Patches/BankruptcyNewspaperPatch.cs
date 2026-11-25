using HarmonyLib;
using Kitchen;
using PlateUpTycoon.Systems;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>The game-over newspaper after a bankruptcy.</summary>
    [HarmonyPatch(typeof(NewspaperSubview), nameof(NewspaperSubview.SetLossReason))]
    public static class BankruptcyNewspaperPatch
    {
        public const string TAGLINE = "SEIZED BY THE BANK! The owners could not pay their bills, so the bank has taken the restaurant and sold everything in it.";

        [HarmonyPostfix]
        private static void Postfix(NewspaperSubview __instance, LossReason reason)
        {
            if (reason != BankruptcySystem.BANKRUPT) return;
            try
            {
                string text = TAGLINE;
                if (!string.IsNullOrEmpty(BankruptcySystem.SaleSummary)) text += " " + BankruptcySystem.SaleSummary;
                Component tagline = AccessTools.Field(typeof(NewspaperSubview), "Tagline")?.GetValue(__instance) as Component;
                tagline?.GetType().GetProperty("text")?.SetValue(tagline, text);
                Mod.LogInfo("Bankruptcy: newspaper says the bank seized the restaurant.");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Bankruptcy newspaper: " + e.Message);
            }
        }
    }
}
