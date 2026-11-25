using Kitchen;
using KitchenData;
using KitchenLib.References;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Wallpaper and flooring at the Purchasing Desk.</summary>
    public static class TycoonDecorShop
    {
        public struct Offer
        {
            public int DecorID;
            public string Name;
            public int Cost;
            public float ReputationPerDay;
            public LayoutMaterialType Type;
            public int ApplicatorID;
        }

        public static readonly int[] TIER_COST = { 0, 50, 90, 140, 220 };
        public static readonly float[] TIER_REPUTATION = { 0f, 1f, 2f, 3f, 5f };

        private static readonly (string reference, string name, int tier)[] Table =
        {
            ("FloorLinoBlue", "Blue Lino Floor", 1),
            ("FloorTilesSimple", "Simple Tile Floor", 1),
            ("FloorPlanksTattered", "Rough Plank Floor", 1),
            ("FloorCarpetBlue", "Blue Carpet", 2),
            ("FloorCarpetRed", "Red Carpet", 2),
            ("FloorCarpetOddBeige", "Beige Carpet", 2),
            ("FloorTilesSlate", "Slate Tile Floor", 2),
            ("FloorTilesTerracotta", "Terracotta Tile Floor", 2),
            ("FloorTilesCobblestone", "Cobblestone Floor", 2),
            ("FloorWoodNeatPlanks", "Neat Wood Floor", 3),
            ("FloorPlanksSmart", "Smart Plank Floor", 3),
            ("FloorWoodChevrons", "Chevron Wood Floor", 3),
            ("FloorTilesHex", "Hexagon Tile Floor", 3),
            ("FloorTilesBlackandWhite", "Black and White Tile Floor", 3),
            ("FloorMarble", "Marble Floor", 4),
            ("FloorMarbleDark", "Dark Marble Floor", 4),

            ("WallpaperBasicBeige", "Plain Beige Wallpaper", 1),
            ("WallpaperBasicBlue", "Plain Blue Wallpaper", 1),
            ("WallpaperBasicDeepRed", "Plain Deep Red Wallpaper", 1),
            ("WallpaperBasicGreen", "Plain Green Wallpaper", 1),
            ("WallpaperBasicRed", "Plain Red Wallpaper", 1),
            ("WallpaperBasicYellow", "Plain Yellow Wallpaper", 1),
            ("WallpaperStripesBlue", "Blue Striped Wallpaper", 2),
            ("WallpaperStripesGreen", "Green Striped Wallpaper", 2),
            ("WallpaperStripesOrange", "Orange Striped Wallpaper", 2),
            ("WallpaperStripesRed", "Red Striped Wallpaper", 2),
            ("WallpaperTilesBlue", "Blue Tiled Wall", 2),
            ("WallpaperTilesRed", "Red Tiled Wall", 2),
            ("WallpaperPatternFloral", "Floral Wallpaper", 3),
            ("WallpaperLeaves", "Leaf Wallpaper", 3),
            ("WallpaperTrees", "Tree Wallpaper", 3),
            ("WallpaperHearts", "Heart Wallpaper", 3),
            ("WallpaperHeartsIcons", "Heart Pattern Wallpaper", 3),
            ("WallpaperPatternBones", "Bone Pattern Wallpaper", 3),
            ("WallpaperPatternSnow", "Snowflake Wallpaper", 3),
            ("WallpaperTurkey", "Turkey Wallpaper", 3),
            ("WallpaperCNY1", "Lunar New Year Wallpaper", 4),
            ("WallpaperCNY2", "Lunar Lantern Wallpaper", 4),
            ("WallpaperCNY3", "Lunar Dragon Wallpaper", 4),
        };

        private static List<Offer> _offers;
        private static Dictionary<int, Offer> _byID;

        public static IReadOnlyList<Offer> Offers { get { Build(); return _offers; } }

        public static bool IsDecor(int id) { Build(); return _byID.ContainsKey(id); }

        public static bool TryGetAny(int id, out Offer offer) { Build(); return _byID.TryGetValue(id, out offer); }

        public static bool TryGet(int id, out Offer offer)
        {
            offer = default;
            return TycoonUnlocks.DecorForSale && TryGetAny(id, out offer);
        }

        public static float ReputationPerDay(int decorID) => TryGetAny(decorID, out Offer o) ? o.ReputationPerDay : 0f;

        private static void Build()
        {
            if (_offers != null) return;
            _offers = new List<Offer>();
            _byID = new Dictionary<int, Offer>();
            var missing = new List<string>();
            foreach ((string reference, string name, int tier) in Table)
            {
                System.Reflection.PropertyInfo p = typeof(DecorReferences).GetProperty(reference);
                if (p == null || !GameData.Main.TryGet<Decor>((int)p.GetValue(null), out Decor decor, false) || decor.ApplicatorAppliance == null)
                {
                    missing.Add(reference);
                    continue;
                }
                var offer = new Offer
                {
                    DecorID = decor.ID, Name = name, Cost = TIER_COST[tier], ReputationPerDay = TIER_REPUTATION[tier],
                    Type = decor.Type, ApplicatorID = decor.ApplicatorAppliance.ID,
                };
                _offers.Add(offer);
                _byID[offer.DecorID] = offer;
            }
            _offers.Sort((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : string.CompareOrdinal(a.Name, b.Name));
            Mod.LogInfo($"Decor shop: {_offers.Count} wallpaper(s) and flooring(s){(missing.Count > 0 ? "; not found: " + string.Join(", ", missing) : "")}.");
        }

        public static Entity CreateItem(EntityManager em, int decorID, Vector3 position)
        {
            if (!TryGetAny(decorID, out Offer offer)) return Entity.Null;
            Entity e = em.CreateEntity();
            em.AddComponentData(e, new CCreateAppliance { ID = offer.ApplicatorID });
            em.AddComponentData(e, new CPosition(position));
            em.AddComponentData(e, new CApplyDecor { ID = offer.DecorID, Type = offer.Type });
            em.AddComponentData(e, new CDrawApplianceUsing { DrawApplianceID = offer.DecorID });
            return e;
        }
    }
}
