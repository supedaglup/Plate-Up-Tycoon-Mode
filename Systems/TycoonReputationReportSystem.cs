using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>The day's Reputation Report, shown at night after the closing ledger.</summary>
    public class TycoonReputationReportSystem : RestaurantSystem, IModSystem
    {
        private const float QUIET_SECONDS = 0.1f;
        private int _publishedDay = -1, _ledgerSeenDay = -1;
        private float _publishedAt;
        private const float LEDGER_WAIT_SECONDS = 5f;
        private const float NET_SECONDS = 2.5f;
        private int _nightDay = -1;
        private float _nightAt;
        private EntityQuery _popups, _recipePopups;
        private int _shownDay = -1, _nextPage = -1;
        private float _quietSince = -1f;
        private float _requestAt = -1f;
        private CPopupEndDayData _pending;

        protected override void Initialise()
        {
            base.Initialise();
            _popups = GetEntityQuery(new QueryHelper().All(typeof(CPopup)));
            _recipePopups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (_requestAt > 0f && UnityEngine.Time.realtimeSinceStartup >= _requestAt)
            {
                _requestAt = -1f;
                PopupUtilities.RequestManagedPopup((PopupType)9, _pending);
                return;
            }
            if (_nextPage >= 0)
            {
                if (!_recipePopups.IsEmpty) return;
                if (_nextPage >= Mod.ReportPages.Count) { _nextPage = -1; return; }
                PopupUtilities.RequestManagedPopup(PopupType.Recipe, new CPopupRecipe { ID = Mod.ReportPages[_nextPage] });
                _nextPage++;
                return;
            }
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (!HasSingleton<SIsNightTime>() || day < 1 || _shownDay == day || TycoonReputationReport.Day != day || !TycoonPreferences.ShowReputationReport) { _quietSince = -1f; return; }
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_nightDay != day) { _nightDay = day; _nightAt = now; }
            if (!_popups.IsEmpty) _ledgerSeenDay = day;
            if (_ledgerSeenDay != day && now - _nightAt < LEDGER_WAIT_SECONDS) return;
            if (_publishedDay != day)
            {
                _publishedDay = day;
                _publishedAt = now;
                if (HasSingleton<STycoonReputation>())
                {
                    STycoonReputation rep = GetSingleton<STycoonReputation>();
                    TycoonReputationReport.Total = rep.GainedToday;
                    TycoonReputationReport.Reputation = rep.Reputation;
                }
                TycoonReputationReport.Stars = TycoonEconomy.CurrentStars;
                TycoonReputationReport.PublishPage();
                Mod.LogInfo("Reputation Report day " + day + ":\n" + TycoonReputationReport.BuildPage(0) + "\n" + TycoonReputationReport.BuildPage(1));
            }
            if (!_popups.IsEmpty || !_recipePopups.IsEmpty || Patches.ManagementCardsView.AnyOpen) { _quietSince = -1f; return; }
            if (now - _publishedAt < NET_SECONDS) return;
            if (_quietSince < 0f) { _quietSince = now; return; }
            if (now - _quietSince < QUIET_SECONDS) return;
            _shownDay = day;
            int total = 0;
            foreach ((int id, int amount) in TycoonReputationReport.Rows()) total += amount;
            _pending = new CPopupEndDayData { Base = total - TycoonReputationReport.PAGE_MARKER, PlayerBonus = TycoonReputationReport.PAGE_MARKER };
            PopupUtilities.RequestManagedPopup((PopupType)9, _pending);
        }
    }
}
