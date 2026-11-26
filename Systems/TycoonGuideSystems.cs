using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Shows a guide section's pages one after another with the game's recipe pop-up, each once the last is gone.</summary>
    internal class TycoonGuidePager
    {
        private readonly Queue<int> _pages = new Queue<int>();
        public bool Busy => _pages.Count > 0;

        public void Start(int section, string where)
        {
            TycoonGuide.MarkSeen(section);
            foreach (int id in TycoonGuide.PageIDs(section)) _pages.Enqueue(id);
            Mod.LogInfo($"Guide: showing '{TycoonGuide.Title(section)}' ({_pages.Count} page(s)) {where}.");
        }

        public bool Step(EntityQuery recipePopups, System.Action<int> show)
        {
            if (_pages.Count == 0) return false;
            if (!recipePopups.IsEmpty) return true;
            show(_pages.Dequeue());
            return true;
        }

        public void Clear() => _pages.Clear();
    }

    /// <summary>The guide's HQ section: the first time a Tycoon save is in HQ with a player in it.</summary>
    public class TycoonGuideHqSystem : FranchiseSystem, IModSystem
    {
        private const float SETTLE_SECONDS = 3f;
        private EntityQuery _players, _recipePopups;
        private readonly TycoonGuidePager _pager = new TycoonGuidePager();
        private float _arrived = -1f;

        protected override void Initialise()
        {
            base.Initialise();
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer)));
            _recipePopups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || !TycoonGuide.Enabled) { _pager.Clear(); return; }
            if (_pager.Step(_recipePopups, id => PopupUtilities.RequestManagedPopup(PopupType.Recipe, new CPopupRecipe { ID = id }))) return;
            if (_players.IsEmpty || TycoonGuide.Seen(TycoonGuide.HQ) || TycoonGuide.PageCount(TycoonGuide.HQ) == 0) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_arrived < 0f || !_recipePopups.IsEmpty) { _arrived = now; return; }
            if (now - _arrived < SETTLE_SECONDS) return;
            _pager.Start(TycoonGuide.HQ, "on the first arrival in HQ");
        }
    }

    /// <summary>The guide's star sections: at night in a restaurant, once nothing else is on screen for a moment.</summary>
    public class TycoonGuideStarSystem : RestaurantSystem, IModSystem
    {
        private const float QUIET_SECONDS = 2f;
        private EntityQuery _recipePopups, _popups, _cards;
        private readonly TycoonGuidePager _pager = new TycoonGuidePager();
        private float _quietSince = -1f;

        protected override void Initialise()
        {
            base.Initialise();
            _recipePopups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
            _popups = GetEntityQuery(new QueryHelper().All(typeof(CPopup)));
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (!TycoonGuide.Enabled) { _pager.Clear(); return; }
            if (_pager.Step(_recipePopups, id => PopupUtilities.RequestManagedPopup(PopupType.Recipe, new CPopupRecipe { ID = id }))) return;

            if (!HasSingleton<SIsNightTime>() || !_popups.IsEmpty || !_recipePopups.IsEmpty || Patches.ManagementCardsView.AnyOpen)
            {
                _quietSince = -1f;
                return;
            }
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_quietSince < 0f) { _quietSince = now; return; }
            if (now - _quietSince < QUIET_SECONDS) return;

            int stars = UnityEngine.Mathf.Clamp(TycoonEconomy.CurrentStars, 0, 5);
            for (int s = 0; s <= stars; s++)
            {
                int section = TycoonGuide.StarSection(s);
                if (TycoonGuide.Seen(section) || TycoonGuide.PageCount(section) == 0) continue;
                _pager.Start(section, $"at {s} star(s)");
                _quietSince = -1f;
                return;
            }

            if (!TycoonGuide.Seen(TycoonGuide.MASTERY) && TycoonGuide.PageCount(TycoonGuide.MASTERY) > 0 && HasMasteryUnlock())
            {
                _pager.Start(TycoonGuide.MASTERY, "after the first Recipe Mastery level");
                _quietSince = -1f;
            }
        }

        private bool HasMasteryUnlock()
        {
            using NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
            foreach (CTycoonCard c in cards) if (c.Star == TycoonRecipeMastery.CARD_STAR) return true;
            return false;
        }
    }
}
