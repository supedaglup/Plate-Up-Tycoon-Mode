using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>Turns the game's end-of-day page into the Tycoon closing ledger, on every player's screen.</summary>
    [HarmonyPatch(typeof(EndOfDayPopupView), "FirstUpdate")]
    public static class TycoonLedgerViewPatch
    {
        internal static string PendingDescription;

        internal static bool ReputationPage;

        [HarmonyPrefix]
        private static void Prefix(ref EndOfDayPopupView.ViewData view_data)
        {
            PendingDescription = null;
            ReputationPage = false;
            try
            {
                if (view_data.PopupData.PlayerBonus == TycoonReputationReport.PAGE_MARKER)
                {
                    var repIds = new DataObjectList();
                    var repAmounts = new DataObjectList();
                    int sum = 0;
                    foreach ((int id, int amount) in TycoonReputationReport.PageRows()) { repIds.Add(id); repAmounts.Add(amount); sum += amount; }
                    view_data.Identifiers = repIds;
                    view_data.Amounts = repAmounts;
                    view_data.PopupData.Base = sum;
                    view_data.PopupData.PlayerBonus = 0;
                    PendingDescription = TycoonReputationReport.PageDescription();
                    ReputationPage = true;
                    return;
                }
                DataObjectList ids = view_data.Identifiers;
                DataObjectList amounts = view_data.Amounts;
                int count = Mathf.Min(ids.Count, amounts.Count);

                bool tycoon = false;
                for (int i = 0; i < count; i++) if (Mod.Ledger.IsTycoonId(ids[i])) { tycoon = true; break; }
                if (!tycoon) return;

                var income = new List<(int id, int amount)>();
                var costs = new List<(int id, int amount)>();
                int nightsInDebt = 0, reputationGained = 0, reputationTotal = 0, starChange = 0;
                int tips = 0;
                for (int i = 0; i < count; i++)
                {
                    int id = ids[i], amount = amounts[i];
                    if (id == Mod.Ledger.Cash) { }
                    else if (id == Mod.Ledger.Debt) nightsInDebt = amount;
                    else if (id == Mod.Ledger.ReputationGained) reputationGained = amount;
                    else if (id == Mod.Ledger.ReputationTotal) reputationTotal = amount;
                    else if (id == Mod.Ledger.StarChange) starChange = amount;
                    else if (id == Mod.Ledger.CostsHeading) { }
                    else if (id == Mod.Ledger.Tips) tips += amount;
                    else if (Mod.Ledger.IsCostRow(id) || amount < 0) costs.Add((id, amount));
                    else income.Add((id, amount));
                }

                int listed = tips;
                foreach ((int _, int amount) in income) listed += amount;
                foreach ((int _, int amount) in costs) listed += amount;
                int unlisted = view_data.PopupData.Base + view_data.PopupData.PlayerBonus - listed;
                if (unlisted > 0) tips += unlisted;
                if (tips > 0) income.Add((Mod.Ledger.Tips, tips));

                var newIds = new DataObjectList();
                var newAmounts = new DataObjectList();
                foreach ((int id, int amount) in income) { newIds.Add(id); newAmounts.Add(amount); }
                newIds.Add(Mod.Ledger.CostsHeading); newAmounts.Add(0);
                foreach ((int id, int amount) in costs) { newIds.Add(id); newAmounts.Add(amount); }
                view_data.Identifiers = newIds;
                view_data.Amounts = newAmounts;

                PendingDescription = Describe(nightsInDebt, reputationGained, reputationTotal, starChange);
            }
            catch (System.Exception e)
            {
                PendingDescription = null;
                Mod.LogWarning("Tycoon ledger: could not arrange the end-of-day page: " + e.Message);
            }
        }

        private static string Describe(int nightsInDebt, int reputationGained, int reputationTotal, int starChange)
        {
            string text = "Closing time!";
            int stars = TycoonDays.Active ? TycoonEconomy.CurrentStars : TycoonDayBar.Stars;
            if (starChange > 0) text += $"\nNEW STAR! {TycoonReputation.StarsText(stars)}. Two new cards are waiting at the Management Desk.";
            else if (starChange < 0) text += $"\nThe food critic was unimpressed: the restaurant lost a star ({TycoonReputation.StarsText(stars)}).";
            text += $"\nReputation today: {reputationGained / 100f:+0.00;-0.00}. Reputation: {reputationTotal / 100f:0.00}";
            text += stars >= TycoonEconomy.MAX_STARS ? "." : $" (next star at {TycoonReputation.PromoteScore(stars):0}).";
            if (nightsInDebt >= TycoonEconomy.BANKRUPT_AFTER_NIGHTS_IN_DEBT)
                text += "\nBANKRUPT: the restaurant could not pay its bills. Tomorrow the bank seizes it and everything in it.";
            else if (nightsInDebt > 0)
                text += "\nIN DEBT! Get back above 0 coins by tomorrow's closing time or the restaurant goes bankrupt.";
            return text;
        }

        [HarmonyPostfix]
        private static void Postfix(EndOfDayPopupView __instance)
        {
            string description = PendingDescription;
            PendingDescription = null;
            if (description == null) return;
            try
            {
                object list = AccessTools.Field(typeof(EndOfDayPopupView), "ModuleList").GetValue(__instance);
                if (!(AccessTools.Field(typeof(ModuleSet), "Modules").GetValue(list) is IEnumerable modules)) return;
                bool described = false;
                foreach (object o in modules)
                {
                    if (!(o is ModuleInstance instance)) continue;
                    if (!described && instance.Module is TextElement text) { text.SetLabel(description); described = true; if (!ReputationPage) return; continue; }
                    if (described && ReputationPage && instance.Module is LabelElement heading && !(instance.Module is TextElement)) { heading.SetLabel("Reputation"); ReputationPage = false; return; }
                }
                ReputationPage = false;
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Tycoon ledger: could not set the closing summary: " + e.Message);
            }
        }
    }

    /// <summary>No Player Bonus row or money in Tycoon.</summary>
    [HarmonyPatch(typeof(DifficultyHelpers), "MoneyRewardPlayerModifier")]
    public static class NoPlayerBonusPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref float __result)
        {
            if (!TycoonSession.IsActive) return true;
            __result = 1f;
            return false;
        }
    }

    /// <summary>The Reputation Report's total row: "Reputation today" instead of the money total's label.</summary>
    [HarmonyPatch(typeof(EndOfDayPopupView), "AddRow")]
    public static class ReputationReportTotalRowPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string text, bool is_sum_row)
        {
            if (is_sum_row && TycoonLedgerViewPatch.ReputationPage) text = "Reputation today";
            else if (TycoonLedgerViewPatch.ReputationPage && text == "Ambience" && TycoonNetData.Get(TycoonReputation.AMBIENCE_NET_KEY) is string pct && pct.Length > 0)
                text = $"Ambience {pct}%";
            else if (TycoonLedgerViewPatch.ReputationPage && text == "Ingredients" && TycoonNetData.Get(TycoonIngredientQuality.NET_KEY_PERCENT) is string q && q.Length > 0)
                text = $"Ingredients {q}%";
        }
    }

    /// <summary>The Reputation Report's rows are reputation, not coins: no coin picture and no coin sound.</summary>
    [HarmonyPatch(typeof(EndOfDayPopupView), "AddRow")]
    public static class ReputationReportRowPatch
    {
        internal static readonly HashSet<MoneyDisplayRow> Rows = new HashSet<MoneyDisplayRow>();
        private static bool _logged;

        [HarmonyPostfix]
        private static void Postfix(MoneyDisplayRow __result)
        {
            if (!TycoonLedgerViewPatch.ReputationPage || (object)__result == null) return;
            Rows.RemoveWhere(r => (object)r == null || r == null);
            Rows.Add(__result);
            try
            {
                var keep = new List<Transform>();
                foreach (var f in new[] { AccessTools.Field(typeof(MoneyDisplayRow), "Value"), AccessTools.Field(typeof(MoneyDisplayRow), "Divider"),
                                          AccessTools.Field(typeof(LabelElement), "Label"), AccessTools.Field(typeof(LabelElement), "LabelTransform") })
                {
                    object v = f?.GetValue(__result);
                    if (v is Component c && c != null) keep.Add(c.transform);
                    else if (v is GameObject g && g != null) keep.Add(g.transform);
                }
                keep.RemoveAll(k => k == __result.transform);
                var hidden = new List<string>();
                var layout = new System.Text.StringBuilder();
                Hide(__result.transform, __result.transform, keep, hidden, layout, 0);
                if (!_logged)
                {
                    _logged = true;
                    Mod.LogInfo($"Reputation Report: row layout:\n{layout}switched off: {(hidden.Count == 0 ? "NOTHING" : string.Join(", ", hidden))}.");
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Reputation Report: could not hide the coin picture: " + e.Message);
            }
        }

        private static void Hide(Transform t, Transform row, List<Transform> keep, List<string> hidden, System.Text.StringBuilder layout, int depth)
        {
            var types = new List<string>();
            foreach (Component c in t.GetComponents<Component>()) if (c != null && !(c is Transform)) types.Add(c.GetType().Name);
            bool inside = false, holds = false;
            foreach (Transform k in keep)
            {
                if (t.IsChildOf(k)) inside = true;
                else if (k.IsChildOf(t)) holds = true;
            }
            string mark = inside ? "keep" : holds || t == row ? "holds" : "OFF";
            layout.Append(new string(' ', depth * 2)).Append($"{t.name} [{string.Join(", ", types)}] {mark}\n");
            if (inside) return;
            if (!holds && t != row)
            {
                t.gameObject.SetActive(false);
                hidden.Add(t.name);
                return;
            }
            for (int i = 0; i < t.childCount; i++) Hide(t.GetChild(i), row, keep, hidden, layout, depth + 1);
        }
    }

    /// <summary>The Reputation Report's rows count up silently.</summary>
    [HarmonyPatch(typeof(MoneyDisplayRow), "PlaySound")]
    public static class ReputationReportSilentPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MoneyDisplayRow __instance) => !ReputationReportRowPatch.Rows.Contains(__instance);
    }

    /// <summary>The "Daily costs" heading is a row with no number: hide its value and show its divider line.</summary>
    [HarmonyPatch(typeof(EndOfDayPopupView), "AddRow")]
    public static class DeskLedgerRowPatch
    {
        [HarmonyPostfix]
        private static void Postfix(string text, bool is_sum_row, MoneyDisplayRow __result)
        {
            if (is_sum_row || (object)__result == null || Mod.Ledger.CostsHeading == 0) return;
            if (!GameData.Main.TryGet<GameDataObject>(Mod.Ledger.CostsHeading, out GameDataObject heading)) return;
            if (!(heading is KitchenData.Localisations.GenericLocalisation label) || label.Name != text) return;
            try
            {
                if (AccessTools.Field(typeof(MoneyDisplayRow), "Value").GetValue(__result) is Component value)
                    value.gameObject.SetActive(false);
                __result.SetLine(true);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Tycoon ledger: could not style the costs heading: " + e.Message);
            }
        }
    }
}
