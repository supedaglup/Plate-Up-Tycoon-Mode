using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PlateUpTycoon
{
    /// <summary>The Tycoon guide: pages shown with the game's recipe pop-up, like the bank tutorial.</summary>
    public static class TycoonGuide
    {
        public const int SECTIONS = 7, MAX_PAGES = 8;
        public const int HQ = 0;
        public static int StarSection(int stars) => 1 + stars;

        private static string[] _titles;
        private static List<string>[] _pages;

        private static void Load()
        {
            if (_pages != null) return;
            _titles = new string[SECTIONS];
            _pages = new List<string>[SECTIONS];
            for (int i = 0; i < SECTIONS; i++) { _pages[i] = new List<string>(); _titles[i] = "Tycoon Mode"; }
            try
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlateUpTycoon.TycoonGuide.txt");
                if (stream == null) { Mod.LogWarning("Guide: TycoonGuide.txt not found."); return; }
                using var reader = new StreamReader(stream);
                int section = -1;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("["))
                    {
                        int end = line.IndexOf(']');
                        string key = end > 0 ? line.Substring(1, end - 1).Trim().ToLowerInvariant() : "";
                        section = key == "hq" ? HQ : key.StartsWith("star") && int.TryParse(key.Substring(4), out int s) && s >= 0 && s <= 5 ? StarSection(s) : -1;
                        if (section >= 0 && end > 0 && end + 1 < line.Length) _titles[section] = line.Substring(end + 1).Trim();
                        continue;
                    }
                    if (section < 0) continue;
                    if (_pages[section].Count < MAX_PAGES) _pages[section].Add(line);
                    else _pages[section][MAX_PAGES - 1] += "\n" + line;
                }
            }
            catch (System.Exception e) { Mod.LogWarning("Guide: could not read TycoonGuide.txt: " + e.Message); }
        }

        public static int PageCount(int section) { Load(); return section >= 0 && section < SECTIONS ? _pages[section].Count : 0; }
        public static string Title(int section) { Load(); return section >= 0 && section < SECTIONS ? _titles[section] : "Tycoon Mode"; }
        public static string Page(int section, int page) { Load(); return page >= 0 && page < PageCount(section) ? _pages[section][page] : ""; }

        public static bool IsGuideTitle(string title)
        {
            Load();
            foreach (string t in _titles) if (!string.IsNullOrEmpty(t) && title.StartsWith(t)) return true;
            return false;
        }

        public static bool Enabled => TycoonPreferences.ShowGuides;

        public static bool Seen(int section) { TycoonCompany.EnsureLoaded(); return (TycoonCompany.GuideSeen & (1 << section)) != 0; }

        public static void MarkSeen(int section)
        {
            TycoonCompany.EnsureLoaded();
            TycoonCompany.GuideSeen |= 1 << section;
            TycoonCompany.Save();
        }

        public static List<int> PageIDs(int section)
        {
            var ids = new List<int>();
            for (int p = 0; p < PageCount(section) && p < MAX_PAGES; p++)
                if (Mod.GuidePages[section, p] != 0) ids.Add(Mod.GuidePages[section, p]);
            return ids;
        }
    }
}
