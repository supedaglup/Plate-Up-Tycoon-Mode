using Kitchen;
using Kitchen.Layouts;
using Kitchen.Layouts.Features;
using KitchenData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>The Tycoon starter restaurant, built from a Kitchen Designer design code.</summary>
    public static class TycoonKitchenDesign
    {
        public const string STARTER_DESIGN =
            "2MjIyMzcwNDYxOjIwMDI4NzYyOTU6MywxOzEsMzsyLDI6Nyw2OjIuMywzLjMsMi4zLDMuMywyLjMsMS4zLDIuMywxLjMsMi4zLDEuMywyLjMsMS4zLDIuMywxLjM6MiwyLDIsMywyOzMsMiwzLDMsMjswLC0xLDAsMCwzOzYsMiw2LDMsMTsxLDUsMiw1LDQ6";

        public static int CitySetting => KitchenLib.References.RestaurantSettingReferences.City;

        public class Design
        {
            public LayoutBlueprint Blueprint;
            public LayoutProfile Profile;
            public int SettingID;
        }

        public static Design Decode(string encoded, int fallbackSettingID)
        {
            if (string.IsNullOrEmpty(encoded) || encoded[0] != '2')
                throw new FormatException("Only version-2 Kitchen Designer codes are supported.");

            string body = encoded.Substring(1).Replace('-', '+').Replace('_', '/');
            body = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
            string[] sections = Encoding.UTF8.GetString(Convert.FromBase64String(body)).Split(':');

            var design = new Design();
            int profileID = int.Parse(sections[0]);
            if (!GameData.Main.TryGet<LayoutProfile>(profileID, out design.Profile))
                throw new InvalidOperationException($"Layout profile {profileID} not found.");

            int settingID = int.Parse(sections[1]);
            if (settingID == 0) settingID = CitySetting;
            design.SettingID = GameData.Main.TryGet<RestaurantSetting>(settingID, out _) ? settingID : fallbackSettingID;

            var layout = new LayoutBlueprint();

            List<Room> rooms = sections[2].Split(';').Select(x =>
            {
                string[] split = x.Split(',');
                return new Room(int.Parse(split[0]), (RoomType)int.Parse(split[1]));
            }).ToList();

            string[] size = sections[3].Split(',');
            int sizeY = int.Parse(size[1]);

            int counter = 0;
            foreach (string section in sections[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string roomSection = section.Contains(".") ? section : section + ".1";
                int[] parts = roomSection.Split('.').Select(int.Parse).ToArray();
                int roomID = parts[0], repeat = parts[1];
                Room room = rooms.SingleOrDefault(r => r.ID == roomID);
                for (int j = 0; j < repeat; j++, counter++)
                {
                    if (roomID != 0) layout.Tiles.Add(new LayoutPosition(counter / sizeY, counter % sizeY), room);
                }
            }

            layout.Features = new List<Feature>();
            foreach (string featureString in sections[5].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = featureString.Split(',');
                var from = new LayoutPosition(int.Parse(f[0]), int.Parse(f[1]));
                var to = new LayoutPosition(int.Parse(f[2]), int.Parse(f[3]));
                var type = (FeatureType)int.Parse(f[4]);
                layout.Features.Add(type == FeatureType.FrontDoor && from.y < to.y ? new Feature(to, from, type) : new Feature(from, to, type));
            }

            CentreLayout(layout);
            design.Blueprint = layout;
            return design;
        }

        public static Entity CreateMap(EntityManager em, Design design, Seed seed)
        {
            Entity layout = em.CreateEntity(typeof(CStartingItem), typeof(CLayoutRoomTile), typeof(CLayoutFeature), typeof(CLayoutAppliancePlacement));
            design.Blueprint.ToEntity(em, layout);

            List<CLayoutAppliancePlacement> backdrop = DecorateBackdrop(design);
            EnsureDumpster(design, backdrop);
            DynamicBuffer<CLayoutAppliancePlacement> placements = em.GetBuffer<CLayoutAppliancePlacement>(layout);
            foreach (CLayoutAppliancePlacement piece in backdrop) placements.Add(piece);

            Entity map = em.CreateEntity(typeof(CCreateItem), typeof(CHeldBy));
            em.SetComponentData(map, new CCreateItem { ID = AssetReference.MapItem });
            em.AddComponentData(map, new CItemLayoutMap { Layout = layout });
            em.AddComponentData(map, new CSetting { RestaurantSetting = design.SettingID, FixedSeed = seed });
            return map;
        }

        private static void EnsureDumpster(Design design, List<CLayoutAppliancePlacement> backdrop)
        {
            Appliance dumpster = design.Profile.ExternalBin;
            if (dumpster == null && GameData.Main.TryGet<LayoutProfile>(Mod.STARTING_LAYOUT_PROFILE, out LayoutProfile basic))
                dumpster = basic.ExternalBin;
            if (dumpster == null)
            {
                Mod.LogWarning("Starter design: no layout profile has a dumpster.");
                return;
            }
            if (backdrop.Any(p => p.Appliance == dumpster.ID))
            {
                Mod.LogInfo("Starter design: dumpster placed by the backdrop.");
                return;
            }
            Feature door = design.Blueprint.Features.First(f => f.Type == FeatureType.FrontDoor);
            int outside = Mathf.Min(door.Tile1.y, door.Tile2.y);
            backdrop.Add(new CLayoutAppliancePlacement { Appliance = dumpster.ID, Position = new Vector3(door.Tile1.x, 0f, outside - 2) });
            Mod.LogInfo($"Starter design: dumpster added beside the street at ({door.Tile1.x}, {outside - 2}).");
        }

        private static List<CLayoutAppliancePlacement> DecorateBackdrop(Design design)
        {
            var decorations = new List<CLayoutAppliancePlacement>();
            if (!GameData.Main.TryGet<RestaurantSetting>(design.SettingID, out RestaurantSetting setting) || setting.Decorators == null)
            {
                Mod.LogWarning($"Starter design: setting {design.SettingID} has no backdrop decorators.");
                return decorations;
            }
            foreach (IDecorationConfiguration config in setting.Decorators)
            {
                if (config == null) continue;
                try
                {
                    if (!(config.Decorator is Decorator decorator)) continue;
                    decorator.Setup(design.Blueprint, design.Profile, config, decorations);
                    decorator.Decorate(new Room());
                }
                catch (Exception e)
                {
                    Mod.LogWarning($"Starter design: backdrop decorator {config.Decorator?.GetType().Name} failed: {e.Message}");
                }
            }
            Mod.LogInfo($"Starter design: {decorations.Count} backdrop piece(s) from setting {design.SettingID}.");
            return decorations;
        }

        private static void CentreLayout(LayoutBlueprint blueprint)
        {
            if (blueprint.Tiles.Count == 0) return;
            Dictionary<LayoutPosition, Room> tiles = blueprint.Tiles;
            var min = new Vector2(tiles.Min(r => r.Key.x), tiles.Min(r => r.Key.y));
            Vector2 newMin = -(new Vector2(tiles.Max(r => r.Key.x), tiles.Max(r => r.Key.y)) - min) / 2f;
            newMin = new Vector2(Mathf.FloorToInt(newMin.x), Mathf.FloorToInt(newMin.y));
            LayoutPosition Translate(Vector2 input) => (LayoutPosition)(input - min + newMin);
            blueprint.Tiles = tiles.ToDictionary(r => Translate((Vector2)r.Key), r => r.Value);
            blueprint.Features = blueprint.Features.Select(f => new Feature(Translate((Vector2)f.Tile1), Translate((Vector2)f.Tile2), f.Type)).ToList();
        }
    }
}
