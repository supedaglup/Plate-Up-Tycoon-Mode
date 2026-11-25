using HarmonyLib;
using Kitchen;
using KitchenData;
using PlateUpTycoon.Components;
using Unity.Entities;
using Mathf = UnityEngine.Mathf;

namespace PlateUpTycoon.Patches
{
    /// <summary>Static days in Tycoon Mode.</summary>
    internal static class TycoonDays
    {
        internal static bool Active => TycoonSession.IsActive && GameInfo.CurrentScene == SceneType.Kitchen;

        internal static bool ViewActive => Active || TycoonDayBar.Seen;

        internal static int EffectiveDay => TycoonEconomy.EffectiveDay(ForcedStars >= 0 ? ForcedStars : TycoonEconomy.CurrentStars);

        internal static int ForcedStars = -1;
    }

    /// <summary>No card choices.</summary>
    [HarmonyPatch(typeof(FindBonusUnlocks), "AddOption")]
    public static class NoCardOptionsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonDays.Active;
    }

    [HarmonyPatch(typeof(CreateUnlockChoicePopup), "OnUpdate")]
    public static class NoCardPopupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonDays.Active;
    }

    /// <summary>No "new star" popup every 3 days.</summary>
    [HarmonyPatch(typeof(CreateStarIncreasePopup), "OnUpdate")]
    public static class NoStarPopupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonDays.Active;
    }

    /// <summary>No "franchise created" popup on day 15.</summary>
    [HarmonyPatch(typeof(CreateFranchiseCreatedPopup), "OnUpdate")]
    public static class NoFranchisePopupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonDays.Active;
    }

    /// <summary>No customer group growth on overtime days.</summary>
    [HarmonyPatch(typeof(AutoGrowGroupSizes), "OnUpdate")]
    public static class NoOvertimeGroupGrowthPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !TycoonDays.Active;
    }

    /// <summary>No "days until the next star" bar at the top of the screen: Tycoon has no card days to count down to.</summary>
    [HarmonyPatch(typeof(TimeDisplayView), "SetTicks")]
    public static class NoDayTicksPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(TimeDisplayView __instance)
        {
            if (!TycoonDays.ViewActive) return true;
            SetTextField(__instance, "Ticks", "");
            return false;
        }

        internal static void SetTextField(object view, string field, string text)
        {
            object label = AccessTools.Field(view.GetType(), field)?.GetValue(view);
            label?.GetType().GetProperty("text")?.SetValue(label, text);
        }
    }

    [HarmonyPatch(typeof(TimeDisplayView), "SetupLine")]
    public static class NoDayLinePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(TimeDisplayView __instance)
        {
            if (!TycoonDays.ViewActive) return true;
            NoDayTicksPatch.SetTextField(__instance, "LineBefore", "");
            NoDayTicksPatch.SetTextField(__instance, "LineAfter", "");
            NoDayTicksPatch.SetTextField(__instance, "Icons", "");
            return false;
        }
    }

    /// <summary>Day length follows the star rating, not the day number.</summary>
    [HarmonyPatch(typeof(ProgressionHelpers), "GetDayLength")]
    public static class StaticDayLengthPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref int day)
        {
            if (TycoonDays.Active) day = TycoonDays.EffectiveDay;
        }
    }

    /// <summary>Customer numbers follow the star rating, not the day number.</summary>
    [HarmonyPatch(typeof(CreateCustomerSchedule), "GetCustomersForDay")]
    public static class StaticCustomersPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref ScheduleParameters schedule_parameters)
        {
            if (TycoonDays.Active) schedule_parameters.Day = TycoonDays.EffectiveDay;
        }
    }

    [HarmonyPatch(typeof(CreateCustomerSchedule), "DetermineTotalCustomers")]
    public static class StaticCustomerTotalPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref ScheduleParameters schedule_parameters)
        {
            if (TycoonDays.Active) schedule_parameters.Day = TycoonDays.EffectiveDay;
        }

        [HarmonyPostfix]
        private static void Postfix(CreateCustomerSchedule __instance, ScheduleParameters schedule_parameters, ref float __result)
        {
            if (!TycoonDays.Active || TycoonDays.ForcedStars >= 0) return;
            try
            {
                int stars = TycoonEconomy.CurrentStars;
                float score = TycoonReputation.Current.Reputation;
                float current = __result, target, share;
                float above = score - TycoonReputation.TotalFor(stars);
                if (above >= 0f)
                {
                    if (stars >= TycoonEconomy.MAX_STARS) return;
                    share = TycoonReputation.Progress(score, stars);
                    target = TotalAt(__instance, schedule_parameters, stars + 1);
                }
                else if (stars > TycoonEconomy.MIN_STARS)
                {
                    share = Mathf.Clamp01(-above / (TycoonReputation.TotalFor(stars) - TycoonReputation.TotalFor(stars - 1)));
                    target = TotalAt(__instance, schedule_parameters, stars - 1);
                }
                else
                {
                    share = Mathf.Clamp01(above / (TycoonReputation.DemoteScore(stars) - TycoonReputation.TotalFor(stars)));
                    float next = TotalAt(__instance, schedule_parameters, stars + 1);
                    target = current > 0f ? current * current / next : current;
                }
                if (share <= 0f) return;
                __result = Mathf.Lerp(current, target, share);
                Mod.LogInfo($"Customers: reputation {score:0.00} moves today's total from {current:0.#} to {__result:0.#} ({share:P0} of the way to {target:0.#}).");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Customers: could not apply reputation: " + e.Message);
            }
            finally
            {
                TycoonDays.ForcedStars = -1;
            }
        }

        private static float TotalAt(CreateCustomerSchedule system, ScheduleParameters parameters, int stars)
        {
            TycoonDays.ForcedStars = TycoonEconomy.ClampStars(stars);
            try { return system.DetermineTotalCustomers(parameters); }
            finally { TycoonDays.ForcedStars = -1; }
        }
    }

    /// <summary>Count customers who run out of patience.</summary>
    [HarmonyPatch(typeof(HandleLifeLoseEvent), "OnUpdate")]
    public static class CountWalkoutsPatch
    {
        [HarmonyPrefix]
        private static void Prefix(HandleLifeLoseEvent __instance)
        {
            if (!TycoonSession.IsActive) return;
            if (!(AccessTools.Field(typeof(HandleLifeLoseEvent), "Query").GetValue(__instance) is EntityQuery query)) return;
            int count = query.CalculateEntityCount();
            if (count == 0) return;

            EntityManager em = __instance.EntityManager;
            using EntityQuery economyQuery = em.CreateEntityQuery(typeof(STycoonEconomy));
            if (economyQuery.IsEmpty) return;
            Entity e = economyQuery.GetSingletonEntity();
            STycoonEconomy economy = em.GetComponentData<STycoonEconomy>(e);
            economy.WalkoutsToday += count;
            economy.WalkoutsTotal += count;
            em.SetComponentData(e, economy);

            using EntityQuery repQuery = em.CreateEntityQuery(typeof(STycoonReputation));
            if (!repQuery.IsEmpty)
            {
                Entity r = repQuery.GetSingletonEntity();
                STycoonReputation rep = em.GetComponentData<STycoonReputation>(r);
                float loss = TycoonReputation.WALKOUT_PENALTY * count;
                TycoonReputation.Add(ref rep, -loss);
                TycoonReputationReport.Walkout(count, -loss);
                em.SetComponentData(r, rep);
                TycoonReputation.Current = rep;
            }
            Mod.LogInfo($"Tycoon: {count} customer group(s) ran out of patience and left (no life lost; -{TycoonReputation.WALKOUT_PENALTY * count:0.#} reputation).");
        }
    }

    /// <summary>Top-of-screen day label: "Day N" and the star rating in place of the franchise tier.</summary>
    [HarmonyPatch(typeof(DayDisplayView), "UpdateData")]
    public static class DayLabelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DayDisplayView __instance, DayDisplayView.ViewData view_data)
        {
            if (!TycoonDays.ViewActive || view_data.IsPractice) return;
            try
            {
                int day = view_data.IsNight ? view_data.Day + 1 : view_data.Day;
                SetText(__instance, "Day", $"Day {day}");
                int stars = TycoonDays.Active ? TycoonEconomy.CurrentStars : TycoonReputationBar.Seen ? TycoonReputationBar.Stars : TycoonDayBar.Stars;
                SetText(__instance, "Tier", TycoonReputation.StarsText(stars));
                TycoonReputationBar.Remember(__instance);
            }
            catch (System.Exception e)
            {
                if (!_logged) { _logged = true; Mod.LogWarning("Tycoon: could not set the day label: " + e.Message); }
            }
        }

        private static bool _logged;

        private static void SetText(DayDisplayView view, string field, string text)
        {
            object label = AccessTools.Field(typeof(DayDisplayView), field).GetValue(view);
            label?.GetType().GetProperty("text")?.SetValue(label, text);
        }
    }
}
