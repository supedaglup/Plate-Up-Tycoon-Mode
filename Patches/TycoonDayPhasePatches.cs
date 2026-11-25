using HarmonyLib;
using Kitchen;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>Holds night back during Closing.</summary>
    [HarmonyPatch(typeof(AdvanceTime), "OnUpdate")]
    public static class TycoonHoldNightPatch
    {
        private static readonly MethodInfo GameTime = AccessTools.DeclaredMethod(typeof(GenericSystemBase), "get_Time");

        [HarmonyPrefix]
        private static bool Prefix(AdvanceTime __instance)
        {
            if (!TycoonDays.Active || !TycoonDayPhases.IsDay || TycoonDayPhases.ClosingDone) return true;

            EntityManager em = __instance.EntityManager;
            using EntityQuery query = em.CreateEntityQuery(typeof(STime));
            if (query.IsEmpty) return true;
            Entity entity = query.GetSingletonEntity();
            STime time = em.GetComponentData<STime>(entity);
            if (time.ForcePause || time.DayLength <= 0f) return true;

            float dt = ((SGameTime)GameTime.Invoke(__instance, null)).DeltaTime;
            if (time.TimeOfDayUnbounded + dt / time.DayLength < 1f) return true;

            time.TimeOfDayUnbounded = 1f;
            time.TimeOfDay = 1f;
            em.SetComponentData(entity, time);
            return false;
        }
    }

    /// <summary>The top bar covers the whole Tycoon day, on every player's screen.</summary>
    public static class TycoonDayBar
    {
        private const int CODE_BASE = -1000000000;
        private const int CODE_SPAN = 100000000;
        private const float LABEL_GAP = 0.05f;
        private static System.Type LabelType;
        private static float _labelDrop;

        private static readonly MethodInfo SetBarMethod = AccessTools.Method(typeof(TimeDisplayView), "SetBar");
        private static readonly Dictionary<Component, float> RushHome = new Dictionary<Component, float>();
        private static TimeDisplayView _view;
        private static GameObject _openingLabel, _closingLabel, _openingLine, _closingLine;
        private static GameObject _contractLine, _contractLabel;
        private static bool _failed;
        private static bool _seen;
        private static float _opening, _closing;

        internal static bool IsDay { get; private set; }
        internal static float Fill { get; private set; }
        internal static int Stars { get; private set; } = TycoonEconomy.MIN_STARS;

        internal static bool Seen
        {
            get
            {
                if (GameInfo.CurrentScene != SceneType.Kitchen) _seen = false;
                return _seen;
            }
        }

        public static int Encode(bool isDay, int stars, float opening, float closing)
        {
            int value = Permille(opening) * 1000 + Permille(closing);
            value = (value * 10 + TycoonEconomy.ClampStars(stars)) * 2 + (isDay ? 1 : 0);
            return CODE_BASE - value;
        }

        private static int Permille(float fraction) => Mathf.Clamp(Mathf.RoundToInt(fraction * 1000f), 0, 999);

        internal static bool TryApply(int process, float progress)
        {
            if (process > CODE_BASE || process <= CODE_BASE - CODE_SPAN) return false;
            int value = CODE_BASE - process;
            IsDay = value % 2 == 1;
            value /= 2;
            Stars = value % 10;
            value /= 10;
            _opening = value / 1000 / 1000f;
            _closing = value % 1000 / 1000f;
            Fill = progress;
            _seen = true;
            Refresh();
            return true;
        }

        internal static void Remember(TimeDisplayView view)
        {
            if (_view == view) return;
            _view = view;
            _openingLabel = _closingLabel = _openingLine = _closingLine = null;
            _contractLine = _contractLabel = null;
            RushHome.Clear();
        }

        private static void Refresh()
        {
            if (_view == null || _failed || !IsDay) return;
            try
            {
                SetBarMethod.Invoke(_view, new object[] { Fill });
                Layout();
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("Top bar: could not show Opening and Closing: " + e);
            }
        }

        private static void Layout()
        {
            Component bar = Field<Component>("ProgressBar");
            Component text = Field<Component>("Ticks");
            if (bar == null) return;

            float width = FieldFloat("BarFullWidth");
            float left = FieldFloat("BarFullX") - width / 2f;
            float service = _opening;
            float serviceEnd = 1f - _closing;
            Vector3 barPosition = bar.transform.localPosition;

            if (_openingLine == null)
            {
                _openingLine = Line(bar);
                _closingLine = Line(bar);
                _openingLabel = text != null ? Label(text, bar, "OPENING") : null;
                _closingLabel = text != null ? Label(text, bar, "CLOSING") : null;
                _labelDrop = LabelDrop(bar, _closingLabel);
                Mod.LogInfo($"Top bar layout: bar x={barPosition.x:0.###} y={barPosition.y:0.###} width={width:0.###}, label drop {_labelDrop:0.###}, text source {text?.GetType().Name ?? "none"}.");
            }

            Place(_openingLine, barPosition, left + width * service, 0f, _opening > 0f);
            Place(_closingLine, barPosition, left + width * serviceEnd, 0f, _closing > 0f);
            Place(_openingLabel, barPosition, left + width * service / 2f, _labelDrop, _opening > 0f);
            Place(_closingLabel, barPosition, left + width * (serviceEnd + 1f) / 2f, _labelDrop, _closing > 0f);

            bool contract = int.TryParse(TycoonNetData.Get("ct") ?? "", out int permille);
            if (contract && _contractLine == null)
            {
                _contractLine = Line(bar);
                Component shape = _contractLine.GetComponent(bar.GetType());
                shape.GetType().GetProperty("Width")?.SetValue(shape, 0.05f);
                shape.GetType().GetProperty("Color")?.SetValue(shape, new Color(1f, 0.8f, 0.15f, 1f));
                _contractLabel = text != null ? Label(text, bar, "CONTRACT") : null;
            }
            float arrival = left + width * (service + permille / 1000f * (serviceEnd - service));
            Place(_contractLine, barPosition, arrival, 0f, contract);
            Place(_contractLabel, barPosition, arrival, -_labelDrop, contract);

            if (AccessTools.Field(typeof(TimeDisplayView), "RushMarkers")?.GetValue(_view) is System.Collections.IEnumerable markers)
            {
                foreach (object o in markers)
                {
                    if (!(o is Component marker)) continue;
                    Transform t = marker.transform;
                    if (!RushHome.TryGetValue(marker, out float home)) RushHome[marker] = home = t.localPosition.x;
                    float inService = (home - left) / width;
                    float x = left + width * (service + inService * (serviceEnd - service));
                    t.localPosition = new Vector3(x, t.localPosition.y, t.localPosition.z);
                }
            }
        }

        private static void Place(GameObject o, Vector3 barPosition, float x, float drop, bool show)
        {
            if (o == null) return;
            if (o.activeSelf != show) o.SetActive(show);
            o.transform.localPosition = new Vector3(x, barPosition.y - drop, barPosition.z - 0.01f);
        }

        private static float LabelDrop(Component bar, GameObject label)
        {
            float barHeight = bar.GetType().GetProperty("Height")?.GetValue(bar) is float h ? h : 0.4f;
            float textHeight = 0.3f;
            if (label != null)
            {
                Component text = label.GetComponent(LabelType);
                if (text != null && LabelType.GetProperty("preferredHeight")?.GetValue(text) is float preferred && preferred > 0f)
                    textHeight = preferred * label.transform.localScale.y;
            }
            return barHeight / 2f + textHeight / 2f + LABEL_GAP;
        }

        private static GameObject Line(Component bar)
        {
            GameObject line = Object.Instantiate(bar.gameObject, bar.transform.parent, false);
            line.name = "Tycoon phase line";
            Component shape = line.GetComponent(bar.GetType());
            shape.GetType().GetProperty("Width")?.SetValue(shape, 0.02f);
            shape.GetType().GetProperty("Color")?.SetValue(shape, Color.white);
            return line;
        }

        private static GameObject Label(Component source, Component bar, string caption)
        {
            GameObject label = Object.Instantiate(source.gameObject, bar.transform.parent, false);
            label.name = "Tycoon phase label " + caption;
            label.SetActive(true);
            Component text = label.GetComponent(source.GetType());
            System.Type type = text.GetType();
            LabelType = type;
            Mod.LogInfo($"Top bar label: source spacing {type.GetProperty("characterSpacing")?.GetValue(text)}, font size {type.GetProperty("fontSize")?.GetValue(text)}, scale {label.transform.localScale}.");
            type.GetProperty("characterSpacing")?.SetValue(text, 0f);
            type.GetProperty("text")?.SetValue(text, caption);
            type.GetProperty("enableWordWrapping")?.SetValue(text, false);
            PropertyInfo alignment = type.GetProperty("alignment");
            if (alignment != null) alignment.SetValue(text, System.Enum.ToObject(alignment.PropertyType, 514));
            return label;
        }

        private static T Field<T>(string name) where T : class => AccessTools.Field(typeof(TimeDisplayView), name)?.GetValue(_view) as T;
        private static float FieldFloat(string name) => AccessTools.Field(typeof(TimeDisplayView), name)?.GetValue(_view) is float f ? f : 0f;
    }

    [HarmonyPatch(typeof(TimeDisplayView), "SetBar")]
    public static class TycoonDayBarFillPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref float fraction)
        {
            if (TycoonDayBar.Seen && TycoonDayBar.IsDay) fraction = TycoonDayBar.Fill;
        }
    }

    [HarmonyPatch(typeof(TimeDisplayView), "UpdateData")]
    public static class TycoonDayBarViewPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TimeDisplayView __instance) => TycoonDayBar.Remember(__instance);
    }
}
