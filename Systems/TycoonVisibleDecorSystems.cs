using Kitchen;
using Kitchen.Layouts;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Furniture and decor give reputation once a day, only where customers can see them.</summary>
    internal static class TycoonVisibleDecor
    {
        internal static float DailyBonus(EntityManager em, TileManager tiles, Bounds bounds, out string details)
        {
            var visible = new HashSet<int>();
            for (int x = Mathf.FloorToInt(bounds.min.x); x <= Mathf.CeilToInt(bounds.max.x); x++)
                for (int z = Mathf.FloorToInt(bounds.min.z); z <= Mathf.CeilToInt(bounds.max.z); z++)
                {
                    CLayoutRoomTile t = tiles.GetTile(new IntVector3(x, 0, z));
                    if (t.Type == RoomType.Dining || t.Type == RoomType.FrontEntrance) visible.Add(t.RoomID);
                }
            AddRoomsWith(em, tiles, typeof(CApplianceTable), visible);
            AddRoomsWith(em, tiles, typeof(CTycoonOrderingCounter), visible);

            float furniture = 0f;
            int pieces = 0;
            using (EntityQuery q = em.CreateEntityQuery(typeof(CAppliance), typeof(CPosition)))
            using (NativeArray<CAppliance> appliances = q.ToComponentDataArray<CAppliance>(Allocator.Temp))
            using (NativeArray<CPosition> positions = q.ToComponentDataArray<CPosition>(Allocator.Temp))
                for (int i = 0; i < appliances.Length; i++)
                {
                    int cost = FurnitureCost(appliances[i].ID);
                    if (cost <= 0 || !Visible(tiles, positions[i].Position, visible)) continue;
                    furniture += cost * TycoonReputation.FURNITURE_PER_COIN_DAY;
                    pieces++;
                }

            float decor = 0f;
            int surfaces = 0;
            using (EntityQuery q = em.CreateEntityQuery(typeof(CChangeDecorEvent)))
            using (NativeArray<CChangeDecorEvent> events = q.ToComponentDataArray<CChangeDecorEvent>(Allocator.Temp))
                for (int i = 0; i < events.Length; i++)
                {
                    if (!visible.Contains(events[i].RoomID)) continue;
                    float r = TycoonDecorShop.ReputationPerDay(events[i].DecorID);
                    if (r <= 0f) continue;
                    decor += r;
                    surfaces++;
                }

            details = $"furniture +{furniture:0.##} ({pieces} piece(s) customers can see), wallpaper/flooring +{decor:0.##} ({surfaces} surface(s)), {visible.Count} visible room(s)";
            return furniture + decor;
        }

        internal static int FurnitureCost(int applianceID)
        {
            if (!TycoonCatalogue.TryGetAny(applianceID, out TycoonCatalogue.Entry entry)) return 0;
            if (entry.Category == TycoonCatalogue.Category.Decorations || IsThemeFurniture(applianceID)) return entry.Cost;
            return 0;
        }

        private static readonly Dictionary<int, bool> ThemeFurniture = new Dictionary<int, bool>();

        internal static bool IsThemeFurniture(int applianceID)
        {
            if (ThemeFurniture.TryGetValue(applianceID, out bool known)) return known;
            bool theme = false;
            if (GameData.Main.TryGet<Appliance>(applianceID, out Appliance a, false) && a.Properties != null)
                foreach (IApplianceProperty p in a.Properties) if (p is CGivesDecoration) { theme = true; break; }
            ThemeFurniture[applianceID] = theme;
            return theme;
        }

        private static bool Visible(TileManager tiles, Vector3 at, HashSet<int> visible)
        {
            CLayoutRoomTile t = tiles.GetTile(new IntVector3(Mathf.RoundToInt(at.x), 0, Mathf.RoundToInt(at.z)));
            return t.Type != RoomType.NoRoom && visible.Contains(t.RoomID);
        }

        private static void AddRoomsWith(EntityManager em, TileManager tiles, System.Type component, HashSet<int> visible)
        {
            using EntityQuery q = em.CreateEntityQuery(component, typeof(CPosition));
            using NativeArray<CPosition> positions = q.ToComponentDataArray<CPosition>(Allocator.Temp);
            foreach (CPosition p in positions)
            {
                CLayoutRoomTile t = tiles.GetTile(new IntVector3(Mathf.RoundToInt(p.Position.x), 0, Mathf.RoundToInt(p.Position.z)));
                if (t.Type != RoomType.NoRoom) visible.Add(t.RoomID);
            }
        }
    }

    /// <summary>Gives theme points back to appliances that older saves had emptied.</summary>
    public class TycoonNoThemePointsSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _emptied;

        protected override void Initialise()
        {
            base.Initialise();
            _emptied = GetEntityQuery(new QueryHelper().All(typeof(CTycoonNoThemePoints), typeof(CAppliance)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (_emptied.IsEmpty) return;
            using NativeArray<Entity> entities = _emptied.ToEntityArray(Allocator.Temp);
            int restored = 0;
            foreach (Entity e in entities)
            {
                int id = EntityManager.GetComponentData<CAppliance>(e).ID;
                if (GameData.Main.TryGet<Appliance>(id, out Appliance a, false) && a.Properties != null && Has<CGivesDecoration>(e))
                    foreach (IApplianceProperty p in a.Properties)
                        if (p is CGivesDecoration gives) { EntityManager.SetComponentData(e, gives); restored++; break; }
                EntityManager.RemoveComponent<CTycoonNoThemePoints>(e);
            }
            Mod.LogInfo($"Theme furniture: theme points given back to {restored} piece(s).");
        }
    }
}
