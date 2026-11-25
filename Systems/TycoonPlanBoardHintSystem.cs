using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>A new restaurant needs a MAIN dish on the recipe pedestal.</summary>
    public class TycoonPlanBoardHintSystem : FranchiseSystem, IModSystem
    {
        private Entity _warned = Entity.Null;

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            Entity paper = Patches.TycoonHq.HeldOn<SDishPedestal>(EntityManager);
            if (paper == Entity.Null || paper == _warned || !Has<CDishChoice>(paper)) return;
            if (!Patches.TycoonHq.SiteOnBoard(EntityManager, out _)) return;
            int dishID = EntityManager.GetComponentData<CDishChoice>(paper).Dish;
            if (!GameData.Main.TryGet<Dish>(dishID, out Dish dish) || TycoonRecipeShop.IsMainDish(dish)) return;
            _warned = paper;
            Vector3 at = Require(paper, out CPosition p) ? p.Position : Vector3.zero;
            string name = dish.Name.Length > 18 ? dish.Name.Substring(0, 18) : dish.Name;
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, at, $"{name} is an add-on: put a main dish here", 6f);
            Mod.LogInfo($"HQ: {dish.Name} ({dish.Type}) is on the recipe pedestal; a new restaurant needs a main dish.");
        }
    }
}
