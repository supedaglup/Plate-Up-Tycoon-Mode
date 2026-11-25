using HarmonyLib;
using Kitchen;
using KitchenData;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>The Take-Out Plates rack's plates drawn light brown.</summary>
    [HarmonyPatch]
    public static class TakeOutPlateRackLook
    {
        private static bool _logged;
        private static readonly Dictionary<LimitedItemSourceView, bool> IsRack = new Dictionary<LimitedItemSourceView, bool>();

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(LimitedItemSourceView), "SetPrefab");
            yield return AccessTools.Method(typeof(LimitedItemSourceView), "SetAmount");
            System.Type data = AccessTools.Inner(typeof(LimitedItemSourceView), "ViewData");
            System.Reflection.MethodInfo update = data != null ? AccessTools.Method(typeof(LimitedItemSourceView), "UpdateData", new[] { data }) : null;
            if (update != null) yield return update;
        }

        [HarmonyPostfix]
        private static void Postfix(LimitedItemSourceView __instance)
        {
            try
            {
                if (__instance == null || !Rack(__instance)) return;
                int changed = Customs.TakeOutPlateLook.Tint(__instance.gameObject, GameData.Main);
                if (!_logged && changed > 0) { _logged = true; Mod.LogInfo($"Take-Out Plates: the rack's stack view tinted ({changed} slot(s)) - event-driven."); }
            }
            catch (System.Exception) { }
        }

        private static bool Rack(LimitedItemSourceView view)
        {
            if (IsRack.TryGetValue(view, out bool known)) return known;
            bool rack = false;
            for (Transform t = view.transform; t != null && !rack; t = t.parent)
                if (t.name.Contains("Take-Out Plates")) rack = true;
            if (IsRack.Count > 200) IsRack.Clear();
            IsRack[view] = rack;
            return rack;
        }

        internal static void Update() { }
    }
}
