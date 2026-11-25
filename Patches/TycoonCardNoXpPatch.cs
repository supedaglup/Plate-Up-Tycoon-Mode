using HarmonyLib;
using Kitchen;
using KitchenData;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>Hides the XP reward on cards in Tycoon.</summary>
    [HarmonyPatch(typeof(UnlockCardElement), nameof(UnlockCardElement.SetUnlock), new[] { typeof(ICard) })]
    public static class TycoonCardNoXpPatch
    {
        private static readonly System.Reflection.FieldInfo Container = AccessTools.Field(typeof(UnlockCardElement), "RewardContainer");
        private static readonly System.Reflection.FieldInfo Reward = AccessTools.Field(typeof(UnlockCardElement), "Reward");
        private static readonly System.Reflection.FieldInfo ChangeText = AccessTools.Field(typeof(UnlockCardElement), "CustomerChange");
        private static readonly System.Reflection.FieldInfo ChangeContainer = AccessTools.Field(typeof(UnlockCardElement), "CustomerChangeContainer");

        [HarmonyPostfix]
        private static void Postfix(UnlockCardElement __instance, ICard unlock)
        {
            if (!TycoonSession.IsActive) return;
            if (unlock is Dish dish) ShowCustomerCut(__instance, dish, false);
            if (Container == null) return;
            object c = Container.GetValue(__instance);
            GameObject o = c as GameObject ?? (c as Component)?.gameObject;
            if (o == null) return;
            string bar = unlock != null && ManagementCardsView.AnyOpen ? Systems.TycoonRecipeMastery.BarText(unlock.CardID) : null;
            object text = bar != null ? Reward?.GetValue(__instance) : null;
            if (text != null)
            {
                text.GetType().GetProperty("richText")?.SetValue(text, true);
                text.GetType().GetProperty("enableWordWrapping")?.SetValue(text, false);
                text.GetType().GetProperty("text")?.SetValue(text, bar);
                if (!o.activeSelf) o.SetActive(true);
            }
            else if (o.activeSelf) o.SetActive(false);
        }

        internal static void ShowGroupSeats(UnlockCardElement element, bool show)
        {
            try
            {
                GameObject box = ChangeContainer?.GetValue(element) as GameObject;
                if (box == null) return;
                int percent = Mathf.RoundToInt(TycoonReputation.GroupSeatShare * 100f);
                object text = ChangeText?.GetValue(element);
                if (show && percent < 100 && text != null)
                {
                    text.GetType().GetProperty("enableWordWrapping")?.SetValue(text, false);
                    var property = text.GetType().GetProperty("text");
                    string tag = $"{percent}% seated";
                    if (property != null && (property.GetValue(text) as string) != tag) property.SetValue(text, tag);
                    if (!box.activeSelf) box.SetActive(true);
                }
                else if (box.activeSelf) box.SetActive(false);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Card group tag: " + e.Message);
            }
        }

        internal static void ShowCustomerCut(UnlockCardElement element, Dish dish, bool show)
        {
            try
            {
                GameObject box = ChangeContainer?.GetValue(element) as GameObject;
                if (box == null) return;
                int cut = show ? TycoonKitchenLoad.CardCustomerCut(dish) : 0;
                object text = ChangeText?.GetValue(element);
                if (cut > 0 && text != null)
                {
                    var property = text.GetType().GetProperty("text");
                    string tag = $"-{cut}% <size=130><sprite name=\"queue\" color=#D13E0F></size>";
                    if (property != null && (property.GetValue(text) as string) != tag) property.SetValue(text, tag);
                    if (!box.activeSelf) box.SetActive(true);
                }
                else if (box.activeSelf) box.SetActive(false);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Card customer tag: " + e.Message);
            }
        }
    }
}
