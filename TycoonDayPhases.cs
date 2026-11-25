using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>A Tycoon day = Opening + the game's normal service + Closing.</summary>
    public static class TycoonDayPhases
    {
        public static float OpeningSeconds = 0f;
        public static float ClosingSeconds = 0f;

        public static bool IsDay { get; internal set; }
        internal static float OpeningElapsed;
        internal static float ClosingElapsed;
        internal static float ServiceSeconds;
        internal static float TimeOfDay;
        internal static int MessesLeft;
        internal static int TrashLeft;

        public static bool InOpening => IsDay && OpeningElapsed < OpeningSeconds;
        public static bool ClosingDone => ClosingElapsed >= ClosingSeconds;

        internal static float TotalSeconds => OpeningSeconds + ServiceSeconds + ClosingSeconds;

        internal static float BarFraction(float timeOfDay)
        {
            if (TotalSeconds <= 0f) return timeOfDay;
            float done = Mathf.Min(OpeningElapsed, OpeningSeconds) + timeOfDay * ServiceSeconds + Mathf.Min(ClosingElapsed, ClosingSeconds);
            return Mathf.Clamp01(done / TotalSeconds);
        }

        internal static void StartDay()
        {
            IsDay = true;
            OpeningElapsed = 0f;
            ClosingElapsed = 0f;
            MessesLeft = 0;
            TrashLeft = 0;
            DisposablePlates = 0;
        }

        internal static int DisposablePlates;
    }
}
