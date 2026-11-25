using HarmonyLib;
using Kitchen;
using PlateUpTycoon.Systems;
using System.Reflection;

namespace PlateUpTycoon.Patches
{
    /// <summary>On every player's machine: when the game's generic prompt label carries a Tycoon announcement.</summary>
    [HarmonyPatch(typeof(GenericPromptIndicatorView), "UpdateData")]
    public static class AnnouncementViewPatch
    {
        private const BindingFlags ANY = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [HarmonyPostfix]
        private static void Postfix(GenericPromptIndicatorView __instance, object[] __args)
        {
            try
            {
                object data = __args[0];
                object info = data.GetType().GetField("AdditionalInfo", ANY)?.GetValue(data);
                string text = info?.ToString();
                if (TycoonNetData.IsData(text))
                {
                    TycoonNetData.Receive(text);
                    SetText(__instance, "ActiveText", "");
                    SetEnabled(__instance, "AdditionalText", false);
                    HidePrompt(__instance);
                    __instance.transform.localScale = UnityEngine.Vector3.zero;
                    return;
                }
                if (text == null || !text.StartsWith(ManageTycoonAnnouncements.MARKER)) return;

                SetText(__instance, "ActiveText", text.Substring(ManageTycoonAnnouncements.MARKER.Length));
                SetEnabled(__instance, "AdditionalText", false);
                HidePrompt(__instance);
            }
            catch { }
        }

        private static void HidePrompt(GenericPromptIndicatorView view)
        {
            if (AccessTools.Field(typeof(GenericPromptIndicatorView), "ActivePrompt")?.GetValue(view) is Kitchen.Modules.InputPromptElement prompt && prompt != null) prompt.Hide();
        }

        private static void SetText(object view, string field, string value)
        {
            object tmp = AccessTools.Field(view.GetType(), field)?.GetValue(view);
            tmp?.GetType().GetProperty("text")?.SetValue(tmp, value);
        }

        private static void SetEnabled(object view, string field, bool value)
        {
            object behaviour = AccessTools.Field(view.GetType(), field)?.GetValue(view);
            behaviour?.GetType().GetProperty("enabled")?.SetValue(behaviour, value);
        }
    }
}
