using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using PlateUpTycoon.UI;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>The RPG-style dialogue box: what a player sees when talking to the banker.</summary>
    internal static class TycoonDialogueView
    {
        internal static Vector3 BoxOffset = new Vector3(0f, -0.9f, -0.3f);
        internal static float Scale = 1f;
        internal const float SPEECH_WIDTH = 8.5f, SPEECH_HEIGHT = 2f;
        internal const float BUTTON_WIDTH = 6.5f, BUTTON_HEIGHT = 0.5f, BUTTON_PITCH = 0.58f;
        internal const float SPEECH_GAP = 0.45f;

        private class State
        {
            public DishSelectionIndicator Indicator;
            public GameObject Root;
            public PanelElement Panel;
            public LabelElement Frame;
            public ButtonElement Speech;
            public readonly List<ButtonElement> Buttons = new List<ButtonElement>();
            public readonly List<IGridItem> Items = new List<IGridItem>();
            public int Focused = -1;
            public string Signature = "";
            public bool PlacementLogged;
            public float Bottom = -SPEECH_HEIGHT / 2f;
        }

        private static readonly Dictionary<DishSelectionIndicator, State> Views = new Dictionary<DishSelectionIndicator, State>();
        private static readonly HashSet<GridMenu> HiddenGrids = new HashSet<GridMenu>();

        internal static bool IsOpen(DishSelectionIndicator indicator) => Views.ContainsKey(indicator);

        internal static void Open(DishSelectionIndicator indicator)
        {
            Forget(indicator);
            var state = new State { Indicator = indicator };
            Views[indicator] = state;
            try
            {
                object c = AccessTools.Field(typeof(DishSelectionIndicator), "Container").GetValue(indicator);
                Transform container = c as Transform ?? (c as GameObject)?.transform ?? (c as Component)?.transform;
                if ((object)container == null) { Mod.LogWarning("Dialogue box: menu has no container."); return; }

                state.Root = new GameObject("Tycoon Dialogue Box");
                state.Root.transform.SetParent(container, false);
                state.Root.transform.localPosition = BoxOffset;
                state.Root.transform.localScale = Vector3.one * Scale;

                state.Panel = ModuleDirectory.Add<PanelElement>(state.Root.transform, Vector2.zero);
                state.Frame = ModuleDirectory.Add<LabelElement>(state.Root.transform, Vector2.zero);
                state.Frame.SetLabel("");
                state.Speech = ModuleDirectory.Add<ButtonElement>(state.Root.transform, Vector2.zero);
                state.Speech.SetSize(SPEECH_WIDTH, SPEECH_HEIGHT);
                state.Speech.SetSelectable(false, true);
                state.Speech.SetStyle(ElementStyle.MainMenu);
                Mod.LogInfo("Dialogue box: opened.");
            }
            catch (System.Exception e)
            {
                Mod.LogError("Dialogue box: could not build it: " + e);
            }
        }

        internal static void Forget(DishSelectionIndicator indicator)
        {
            if (!Views.TryGetValue(indicator, out State state)) return;
            Views.Remove(indicator);
            HiddenGrids.RemoveWhere(g => g == null);
            if ((object)state.Root != null) Object.Destroy(state.Root);
        }

        internal static Vector2 BoxScreenPoint = new Vector2(0.5f, 0.5f);
        internal const float MAX_SCREEN_HEIGHT = 0.85f, MAX_SCREEN_WIDTH = 0.9f;
        private static bool _camerasLogged;

        internal static void FollowAll()
        {
            foreach (State state in Views.Values) FollowScreen(state);
        }

        private static bool Sane(Vector3 v) => v.z > 0f && v.x > -3f && v.x < 4f && v.y > -3f && v.y < 4f;

        private static void FollowScreen(State state)
        {
            if ((object)state.Root == null || (object)state.Root.transform.parent == null) return;
            Transform root = state.Root.transform;
            root.localPosition = BoxOffset;
            int sharing = DeskMenuContext.ScreenSlot(state.Indicator, out float slotX);
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one * Scale;

            Camera camera = BoxCamera(state);
            string problem = null;
            float fit = 1f, height = 0f;
            Vector3 middle = default;
            if ((object)camera == null) problem = "no camera draws it";
            else
            {
                float top = SPEECH_HEIGHT / 2f, bottom = Mathf.Min(state.Bottom, -top);
                Vector3 up = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(0f, top, 0f)));
                Vector3 down = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(0f, bottom, 0f)));
                Vector3 left = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(-SPEECH_WIDTH / 2f, 0f, 0f)));
                Vector3 right = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(SPEECH_WIDTH / 2f, 0f, 0f)));
                if (Sane(up) && Sane(down) && Sane(left) && Sane(right))
                {
                    height = Mathf.Abs(up.y - down.y);
                    float width = Mathf.Abs(right.x - left.x);
                    if (height > 0.01f && width > 0.01f) fit = Mathf.Min(1f, MAX_SCREEN_HEIGHT / height, MAX_SCREEN_WIDTH / sharing / width);
                    root.localScale = Vector3.one * (Scale * fit);
                }
                middle = camera.WorldToViewportPoint(root.TransformPoint(new Vector3(0f, (top + bottom) / 2f, 0f)));
                if (!Sane(middle)) problem = $"its middle is off the screen at ({middle.x:0.00}, {middle.y:0.00}, {middle.z:0.00})";
                else
                    root.position += camera.ViewportToWorldPoint(new Vector3(sharing > 1 ? slotX : BoxScreenPoint.x, BoxScreenPoint.y, middle.z))
                        - camera.ViewportToWorldPoint(middle);
                if (problem != null) root.localScale = Vector3.one * Scale;
            }
            if (!state.PlacementLogged)
            {
                state.PlacementLogged = true;
                Mod.LogInfo(problem != null
                    ? $"Dialogue box: kept next to the desk ({problem}); screen {Screen.width}x{Screen.height}."
                    : $"Dialogue box: moved to the middle with '{camera.name}' (its middle was at ({middle.x:0.00}, {middle.y:0.00}); {height:P0} of the screen tall, shrunk to {fit:P0}); screen {Screen.width}x{Screen.height}.");
            }
        }

        private static Camera BoxCamera(State state)
        {
            int layer = (object)state.Speech != null ? state.Speech.gameObject.layer : state.Root.layer;
            Camera named = null, ortho = null, any = null;
            foreach (Camera c in Camera.allCameras)
            {
                if (c == null || !c.isActiveAndEnabled || (c.cullingMask & (1 << layer)) == 0) continue;
                if (c.name == "UI Camera") named = c;
                else if (c.orthographic && (object)ortho == null) ortho = c;
                else if ((object)any == null) any = c;
            }
            if (!_camerasLogged)
            {
                _camerasLogged = true;
                var list = new System.Text.StringBuilder();
                foreach (Camera c in Camera.allCameras)
                    list.Append($" '{c.name}' (depth {c.depth}, {(c.orthographic ? "orthographic" : "perspective")}, draws the box: {(c.cullingMask & (1 << layer)) != 0}, at {c.transform.position});");
                Mod.LogInfo($"Dialogue box: layer {LayerMask.LayerToName(layer)} ({layer}); cameras:{list}");
            }
            return named ?? ortho ?? any;
        }

        internal static void Sync(DishSelectionIndicator indicator)
        {
            if (!Views.TryGetValue(indicator, out State state) || (object)state.Root == null) return;
            FollowScreen(state);
            if (!(AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(indicator) is GridMenu menu) || menu.Grid == null) return;
            HideGrid(menu);

            var items = new List<IGridItem>();
            foreach (ModuleInstance module in menu.Grid.Modules)
                if (module?.Item is IGridItem item && IsOurs(item)) items.Add(item);
            string signature = Signature(items);
            if (signature != state.Signature) Rebuild(state, items, signature);

            int focused = -1;
            if (menu.Grid.Selected != null && menu.Grid.Selected.Item is IGridItem selected)
                for (int i = 0; i < state.Items.Count; i++)
                    if (Same(state.Items[i], selected)) { focused = i; break; }
            if (focused != state.Focused)
            {
                if (state.Focused >= 0 && state.Focused < state.Buttons.Count) state.Buttons[state.Focused].LoseFocus();
                if (focused >= 0 && focused < state.Buttons.Count) state.Buttons[focused].GainFocus();
                state.Focused = focused;
            }
        }

        private static void Rebuild(State state, List<IGridItem> items, string signature)
        {
            state.Signature = signature;
            state.PlacementLogged = false;
            foreach (ButtonElement b in state.Buttons) if ((object)b != null) Object.Destroy(b.gameObject);
            state.Buttons.Clear();
            state.Items.Clear();
            state.Items.AddRange(items);
            state.Focused = -1;

            DeskMenuContext.Current = state.Indicator;
            PurchasingDeskMenu.DeskKind kind = PurchasingDeskMenu.KindOf(state.Indicator);
            string speech = kind == PurchasingDeskMenu.DeskKind.Chef ? TycoonChefMenu.SpeechFor(items)
                : kind == PurchasingDeskMenu.DeskKind.Cards ? TycoonCardMarketMenu.SpeechFor(items)
                : kind == PurchasingDeskMenu.DeskKind.Discount ? TycoonDiscountMenu.SpeechFor(items)
                : TycoonBankMenu.SpeechFor(items);
            state.Speech.SetLabel(speech);

            float y = -(SPEECH_HEIGHT / 2f + SPEECH_GAP + BUTTON_HEIGHT / 2f);
            foreach (IGridItem item in items)
            {
                ButtonElement button = ModuleDirectory.Add<ButtonElement>(state.Root.transform, new Vector2(0f, y));
                button.SetSize(BUTTON_WIDTH, BUTTON_HEIGHT);
                button.SetLabel(LabelFor(state.Indicator, item));
                button.SetStyle(ElementStyle.MainMenu);
                button.transform.localPosition = new Vector3(0f, y, 0f);
                IGridItem picked = item;
                button.OnActivate += () => Click(state, picked);
                state.Buttons.Add(button);
                y -= BUTTON_PITCH;
            }
            state.Bottom = y + BUTTON_PITCH - BUTTON_HEIGHT / 2f;
            FitPanel(state, state.Bottom);
            Mod.LogInfo($"Dialogue box: page with {items.Count} choice(s): \"{speech}\".");
        }

        private static void Click(State state, IGridItem item)
        {
            try
            {
                if (!(AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(state.Indicator) is GenericGridMenu menu)) return;
                Mod.LogInfo($"Dialogue box: clicked \"{LabelFor(state.Indicator, item)}\".");
                AccessTools.Method(typeof(GenericGridMenu), "OnSelect").Invoke(menu, new object[] { item });
                Sync(state.Indicator);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Dialogue box: click failed: " + e.Message);
            }
        }

        private static void FitPanel(State state, float bottom)
        {
            if ((object)state.Panel == null || (object)state.Frame == null) return;
            try
            {
                float top = SPEECH_HEIGHT / 2f;
                state.Frame.SetSize(SPEECH_WIDTH, top - bottom);
                state.Frame.transform.localPosition = new Vector3(0f, (top + bottom) / 2f, 0f);
                state.Panel.SetTarget(state.Frame);
                Vector3 p = state.Panel.transform.localPosition;
                state.Panel.transform.localPosition = new Vector3(p.x, p.y, p.z + PANEL_DEPTH);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Dialogue box: could not size the panel: " + e.Message);
            }
        }

        internal const float PANEL_DEPTH = 0.1f;

        private static bool IsChef(DishSelectionIndicator indicator) => PurchasingDeskMenu.KindOf(indicator) == PurchasingDeskMenu.DeskKind.Chef;

        private static string LabelFor(DishSelectionIndicator indicator, IGridItem item) =>
            IsChef(indicator) ? TycoonChefMenu.LabelFor(item)
            : PurchasingDeskMenu.KindOf(indicator) == PurchasingDeskMenu.DeskKind.Cards ? TycoonCardMarketMenu.LabelFor(item)
            : PurchasingDeskMenu.KindOf(indicator) == PurchasingDeskMenu.DeskKind.Discount ? TycoonDiscountMenu.LabelFor(item)
            : TycoonBankMenu.LabelFor(item);

        private static bool IsOurs(IGridItem item) =>
            item is GridItemBankChoice || item is GridItemBankConfirm || item is GridItemBankTip || item is GridItemDeskBack || item is GridItemDeskClose
            || item is GridItemChefBuy || item is GridItemChefConfirm || item is GridItemCardBuy || item is GridItemCardConfirm || item is GridItemCrateBuy || item is GridItemCrateConfirm;

        private static string Signature(List<IGridItem> items)
        {
            var sb = new System.Text.StringBuilder();
            foreach (IGridItem item in items) sb.Append(Key(item)).Append('|');
            return sb.ToString();
        }

        private static string Key(IGridItem item)
        {
            switch (item)
            {
                case GridItemBankChoice c: return $"C{c.Kind}:{c.Amount}:{c.Days}";
                case GridItemBankConfirm c: return $"K{c.Value}";
                case GridItemBankTip t: return $"T{t.Page}";
                case GridItemChefBuy b: return $"S{b.Dish}";
                case GridItemChefConfirm c: return $"Y{c.Dish}";
                case GridItemCardBuy b: return $"D{b.Card}";
                case GridItemCardConfirm c: return $"E{c.Card}";
                case GridItemCrateBuy b: return $"W{b.Appliance}";
                case GridItemCrateConfirm c: return $"V{c.Appliance}";
                case GridItemDeskBack b: return "B" + b.Title;
                case GridItemDeskClose _: return "X";
                default: return item.GetType().Name;
            }
        }

        private static bool Same(IGridItem a, IGridItem b) => a.GetType() == b.GetType() && Key(a) == Key(b);

        private static void HideGrid(GridMenu menu)
        {
            if (menu == null || HiddenGrids.Contains(menu)) return;
            HiddenGrids.Add(menu);
            try
            {
                if ((object)menu.Panel != null) menu.Panel.gameObject.SetActive(false);
                foreach (ModuleInstance module in menu.Grid.Modules)
                {
                    if (!(module?.Module is Component component) || (object)component == null) continue;
                    foreach (Renderer r in component.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Dialogue box: could not hide the tile page: " + e.Message);
            }
        }
    }

    /// <summary>Dialogue box: after the menu has handled its input, update the box.</summary>
    [HarmonyPatch(typeof(DishSelectionIndicator), "TakeInput")]
    public static class TycoonDialogueSyncPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance)
        {
            if (!TycoonDialogueView.IsOpen(__instance)) return;
            try { TycoonDialogueView.Sync(__instance); }
            catch (System.Exception e) { Mod.LogError("Dialogue box: update failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(DishSelectionIndicator), "OnDestroy")]
    public static class TycoonDialogueForgetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance) => TycoonDialogueView.Forget(__instance);
    }
}
