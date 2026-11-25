using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>HQ in Tycoon Mode: puts the Tycoon restaurant on the drawing board, then hides and locks the board.</summary>
    public class TycoonFranchiseSetupSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery _tycoonMaps;
        private Entity _openBoard = Entity.Null;
        private Entity _waitingFor = Entity.Null;
        private float _waitUntil;

        protected override void Initialise()
        {
            base.Initialise();
            _tycoonMaps = GetEntityQuery(new QueryHelper().All(typeof(CTycoonLayoutMap)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive)
                return;
            if (!_tycoonMaps.IsEmpty)
                return;
            if (!HasSingleton<SSelectedLayoutPedestal>())
                return;
            Entity board = GetSingletonEntity<SSelectedLayoutPedestal>();
            if (!Require(board, out CItemHolder holder))
                return;

            if (board == _openBoard) return;
            if (board != _waitingFor) { _waitingFor = board; _waitUntil = Time.RealTotalTime + 8f; }
            bool anySaved = TycoonSites.SavedRestaurants() > 0;
            if (!anySaved && Time.RealTotalTime < _waitUntil) return;
            if (anySaved)
            {
                _openBoard = board;
                Mod.LogInfo("HQ: the drawing board is open for new restaurants (a bought site + a main recipe on the recipe pedestal).");
                return;
            }

            if (!Has<CHideView>(board)) EntityManager.AddComponent<CHideView>(board);
            if (!Has<CPreventItemTransfer>(board)) EntityManager.AddComponent<CPreventItemTransfer>(board);

            if (holder.HeldItem != Entity.Null)
                return;

            int settingID = TycoonKitchenDesign.CitySetting;
            Entity map = Entity.Null;
            string source;

            try
            {
                if (!Mod.USE_STARTER_DESIGN) throw new System.InvalidOperationException("starter design switched off for now (Mod.USE_STARTER_DESIGN)");
                TycoonKitchenDesign.Design design = TycoonKitchenDesign.Decode(TycoonKitchenDesign.STARTER_DESIGN, settingID);
                Seed fixedSeed = Seed.Generate(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                map = TycoonKitchenDesign.CreateMap(EntityManager, design, fixedSeed);
                source = $"Tycoon starter design ({design.Blueprint.Tiles.Count} tiles, setting {design.SettingID})";
            }
            catch (System.Exception e)
            {
                if (Mod.USE_STARTER_DESIGN) Mod.LogWarning("HQ: could not build the Tycoon starter design, using a normal small layout instead: " + e);
                LayoutRecipeDump.LogOnce(Mod.STARTING_LAYOUT_PROFILE);
                var seed = new LayoutSeed(UnityEngine.Random.Range(int.MinValue, int.MaxValue), new[] { Mod.STARTING_LAYOUT_PROFILE });
                map = seed.GenerateMap(EntityManager, settingID);
                source = $"layout profile {seed.LayoutID}, seed {seed.FixedSeed}";
            }

            EntityManager.AddComponentData(map, new CSettingDish { DishID = ResolveStartingDish() });
            EntityManager.AddComponentData(map, new CTycoonLayoutMap());
            EntityManager.SetComponentData(map, new CHeldBy { Holder = board });
            if (Has<CHome>(map)) EntityManager.SetComponentData(map, new CHome { Holder = board });
            else EntityManager.AddComponentData(map, new CHome { Holder = board });
            EntityManager.SetComponentData(board, new CItemHolder { HeldItem = map });

            Mod.LogInfo($"HQ: Tycoon layout placed on the drawing board ({source}).");
        }

        private static int ResolveStartingDish()
        {
            if (GameData.Main.TryGet<Dish>(Mod.STARTING_DISH, out Dish dish) && dish.Type == DishType.Base)
                return dish.ID;

            Mod.LogError($"Dish {Mod.STARTING_DISH} is not a base dish. Base dish IDs in this game version:");
            foreach (Dish d in GameData.Main.Get<Dish>())
            {
                if (d.Type == DishType.Base)
                    Mod.LogInfo($"  base dish ID {d.ID}");
            }
            return 0;
        }
    }

    /// <summary>Writes the steps of a layout profile's room "recipe" to the log, once per game launch.</summary>
    internal static class LayoutRecipeDump
    {
        private static bool _done;

        internal static void LogOnce(int profileID)
        {
            if (_done) return;
            _done = true;
            try
            {
                if (!GameData.Main.TryGet<LayoutProfile>(profileID, out LayoutProfile profile)) { Mod.LogWarning($"Layout {profileID} not found."); return; }
                object graph = typeof(LayoutProfile).GetField("Graph")?.GetValue(profile);
                if (graph == null) { Mod.LogWarning("Layout has no graph."); return; }
                var sb = new System.Text.StringBuilder($"Layout recipe for profile {profileID}:\n");
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                System.Reflection.FieldInfo nodesField = null;
                for (System.Type t = graph.GetType(); t != null && nodesField == null; t = t.BaseType) nodesField = t.GetField("nodes", flags);
                if (!(nodesField?.GetValue(graph) is System.Collections.IEnumerable nodes)) { Mod.LogWarning("Layout graph has no node list."); return; }
                foreach (object node in nodes)
                {
                    if (node == null) continue;
                    sb.Append("  - ").Append(node.GetType().Name).Append(':');
                    foreach (System.Reflection.FieldInfo f in node.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (f.DeclaringType != null && f.DeclaringType.Namespace == "XNode") continue;
                        object v = f.GetValue(node);
                        sb.Append(' ').Append(f.Name).Append('=').Append(v is UnityEngine.Object o ? o.name : v?.ToString() ?? "null");
                    }
                    sb.Append('\n');
                }
                Mod.LogInfo(sb.ToString());
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Could not log the layout recipe: " + e.Message);
            }
        }
    }
}
