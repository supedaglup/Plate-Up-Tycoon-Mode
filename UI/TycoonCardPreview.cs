using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>Card Market: shows a card while a player stands next to its pedestal.</summary>
    public static class TycoonCardPreview
    {
        public const float NEAR = 1.15f;
        public const float FACING = 0.7f;
        public const float HELD = 0.4f;
        public static readonly Vector2 CARD_SCREEN_POINT = new Vector2(0.5f, 0.52f);
        public const float CARD_SCREEN_HEIGHT = 0.3f;
        private const float LOOK_EVERY = 0.15f;
        private const int UI_LAYER = 5;

        internal static readonly Dictionary<DishChoiceView, int> Cards = new Dictionary<DishChoiceView, int>();

        private static readonly System.Reflection.FieldInfo IsMine = AccessTools.Field(typeof(PlayerView), "IsMyPlayer");
        private static PlayerView[] _players = new PlayerView[0];
        private static float _nextLook;
        private static int _shownCard;
        private static GameObject _root;
        private static UnlockCardElement _card;
        private static Camera _camera;
        private static bool _failed, _logged;

        internal static void Update()
        {
            if (_failed) return;
            try
            {
                if (GameInfo.CurrentScene != SceneType.Franchise || Cards.Count == 0) { Hide(); return; }
                float now = Time.unscaledTime;
                if (now >= _nextLook)
                {
                    _nextLook = now + LOOK_EVERY;
                    _players = Object.FindObjectsOfType<PlayerView>();
                    int card = NearestCard();
                    if (card == 0) { Hide(); return; }
                    if (card != _shownCard) Show(card);
                }
                if (_root != null) Place();
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("Card preview: " + e);
                Hide();
            }
        }

        private static int NearestCard()
        {
            var dead = new List<DishChoiceView>();
            int best = 0;
            float bestDistance = NEAR * NEAR;
            foreach (KeyValuePair<DishChoiceView, int> c in Cards)
            {
                if (c.Key == null) { dead.Add(c.Key); continue; }
                if (!c.Key.gameObject.activeInHierarchy) continue;
                if (c.Key.GetComponentInParent<PlayerView>() != null) continue;
                Vector3 paper = c.Key.transform.position;
                foreach (PlayerView player in _players)
                {
                    if (player == null || IsMine == null || !(bool)IsMine.GetValue(player)) continue;
                    Vector3 d = player.transform.position - paper;
                    d.y = 0f;
                    if (d.sqrMagnitude < HELD * HELD) continue;
                    Vector3 facing = player.transform.forward; facing.y = 0f;
                    if (facing.sqrMagnitude < 0.0001f || Vector3.Dot(facing.normalized, (-d).normalized) < FACING) continue;
                    if (d.sqrMagnitude <= bestDistance) { bestDistance = d.sqrMagnitude; best = c.Value; }
                }
            }
            foreach (DishChoiceView d in dead) Cards.Remove(d);
            return best;
        }

        private static void Show(int cardID)
        {
            if (!GameData.Main.TryGet<Unlock>(cardID, out Unlock unlock, false)) { Hide(); return; }
            if (_root == null)
            {
                _root = new GameObject("Tycoon card preview");
                _root.layer = UI_LAYER;
                _card = ModuleDirectory.Add<UnlockCardElement>(_root.transform, Vector2.zero);
            }
            _root.SetActive(true);
            _card.SetUnlock(unlock);
            SetLayer(_root.transform, UI_LAYER);
            _shownCard = cardID;
            Fit();
            if (!_logged)
            {
                _logged = true;
                Mod.LogInfo($"Card preview: showing '{unlock.Name}' with camera '{(_camera != null ? _camera.name : "none")}'.");
            }
        }

        private static void Fit()
        {
            Camera camera = MenuCamera();
            if (camera == null) return;
            _root.transform.localScale = Vector3.one;
            Bounds? bounds = null;
            foreach (Renderer r in _root.GetComponentsInChildren<Renderer>(true))
            {
                if (bounds == null) bounds = r.bounds;
                else { Bounds b = bounds.Value; b.Encapsulate(r.bounds); bounds = b; }
            }
            float measured = bounds.HasValue ? Mathf.Max(bounds.Value.size.x, bounds.Value.size.y) : 0f;
            float wanted = (camera.orthographic ? camera.orthographicSize * 2f : 10f) * CARD_SCREEN_HEIGHT;
            if (measured > 0.0001f) _root.transform.localScale = Vector3.one * (wanted / measured);
        }

        private static void Place()
        {
            Camera camera = MenuCamera();
            if (camera == null) return;
            float depth = camera.nearClipPlane + 2f;
            _root.transform.position = camera.ViewportToWorldPoint(new Vector3(CARD_SCREEN_POINT.x, CARD_SCREEN_POINT.y, depth));
            _root.transform.rotation = camera.transform.rotation;
        }

        internal static Camera MenuCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled) return _camera;
            _camera = null;
            foreach (Camera c in Camera.allCameras)
            {
                if ((c.cullingMask & (1 << UI_LAYER)) == 0) continue;
                if (c.name == "UI Camera") { _camera = c; break; }
                if (_camera == null && c.orthographic) _camera = c;
            }
            return _camera;
        }

        internal static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }

        private static void Hide()
        {
            _shownCard = 0;
            if (_root != null) _root.SetActive(false);
        }
    }
}
