using HarmonyLib;
using Kitchen;

namespace PlateUpTycoon.Patches
{
    /// <summary>The game's own play menus.</summary>
    [HarmonyPatch(typeof(SingleplayerMainMenu), "Setup")]
    public static class SingleplayerMenuSessionPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            if (!TycoonSession.OpenedFromTycoonMenu) TycoonSession.End();
        }
    }

    [HarmonyPatch(typeof(MultiplayerLoadingMenu), "Setup")]
    public static class MultiplayerMenuSessionPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            if (!TycoonSession.OpenedFromTycoonMenu) TycoonSession.End();
        }
    }

    /// <summary>New Tycoon characters: honour the "Tutorial" preference.</summary>
    [HarmonyPatch(typeof(GameCreator), "CreateGameWorld")]
    public static class SkipTutorialPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Mod.LogInfo($"Creating game world (Tycoon session: {TycoonSession.IsActive}, Tycoon saves: {TycoonSaveData.IsActive}).");
            if (!TycoonSession.IsActive || TycoonPreferences.PlayTutorial) return;

            foreach (RetainedPlayer player in Session.RetainedPlayers)
                ProfileAccessor.SetNeedsTutorial(player.PlayerProfile, false);
            Mod.LogInfo("Tutorial skipped (Tycoon preference).");
        }
    }
}
