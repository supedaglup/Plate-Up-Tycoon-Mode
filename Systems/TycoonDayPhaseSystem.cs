using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Runs the Opening and Closing parts of the day.</summary>
    public class TycoonDayPhaseSystem : RestaurantSystem, IModSystem
    {
        private static readonly Vector3 CARRIER_POSITION = new Vector3(0f, -50f, 0f);

        private EntityQuery _messes;
        private EntityQuery _bins;
        private EntityQuery _carriers;

        protected override void Initialise()
        {
            base.Initialise();
            _messes = GetEntityQuery(new QueryHelper().All(typeof(CMess)));
            _bins = GetEntityQuery(new QueryHelper().All(typeof(CApplianceBin)).None(typeof(CApplianceExternalBin)));
            _carriers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonPhaseCarrier)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STime>();
        }

        protected override void OnUpdate()
        {
            if (HasSingleton<SIsDayTime>() && !HasSingleton<SPracticeMode>()) UpdateDay();
            else TycoonDayPhases.IsDay = false;
            UpdateCarrier();
        }

        private void UpdateDay()
        {
            if (!TycoonDayPhases.IsDay) TycoonDayPhases.StartDay();

            bool wasOpening = TycoonDayPhases.InOpening;
            STime time = GetSingleton<STime>();
            float dt = Time.DeltaTime;

            if (wasOpening) TycoonDayPhases.OpeningElapsed += dt;
            bool pause = TycoonDayPhases.InOpening;
            if (time.ForcePause != pause)
            {
                time.ForcePause = pause;
                Set(time);
            }

            TycoonDayPhases.ServiceSeconds = time.DayLength;
            TycoonDayPhases.TimeOfDay = time.TimeOfDay;

            if (time.TimeOfDayUnbounded >= 1f)
            {
                TycoonDayPhases.ClosingElapsed += dt;
                TycoonMesses.Count(EntityManager, _messes, out int dirty, out _);
                TycoonDayPhases.MessesLeft = dirty;
                TycoonDayPhases.TrashLeft = CountTrash();
            }
        }

        private void UpdateCarrier()
        {
            Entity carrier;
            if (_carriers.IsEmpty)
            {
                carrier = EntityManager.CreateEntity(typeof(CTycoonPhaseCarrier), typeof(CRequiresView), typeof(CPosition), typeof(CProgressIndicator), typeof(CDoNotPersist));
                EntityManager.SetComponentData(carrier, new CRequiresView { Type = ViewType.ProgressView });
                EntityManager.SetComponentData(carrier, new CPosition(CARRIER_POSITION));
            }
            else carrier = _carriers.GetSingletonEntity();

            float total = TycoonDayPhases.TotalSeconds;
            float opening = total > 0f ? TycoonDayPhases.OpeningSeconds / total : 0f;
            float closing = total > 0f ? TycoonDayPhases.ClosingSeconds / total : 0f;
            EntityManager.SetComponentData(carrier, new CProgressIndicator
            {
                Progress = TycoonDayPhases.IsDay ? TycoonDayPhases.BarFraction(TycoonDayPhases.TimeOfDay) : 1f,
                Process = Patches.TycoonDayBar.Encode(TycoonDayPhases.IsDay, TycoonEconomy.CurrentStars, opening, closing),
            });
        }

        private int CountTrash()
        {
            int trash = 0;
            using NativeArray<CApplianceBin> bins = _bins.ToComponentDataArray<CApplianceBin>(Allocator.Temp);
            for (int i = 0; i < bins.Length; i++) trash += bins[i].CurrentAmount;
            return trash;
        }
    }
}
