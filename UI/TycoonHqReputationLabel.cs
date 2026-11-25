using Kitchen;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>HQ: "Total Reputation: X" on the right of every player's screen.</summary>
    public static class TycoonHqReputationLabel
    {
        public static readonly Vector2 SCREEN_POINT = new Vector2(0.96f, 0.86f);
        public const float SCALE = 2.4f;

        private static Component _text;
        private static Component _template;
        private static MoneyDisplayView _source;
        private static string _shown;
        private static bool _failed;
        private static bool _logged;

        internal static void Update()
        {
            if (_failed) return;
            try
            {
                string value = GameInfo.CurrentScene == SceneType.Franchise ? TycoonNetData.Get(Systems.TycoonCompanyTrackerSystem.NET_KEY) : null;
                if (value == null || !int.TryParse(value, out int hundredths)) { Remove(); return; }

                if ((object)_text == null || _text == null || _source == null)
                {
                    Remove();
                    if (!Make()) return;
                }
                string text = $"Total Reputation: {hundredths / 100f:0.00}";
                if (text != _shown)
                {
                    _text.GetType().GetProperty("text")?.SetValue(_text, text);
                    _shown = text;
                }
                Place();
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("HQ Total Reputation label: " + e);
            }
        }

        private static bool Make()
        {
            _source = Object.FindObjectOfType<MoneyDisplayView>();
            if (_source == null) return false;
            Component template = null;
            foreach (Component c in _source.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name.StartsWith("TextMeshPro")) { template = c; break; }
            if (template == null)
            {
                _failed = true;
                Mod.LogWarning("HQ Total Reputation label: the coin counter has no text to copy.");
                return false;
            }

            GameObject copy = Object.Instantiate(template.gameObject, _source.transform.parent, true);
            copy.name = "Tycoon total reputation label";
            foreach (MoneyDisplayView view in copy.GetComponentsInChildren<MoneyDisplayView>(true)) Object.Destroy(view);
            foreach (Transform child in copy.transform) Object.Destroy(child.gameObject);
            _text = copy.GetComponent(template.GetType());
            _template = template;
            _text.GetType().GetProperty("enableWordWrapping")?.SetValue(_text, false);
            SetEnum(_text, "alignment", "Right");
            if (copy.transform is RectTransform rect) rect.pivot = new Vector2(1f, rect.pivot.y);
            MatchSize();
            copy.SetActive(true);
            _shown = null;
            if (!_logged)
            {
                _logged = true;
                Mod.LogInfo($"HQ Total Reputation label: copied the coin counter's '{template.name}' ({template.GetType().Name}), scale {copy.transform.localScale}.");
            }
            return true;
        }

        private static void Place()
        {
            MatchSize();
            Camera camera = MenuCamera();
            if (camera == null) return;
            Transform t = _text.transform;
            float depth = Vector3.Dot(t.position - camera.transform.position, camera.transform.forward);
            if (depth <= 0f) depth = camera.nearClipPlane + 1f;
            t.position = camera.ViewportToWorldPoint(new Vector3(SCREEN_POINT.x, SCREEN_POINT.y, depth));
        }

        private static Camera _camera;
        private static bool _logged2;

        private static Camera MenuCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled) return _camera;
            _camera = null;
            int layer = _text.gameObject.layer;
            foreach (Camera c in Camera.allCameras)
            {
                if ((c.cullingMask & (1 << layer)) == 0) continue;
                if (c.name == "UI Camera") { _camera = c; break; }
                if (_camera == null) _camera = c;
            }
            if (_camera != null && !_logged2)
            {
                _logged2 = true;
                Mod.LogInfo($"HQ Total Reputation label: placed with camera '{_camera.name}' (layer {layer}).");
            }
            return _camera;
        }

        private static void MatchSize()
        {
            if (_template == null || _text == null) return;
            Vector3 want = _template.transform.lossyScale * SCALE;
            if (want.sqrMagnitude < 1e-8f) return;
            Transform parent = _text.transform.parent;
            Vector3 p = parent != null ? parent.lossyScale : Vector3.one;
            _text.transform.localScale = new Vector3(want.x / NonZero(p.x), want.y / NonZero(p.y), want.z / NonZero(p.z));
        }

        private static float NonZero(float v) => Mathf.Abs(v) < 0.0001f ? 1f : v;

        private static void SetEnum(Component c, string property, string value)
        {
            System.Reflection.PropertyInfo p = c.GetType().GetProperty(property);
            if (p == null || !p.PropertyType.IsEnum) return;
            try { p.SetValue(c, System.Enum.Parse(p.PropertyType, value)); }
            catch (System.ArgumentException) { }
        }

        private static void Remove()
        {
            if ((object)_text != null && _text != null) Object.Destroy(_text.gameObject);
            _text = null;
            _source = null;
            _template = null;
            _shown = null;
        }
    }
}
