using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>A recipe whose "how to cook it" popup this restaurant has already shown.</summary>
    public struct CTycoonRecipeSeen : IComponentData, IModComponent
    {
        public int DishID;
    }

    /// <summary>Singleton: the recipes already on the menu were marked as seen once.</summary>
    public struct STycoonRecipesSeenInit : IComponentData, IModComponent
    {
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>The "how to cook it" recipe popup for a recipe card switched on at the Management Desk.</summary>
    public class TycoonRecipePopupSystem : RestaurantSystem, IModSystem
    {
        internal static readonly HashSet<int> Pending = new HashSet<int>();
        private const float AFTER_CLOSE = 0.5f;

        private EntityQuery _popups;
        private EntityQuery _seen;
        private EntityQuery _appliedCards;
        private float _closedAt = -100f;

        protected override void Initialise()
        {
            base.Initialise();
            _popups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
            _seen = GetEntityQuery(new QueryHelper().All(typeof(CTycoonRecipeSeen)));
            _appliedCards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard), typeof(CTycoonCardApplied)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<STycoonRecipesSeenInit>()) MarkMenuSeen();
            if (Pending.Count == 0) return;
            if (Patches.ManagementCardsView.AnyOpen) { _closedAt = Time.RealTotalTime; return; }
            if (Time.RealTotalTime - _closedAt < AFTER_CLOSE || !_popups.IsEmpty) return;

            HashSet<int> seen = Seen();
            int next = 0;
            foreach (int id in Pending) { next = id; break; }
            Pending.Remove(next);
            if (seen.Contains(next)) return;
            if (!TycoonCards.IsEnabled(EntityManager, next)) return;
            MarkSeen(next);
            if (Preferences.Get<bool>(Pref.SkipNewRecipePopups)) return;
            PopupUtilities.RequestManagedPopup(PopupType.Recipe, new CPopupRecipe { ID = next });
            Mod.LogInfo($"Recipe popup: {TycoonRecipeShop.NameOf(next)} (shown once for this restaurant).");
        }

        private HashSet<int> Seen()
        {
            var set = new HashSet<int>();
            using NativeArray<CTycoonRecipeSeen> all = _seen.ToComponentDataArray<CTycoonRecipeSeen>(Allocator.Temp);
            for (int i = 0; i < all.Length; i++) set.Add(all[i].DishID);
            return set;
        }

        private void MarkSeen(int dishID)
        {
            Entity e = EntityManager.CreateEntity(typeof(CTycoonRecipeSeen));
            EntityManager.SetComponentData(e, new CTycoonRecipeSeen { DishID = dishID });
        }

        private void MarkMenuSeen()
        {
            HashSet<int> seen = Seen();
            int marked = 0;
            using (NativeArray<CTycoonCard> cards = _appliedCards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                for (int i = 0; i < cards.Length; i++)
                    if (TycoonCards.IsRecipe(cards[i].UnlockID) && seen.Add(cards[i].UnlockID)) { MarkSeen(cards[i].UnlockID); marked++; }
            Set(new STycoonRecipesSeenInit());
            Pending.Clear();
            Mod.LogInfo($"Recipe popups: {marked} recipe(s) already on the menu count as seen.");
        }
    }
}
