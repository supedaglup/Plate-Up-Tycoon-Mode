using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Singleton: this restaurant's Opening and Closing lengths in seconds.</summary>
    public struct STycoonBusinessHours : IComponentData, IModComponent
    {
        public int Opening;
        public int Closing;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Business Hours: applies the chosen Opening and Closing lengths outside the day.</summary>
    public class TycoonBusinessHoursSystem : RestaurantSystem, IModSystem
    {
        public const float DEFAULT_OPENING = 0f, DEFAULT_CLOSING = 0f;
        public static readonly int[] CHOICES = { 0, 3, 6, 9, 12, 15 };

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (TycoonDayPhases.IsDay) return;
            float opening = DEFAULT_OPENING, closing = DEFAULT_CLOSING;
            if (HasSingleton<STycoonBusinessHours>())
            {
                STycoonBusinessHours hours = GetSingleton<STycoonBusinessHours>();
                opening = hours.Opening;
                closing = hours.Closing;
            }
            if (TycoonDayPhases.OpeningSeconds != opening || TycoonDayPhases.ClosingSeconds != closing)
            {
                TycoonDayPhases.OpeningSeconds = opening;
                TycoonDayPhases.ClosingSeconds = closing;
                Mod.LogInfo($"Business hours: Opening {opening:0} s, Closing {closing:0} s.");
            }
        }

        public static int Encode(bool opening, int seconds) => -77000 - (opening ? 0 : 100) - seconds;

        public static bool Decode(int code, out bool opening, out int seconds)
        {
            opening = false; seconds = 0;
            if (code > -77000 || code <= -77200) return false;
            int rest = -77000 - code;
            opening = rest < 100;
            seconds = rest % 100;
            return true;
        }
    }
}
