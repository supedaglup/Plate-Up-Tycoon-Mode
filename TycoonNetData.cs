using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using PlateUpTycoon.Systems;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon
{
    /// <summary>Host-to-guest data: short key/value texts carried on hidden entities.</summary>
    public static class TycoonNetData
    {
        public const string DATA_MARKER = "#D";
        public const string DATA_MARKER_ALT = "#E";

        internal static bool IsData(string text) => text != null && (text.StartsWith(DATA_MARKER) || text.StartsWith(DATA_MARKER_ALT));
        internal static readonly UnityEngine.Vector3 HIDDEN = new UnityEngine.Vector3(0f, -200f, 0f);

        private static readonly Dictionary<string, string> Outgoing = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Received = new Dictionary<string, string>();

        public static bool IsHost
        {
            get
            {
                float now = UnityEngine.Time.realtimeSinceStartup;
                if (now < _hostCheckedAt + 0.5f) return _isHost;
                _hostCheckedAt = now;
                _isHost = false;
                if (!TycoonSession.IsActive) return false;
                try
                {
                    EntityManager em = KitchenLib.Utils.EntityUtils.GetEntityManager();
                    using EntityQuery players = em.CreateEntityQuery(typeof(CPlayer));
                    _isHost = !players.IsEmpty;
                }
                catch (System.Exception) { _isHost = false; }
                return _isHost;
            }
        }
        private static bool _isHost;
        private static float _hostCheckedAt = -10f;

        public static void Set(string key, string value)
        {
            if (value != null && DATA_MARKER.Length + key.Length + 1 + value.Length > 60)
                Mod.LogWarning($"Net data: '{key}' is too long and will be cut: {value}");
            Outgoing[key] = value ?? "";
        }

        public static string Get(string key)
        {
            if (IsHost) return Outgoing.TryGetValue(key, out string mine) ? mine : null;
            return Received.TryGetValue(key, out string got) ? got : null;
        }

        public static int[] Ints(string key)
        {
            string value = Get(key);
            if (string.IsNullOrEmpty(value)) return null;
            string[] parts = value.Split(',');
            var ints = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) int.TryParse(parts[i], out ints[i]);
            return ints;
        }

        internal static IEnumerable<KeyValuePair<string, string>> AllOutgoing => Outgoing;

        internal static string OutgoingValue(string key) => Outgoing.TryGetValue(key, out string v) ? v ?? "" : "";

        internal static void Receive(string text)
        {
            string body = text.Substring(DATA_MARKER.Length);
            int eq = body.IndexOf('=');
            if (eq <= 0) return;
            Received[body.Substring(0, eq)] = body.Substring(eq + 1);
        }
    }

    /// <summary>Host: keeps one carrier entity per TycoonNetData key, its text up to date.</summary>
    public class TycoonNetDataSystem : GenericSystemBase, IModSystem
    {
        private readonly Dictionary<string, Entity> _carriers = new Dictionary<string, Entity>();
        private readonly Dictionary<string, string> _sent = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> _altOf = new Dictionary<string, bool>();
        private readonly List<string> _keys = new List<string>();
        private World _world;
        private const float RESEND_SECONDS = 3f;
        private const int CHANGES_PER_FRAME = 8;
        private int _resendIndex;
        private float _resendCredit, _lastFrame = -1f;

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            if (_world != World) { _world = World; _carriers.Clear(); _sent.Clear(); _altOf.Clear(); }
            float now = Time.RealTotalTime;
            _keys.Clear();
            foreach (KeyValuePair<string, string> item in TycoonNetData.AllOutgoing) _keys.Add(item.Key);

            int changes = 0;
            foreach (string key in _keys)
            {
                string value = TycoonNetData.OutgoingValue(key);
                bool exists = _carriers.TryGetValue(key, out Entity carrier) && EntityManager.Exists(carrier);
                if (exists && _sent.TryGetValue(key, out string sent) && sent == value) continue;
                if (changes >= CHANGES_PER_FRAME) continue;
                changes++;
                if (exists) Retire(carrier);
                carrier = ManageTycoonAnnouncements.Announce(EntityManager, now, TycoonNetData.HIDDEN, "", 1e9f);
                _carriers[key] = carrier;
                Write(carrier, key, value);
            }

            float dt = _lastFrame < 0f ? 0f : UnityEngine.Mathf.Clamp(now - _lastFrame, 0f, 1f);
            _lastFrame = now;
            if (_keys.Count == 0) return;
            _resendCredit = UnityEngine.Mathf.Min(_resendCredit + dt * _keys.Count / RESEND_SECONDS, _keys.Count);
            while (_resendCredit >= 1f)
            {
                _resendCredit -= 1f;
                _resendIndex = (_resendIndex + 1) % _keys.Count;
                string key = _keys[_resendIndex];
                if (!_carriers.TryGetValue(key, out Entity carrier) || !EntityManager.Exists(carrier)) continue;
                _altOf[key] = !(_altOf.TryGetValue(key, out bool alt) && alt);
                Write(carrier, key, TycoonNetData.OutgoingValue(key));
            }
        }

        private void Write(Entity carrier, string key, string value)
        {
            string marker = _altOf.TryGetValue(key, out bool alt) && alt ? TycoonNetData.DATA_MARKER_ALT : TycoonNetData.DATA_MARKER;
            FixedString64 text = new FixedString64(Clip(marker + key + "=" + value));
            EntityManager.SetComponentData(carrier, new CLocationChoice { RestaurantName = text, RestaurantSafeName = text });
            _sent[key] = value;
        }

        private void Retire(Entity carrier)
        {
            if (EntityManager.HasComponent<CTycoonAnnouncement>(carrier))
                EntityManager.SetComponentData(carrier, new CTycoonAnnouncement { ExpiresAt = Time.RealTotalTime - 1f });
        }

        private static string Clip(string s) => s.Length > 60 ? s.Substring(0, 60) : s;
    }
}
