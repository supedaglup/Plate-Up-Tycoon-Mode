using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.Customs
{
#pragma warning disable CS0618
    public class CardItem : CustomItem
    {
        public override string UniqueNameID => "TycoonCardItem";
        public override ItemCategory ItemCategory => ItemCategory.Generic;

        private const float WIDTH = 0.32f, HEIGHT = 0.44f, THICK = 0.016f, CORNER = 0.025f, BORDER = 0.022f, LEAN = 22f;
        public const string FRAME = "Frame";
        private const string CARD = "Card", FALLBACK = "Fallback", REAL = "Tycoon real card";

        private static GameObject _prefab;
        private static GameObject _hider;
        private static readonly Dictionary<Color, Material> FrameMaterials = new Dictionary<Color, Material>();

        public override GameObject Prefab => _prefab;

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (_prefab == null)
            {
                try { _prefab = Build(); }
                catch (System.Exception e) { Mod.LogError("Card item: could not build the card model: " + e); }
            }
            base.AttachDependentProperties(gameData, gameDataObject);
        }

        private static GameObject Build()
        {
            _hider = new GameObject("Tycoon prefab holder");
            _hider.SetActive(false);
            Object.DontDestroyOnLoad(_hider);

            var root = new GameObject("Tycoon Card");
            root.transform.SetParent(_hider.transform, false);
            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            card.transform.localRotation = Quaternion.Euler(LEAN, 0f, 0f);
            var fallback = new GameObject(FALLBACK);
            fallback.transform.SetParent(card.transform, false);

            Material frame = FrameMaterial(new Color(0.55f, 0.35f, 0.75f));
            Material face = MaterialUtils.CreateFlat("Tycoon Card Face", new Color(0.98f, 0.96f, 0.91f), 0.1f, 0f);
            Material title = MaterialUtils.CreateFlat("Tycoon Card Title", new Color(0.25f, 0.23f, 0.26f), 0f, 0f);
            Material lines = MaterialUtils.CreateFlat("Tycoon Card Lines", new Color(0.62f, 0.60f, 0.62f), 0f, 0f);

            float mid = HEIGHT / 2f, top = -THICK / 2f - 0.004f;
            Box(fallback, FRAME + " A", frame, new Vector3(0f, mid, 0f), new Vector3(WIDTH, HEIGHT - 2f * CORNER, THICK));
            Box(fallback, FRAME + " B", frame, new Vector3(0f, mid, 0f), new Vector3(WIDTH - 2f * CORNER, HEIGHT, THICK));
            float fw = WIDTH - 2f * BORDER, fh = HEIGHT - 2f * BORDER, fc = CORNER * 0.6f;
            Box(fallback, "Face A", face, new Vector3(0f, mid, -0.003f), new Vector3(fw, fh - 2f * fc, THICK));
            Box(fallback, "Face B", face, new Vector3(0f, mid, -0.003f), new Vector3(fw - 2f * fc, fh, THICK));
            GameObject badge = Box(fallback, FRAME + " Badge", frame, new Vector3(-WIDTH / 2f + 0.035f, HEIGHT - 0.035f, top), new Vector3(0.05f, 0.05f, 0.006f));
            badge.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Box(fallback, "Title", title, new Vector3(0.02f, HEIGHT - 0.07f, top), new Vector3(fw * 0.62f, 0.028f, 0.004f));
            for (int i = 0; i < 4; i++)
                Box(fallback, "Line " + i, lines, new Vector3(i == 3 ? -0.04f : 0f, HEIGHT * 0.58f - i * 0.045f, top), new Vector3(i == 3 ? fw * 0.5f : fw * 0.78f, 0.014f, 0.004f));

            root.AddComponent<DishChoiceView>();
            Mod.LogInfo("Card item: card model built.");
            return root;
        }

        public static void ShowCard(Component view, Unlock unlock)
        {
            Transform card = view.transform.Find(CARD);
            if (card == null) return;
            Transform real = card.Find(REAL);
            UnlockCardElement element = real != null ? real.GetComponentInChildren<UnlockCardElement>(true) : null;
            if (element == null)
            {
                if (real != null) Object.Destroy(real.gameObject);
                var holder = new GameObject(REAL);
                holder.transform.SetParent(card, false);
                element = ModuleDirectory.Add<UnlockCardElement>(holder.transform, Vector2.zero);
                if (element == null) return;
                foreach (Component c in holder.GetComponentsInChildren<Component>(true))
                    if (c != null && c.GetType().Name.EndsWith("Collider")) Object.Destroy(c);
                real = holder.transform;
                Fitted.Remove(view);
            }
            try { element.SetUIMode(false); } catch (System.Exception) { }
            element.SetUnlock(unlock);
            SetLayer(real, view.gameObject.layer);
            if (Fitted.Contains(view)) return;

            real.localScale = Vector3.one;
            real.localPosition = Vector3.zero;
            if (!Measure(real, out Bounds bounds)) return;
            float wanted = HEIGHT * card.lossyScale.y;
            float measured = Mathf.Max(bounds.size.y, bounds.size.x * HEIGHT / WIDTH);
            if (measured < 0.0001f) return;
            real.localScale = Vector3.one * (wanted / measured);
            if (!Measure(real, out bounds)) return;
            Vector3 middle = card.TransformPoint(new Vector3(0f, HEIGHT / 2f, 0f));
            real.position += middle - bounds.center;
            Transform fallback = card.Find(FALLBACK);
            if (fallback != null) fallback.gameObject.SetActive(false);
            Fitted.Add(view);
        }

        private static readonly HashSet<Component> Fitted = new HashSet<Component>();

        private static bool Measure(Transform t, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(false))
            {
                if (r.bounds.size.sqrMagnitude < 1e-10f) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }

        public static void Tint(Component view, Color colour)
        {
            Material m = FrameMaterial(colour);
            foreach (MeshRenderer r in view.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.name.StartsWith(FRAME)) r.sharedMaterial = m;
        }

        private static Material FrameMaterial(Color colour)
        {
            colour.a = 1f;
            Fitted.RemoveWhere(v => v == null);
            if (FrameMaterials.TryGetValue(colour, out Material m) && m != null) return m;
            m = MaterialUtils.CreateFlat("Tycoon Card Frame", colour, 0.2f, 0f);
            FrameMaterials[colour] = m;
            return m;
        }

        private static GameObject Box(GameObject parent, string name, Material material, Vector3 at, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Component collider = box.GetComponent("BoxCollider");
            if (collider != null) Object.DestroyImmediate(collider);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = at;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }
    }
#pragma warning restore CS0618
}
