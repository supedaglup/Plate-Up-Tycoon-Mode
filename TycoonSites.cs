using Kitchen;
using KitchenData;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Restaurant sites sold or leased at the HQ bank.</summary>
    public static class TycoonSites
    {
        public const int SITE_BASE = 400, SITE_PER_SQM = 25, PRICE_ROUNDING = 50;
        public static readonly (string name, float factor)[] LOCATION_FACTORS =
        {
            ("City", 1.25f), ("Alpine", 1.15f), ("Japan", 1.15f), ("Country", 0.85f), ("Autumn", 0.9f), ("Farm", 0.85f),
        };
        public const int ON_OFFER = 3;
        public const int MAX_RESTAURANTS = 5;
        public const int SMALL_SQM = 35, MEDIUM_SQM = 55;
        private const int CANDIDATES = 3;

        public static string Key(int source, int layout, int setting) => $"{source}:{layout}:{setting}";

        public static bool ParseKey(string key, out int source, out int layout, out int setting)
        {
            source = layout = setting = 0;
            string[] parts = key.Split(':');
            return parts.Length == 3 && int.TryParse(parts[0], out source) && int.TryParse(parts[1], out layout) && int.TryParse(parts[2], out setting);
        }

        public static int Price(int settingID, int sqm)
        {
            float factor = 1f;
            string name = SettingName(settingID);
            foreach ((string part, float f) in LOCATION_FACTORS)
                if (name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) { factor = f; break; }
            float price = (SITE_BASE + SITE_PER_SQM * Mathf.Max(0, sqm)) * factor;
            return Mathf.Max(PRICE_ROUNDING, Mathf.RoundToInt(price / PRICE_ROUNDING) * PRICE_ROUNDING);
        }

        public static string CanLease(TycoonBank.Standing standing, int price)
        {
            if (Full(standing)) return "full";
            if (standing.HasLoan) return "in-debt";
            if (standing.Credit < price) return "no-credit";
            return null;
        }

        public static string CanBuy(TycoonBank.Standing standing, int price) =>
            Full(standing) ? "full" : standing.Cash < price ? "poor" : null;

        private static bool Full(TycoonBank.Standing standing) => standing.Restaurants + standing.SitesBought >= MAX_RESTAURANTS;

        public static string SettingName(int settingID) =>
            GameData.Main.TryGet<RestaurantSetting>(settingID, out RestaurantSetting s) && !string.IsNullOrEmpty(s.Name) ? s.Name : "Unknown";

        private static List<int> _layouts, _settings;

        private static bool IsTurbo(RestaurantSetting s) =>
            s.ID == KitchenLib.References.RestaurantSettingReferences.MarchSettingTurbo
            || (!string.IsNullOrEmpty(s.Name) && s.Name.IndexOf("Turbo", System.StringComparison.OrdinalIgnoreCase) >= 0);

        public static List<int> Layouts { get { if (_layouts == null) Load(); return _layouts; } }
        public static List<int> Settings { get { if (_settings == null) Load(); return _settings; } }

        private static void Load()
        {
            _layouts = new List<int>();
            _settings = new List<int>();
            void AddLayout(int id) { if (id != 0 && !_layouts.Contains(id)) _layouts.Add(id); }
            void AddSetting(RestaurantSetting s)
            {
                if (s == null || _settings.Contains(s.ID) || (object)s.FixedDish != null || (object)s.ForceLayout != null) return;
                if (IsTurbo(s)) return;
                _settings.Add(s.ID);
            }
            AddLayout(AssetReference.LayoutBasic);
            if (GameData.Main.TryGet<RestaurantSetting>(AssetReference.DefaultSetting, out RestaurantSetting city)) AddSetting(city);
            foreach (LevelUpgradeSet set in GameData.Main.Get<LevelUpgradeSet>())
            {
                if (set?.Upgrades == null) continue;
                foreach (LevelUpgrade u in set.Upgrades)
                {
                    object upgrade = u?.Upgrade;
                    if (upgrade is LayoutProfile layout) AddLayout(layout.ID);
                    else if (upgrade is RestaurantSetting setting) AddSetting(setting);
                }
            }
            var names = new List<string>();
            foreach (int id in _layouts) names.Add(GameData.Main.TryGet<LayoutProfile>(id, out LayoutProfile p) && !string.IsNullOrEmpty(p.Name) ? p.Name : id.ToString());
            var places = new List<string>();
            foreach (int id in _settings) places.Add(SettingName(id));
            Mod.LogInfo($"Sites: layouts on offer: {string.Join(", ", names)}; locations: {string.Join(", ", places)}.");
        }

        public static int Size(EntityManager em, Entity map)
        {
            if (!em.HasComponent<CItemLayoutMap>(map)) return 0;
            Entity layout = em.GetComponentData<CItemLayoutMap>(map).Layout;
            if (layout == Entity.Null || !em.Exists(layout) || !em.HasComponent<CLayoutRoomTile>(layout)) return 0;
            int sqm = 0;
            DynamicBuffer<CLayoutRoomTile> tiles = em.GetBuffer<CLayoutRoomTile>(layout);
            for (int i = 0; i < tiles.Length; i++) if (Kitchen.Layouts.LayoutHelpers.IsInside(tiles[i].Type, false)) sqm++;
            return sqm;
        }

        public static Entity Make(EntityManager em, int source, int layoutID, int settingID, bool paid)
        {
            var seed = new LayoutSeed(source, new[] { layoutID });
            Entity map = seed.GenerateMap(em, settingID);
            int sqm = Size(em, map);
            em.AddComponentData(map, new CTycoonBlueprint { Source = source, Layout = layoutID, Setting = settingID, Size = sqm, Price = Price(settingID, sqm), Paid = paid });
            em.AddComponent<CDoNotPersist>(map);
            return map;
        }

        public static void Destroy(EntityManager em, Entity map)
        {
            Entity layout = em.HasComponent<CItemLayoutMap>(map) ? em.GetComponentData<CItemLayoutMap>(map).Layout : Entity.Null;
            em.DestroyEntity(map);
            if (layout != Entity.Null && em.Exists(layout)) em.DestroyEntity(layout);
        }

        public static List<Entity> TodaysOffers(EntityManager em, int day, int restaurants, int count)
        {
            var offers = new List<Entity>();
            if (Layouts.Count == 0 || Settings.Count == 0) return offers;
            for (int slot = 0; slot < count; slot++)
            {
                var random = new System.Random(day * 7919 + slot * 104729 + 31 + TycoonCompany.ShopReroll * 15485863);
                var candidates = new List<Entity>();
                var weights = new List<int>();
                for (int c = 0; c < CANDIDATES; c++)
                {
                    int source = random.Next(int.MinValue, int.MaxValue);
                    int layout = Layouts[random.Next(Layouts.Count)];
                    int setting = Settings[random.Next(Settings.Count)];
                    try
                    {
                        Entity map = Make(em, source, layout, setting, false);
                        int sqm = em.GetComponentData<CTycoonBlueprint>(map).Size;
                        candidates.Add(map);
                        weights.Add(restaurants <= 1 ? (sqm <= SMALL_SQM ? 3 : sqm <= MEDIUM_SQM ? 2 : 1) : 1);
                    }
                    catch (System.Exception e) { Mod.LogWarning($"Sites: could not make a blueprint (layout {layout}, location {setting}): {e.Message}"); }
                }
                if (candidates.Count == 0) continue;
                int total = 0;
                foreach (int w in weights) total += w;
                int roll = random.Next(total), keep = 0;
                for (int i = 0; i < weights.Count; i++) { if (roll < weights[i]) { keep = i; break; } roll -= weights[i]; }
                for (int i = 0; i < candidates.Count; i++) if (i != keep) Destroy(em, candidates[i]);
                offers.Add(candidates[keep]);
            }
            return offers;
        }

        public static World SavedWorld(int slot)
        {
            int index = 0;
            try
            {
                foreach ((World world, SaveState state) in Persistence.FullWorld.All())
                    if (++index == slot) return state == SaveState.Loaded ? world : null;
            }
            catch (System.Exception) { }
            return null;
        }

        public static int SavedRestaurants()
        {
            int n = 0;
            try
            {
                foreach ((World world, SaveState state) in Persistence.FullWorld.All())
                    if (state == SaveState.Loaded && world != null) n++;
            }
            catch (System.Exception) { }
            return n;
        }
    }

    /// <summary>What the next new restaurant should be and how a restaurant began.</summary>
    public static class TycoonNewRestaurant
    {
        internal static bool PendingFromBlueprint;
        internal static int PendingDish;
        internal static string PendingSiteKey;
        internal static int PendingSlot;
        internal static int PendingSitePrice;

        public static STycoonRestaurantOrigin Claim(EntityManager em)
        {
            using (EntityQuery q = em.CreateEntityQuery(typeof(STycoonRestaurantOrigin)))
                if (!q.IsEmpty) return q.GetSingleton<STycoonRestaurantOrigin>();
            var origin = new STycoonRestaurantOrigin
            {
                FromBlueprint = PendingFromBlueprint && PendingDish != 0,
                StartingDish = PendingFromBlueprint && PendingDish != 0 ? PendingDish : Mod.STARTING_DISH,
            };
            PendingFromBlueprint = false;
            PendingDish = 0;
            Entity e = em.CreateEntity(typeof(STycoonRestaurantOrigin));
            em.SetComponentData(e, origin);
            Mod.LogInfo($"New restaurant: {(origin.FromBlueprint ? "opened from a bought site" : "the company's coffee stand")}, starting recipe {TycoonRecipeShop.NameOf(origin.StartingDish)}.");
            return origin;
        }

        public static STycoonRestaurantOrigin Peek(EntityManager em)
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(STycoonRestaurantOrigin));
            return q.IsEmpty ? new STycoonRestaurantOrigin { StartingDish = Mod.STARTING_DISH } : q.GetSingleton<STycoonRestaurantOrigin>();
        }
    }
}
