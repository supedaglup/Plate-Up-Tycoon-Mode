using HarmonyLib;
using Kitchen;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>The queue patience bars reuse the game's small appliance progress bar.</summary>
    [HarmonyPatch(typeof(ProgressView), "UpdateData")]
    public static class PatienceBarViewPatch
    {
        public const int PATIENCE_BAR_PROCESS = -73810;
        public const float BAR_SCALE = 0.85f;

        private static readonly Dictionary<int, List<System.Action>> Trimmed = new Dictionary<int, List<System.Action>>();
        private static bool _logged;

        [HarmonyPostfix]
        private static void Postfix(ProgressView __instance, ProgressView.ViewData view_data)
        {
            if (__instance == null) return;
            int id = __instance.GetInstanceID();
            if (TycoonDayBar.TryApply(view_data.Process, view_data.Progress) || TycoonReputationBar.TryApply(view_data.Process, view_data.Progress)
                || TycoonReputationBar.TryApplyNeeded(view_data.Process, view_data.Progress))
            {
                if (Trimmed.ContainsKey(id)) return;
                Transform hidden = (Field<GameObject>(__instance, "ProgressIndicator") ?? __instance.gameObject).transform;
                Vector3 scale = hidden.localScale;
                hidden.localScale = Vector3.zero;
                Trimmed[id] = new List<System.Action> { () => hidden.localScale = scale };
                return;
            }
            if (view_data.Process != PATIENCE_BAR_PROCESS)
            {
                if (Trimmed.TryGetValue(id, out List<System.Action> undo))
                {
                    Trimmed.Remove(id);
                    foreach (System.Action step in undo) { try { step(); } catch (System.Exception) { } }
                }
                return;
            }
            if (Trimmed.ContainsKey(id)) return;
            var steps = new List<System.Action>();
            Trimmed[id] = steps;
            try { Trim(__instance, steps); }
            catch (System.Exception e) { Mod.LogWarning("Patience bar trim: " + e.Message); }
        }

        private static T Field<T>(object o, string name) where T : class => AccessTools.Field(o.GetType(), name)?.GetValue(o) as T;
        private static float FieldFloat(object o, string name) => AccessTools.Field(o.GetType(), name)?.GetValue(o) is float f ? f : 0f;

        private static float Get(Component c, string property)
        {
            PropertyInfo p = c.GetType().GetProperty(property);
            return p != null && p.GetValue(c) is float f ? f : 0f;
        }

        private static void SetF(Component c, string property, float value)
        {
            PropertyInfo p = c.GetType().GetProperty(property);
            if (p != null && p.CanWrite) p.SetValue(c, value);
        }

        private static void Trim(ProgressView view, List<System.Action> undo)
        {
            Component icon = Field<Component>(view, "Icon");
            Component bar = Field<Component>(view, "ProgressBar");
            GameObject root = Field<GameObject>(view, "ProgressIndicator") ?? view.gameObject;
            if (bar == null) return;

            Transform scaled = root.transform;
            Vector3 oldScale = scaled.localScale;
            scaled.localScale = oldScale * BAR_SCALE;
            undo.Add(() => scaled.localScale = oldScale);

            if (icon != null && icon.gameObject.activeSelf)
            {
                GameObject iconObject = icon.gameObject;
                iconObject.SetActive(false);
                undo.Add(() => iconObject.SetActive(true));
            }

            Transform barParent = bar.transform.parent;
            float barX = FieldFloat(view, "BarFullX");
            float barWidth = FieldFloat(view, "BarFullWidth");
            float barLeft = barX - barWidth / 2f, barRight = barX + barWidth / 2f;
            float barHeight = Get(bar, "Height");

            var log = new StringBuilder($"Patience bar layout: bar x={barX:0.###} width={barWidth:0.###} height={barHeight:0.###}\n");
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c == bar) continue;
                string type = c.GetType().Name;
                Transform t = c.transform;
                float w = Get(c, "Width"), h = Get(c, "Height");
                log.Append($"  {Path(t, root.transform)} [{type}] local={t.localPosition} w={w:0.###} h={h:0.###}\n");
                if (type != "Rectangle" || t.parent != barParent || bar.transform.IsChildOf(t)) continue;

                float left = t.localPosition.x - w / 2f, right = t.localPosition.x + w / 2f;
                if (right <= barLeft + 0.01f)
                {
                    GameObject slot = c.gameObject;
                    if (!slot.activeSelf) continue;
                    slot.SetActive(false);
                    undo.Add(() => slot.SetActive(true));
                    log.Append("    -> hidden (icon slot)\n");
                }
                else if (left < barLeft - 0.01f && w > barWidth)
                {
                    float margin = Mathf.Max(0f, (h - barHeight) / 2f);
                    float keepRight = Mathf.Max(right, barRight + margin);
                    float newLeft = barLeft - margin;
                    Component shape = c;
                    Vector3 oldPos = t.localPosition;
                    float oldWidth = w;
                    undo.Add(() => { SetF(shape, "Width", oldWidth); shape.transform.localPosition = oldPos; });
                    SetF(c, "Width", keepRight - newLeft);
                    t.localPosition = new Vector3((keepRight + newLeft) / 2f, t.localPosition.y, t.localPosition.z);
                    log.Append($"    -> shrunk to the bar (w={keepRight - newLeft:0.###})\n");
                }
            }
            if (!_logged) { _logged = true; Mod.LogInfo(log.ToString()); }
        }

        private static string Path(Transform t, Transform root)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root.parent; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
