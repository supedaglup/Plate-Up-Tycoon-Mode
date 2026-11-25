using KitchenLib.Preferences;

namespace PlateUpTycoon
{
    /// <summary>Mod preferences, saved by KitchenLib.</summary>
    public static class TycoonPreferences
    {
        public const string PLAY_TUTORIAL = "playTutorial";
        public const string DEBUG_AUTO_START = "debugAutoStart";
        public const string SELL_UPGRADES = "sellUpgrades";
        public const string SHOW_GUIDES = "showGuides";
        public const string SHOW_REPUTATION_REPORT = "showReputationReport";
        public const string DEVELOPER_MODE = "developerMode";

        public static bool DeveloperMode => Manager == null || Manager.GetPreference<PreferenceBool>(DEVELOPER_MODE).Value;

        public static bool ShowReputationReport => Manager.GetPreference<PreferenceBool>(SHOW_REPUTATION_REPORT).Value;

        public static bool ShowGuides => Manager.GetPreference<PreferenceBool>(SHOW_GUIDES).Value;

        public static bool ShowCharacterTutorials => PlayTutorial && ShowGuides;

        public static PreferenceManager Manager { get; private set; }

        public static bool PlayTutorial => Manager.GetPreference<PreferenceBool>(PLAY_TUTORIAL).Value;

        public static bool DebugAutoStart => DeveloperMode && Manager.GetPreference<PreferenceBool>(DEBUG_AUTO_START).Value;

        public static bool SellUpgrades => Manager.GetPreference<PreferenceBool>(SELL_UPGRADES).Value;

        public static void Init()
        {
            Manager = new PreferenceManager(Mod.MOD_GUID);
            Manager.RegisterPreference(new PreferenceBool(PLAY_TUTORIAL, true));
            Manager.RegisterPreference(new PreferenceBool(DEBUG_AUTO_START, false));
            Manager.RegisterPreference(new PreferenceBool(SELL_UPGRADES, true));
            Manager.RegisterPreference(new PreferenceBool(SHOW_GUIDES, true));
            Manager.RegisterPreference(new PreferenceBool(SHOW_REPUTATION_REPORT, true));
            Manager.RegisterPreference(new PreferenceBool(DEVELOPER_MODE, false));
            Manager.Load();
            Manager.Save();
        }

        public static void Set(string key, bool value)
        {
            Manager.GetPreference<PreferenceBool>(key).Set(value);
            Manager.Save();
        }
    }
}
