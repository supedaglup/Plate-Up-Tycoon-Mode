using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PlateUpTycoon
{
    /// <summary>A talking cat's lines, read once from a text file built into the DLL. Lines are grouped by [section].</summary>
    public class CatDialogue
    {
        public const int MAX_LENGTH = 58;

        private readonly string _resource, _who, _defaultName;
        private readonly HashSet<string> _bubbleSections;
        private Dictionary<string, List<string>> _sections;
        private string _last;
        private static readonly System.Random Random = new System.Random();

        public CatDialogue(string resource, string who, string defaultName, params string[] bubbleSections)
        {
            _resource = resource;
            _who = who;
            _defaultName = defaultName;
            _bubbleSections = new HashSet<string>(bubbleSections);
        }

        public string Say(params string[] sections)
        {
            if (_sections == null) Load();
            var pool = new List<string>();
            foreach (string s in sections)
                if (_sections.TryGetValue(s, out List<string> lines)) pool.AddRange(lines);
            if (pool.Count == 0) return "Meow.";
            if (pool.Count > 1) pool.Remove(_last);
            _last = pool[Random.Next(pool.Count)];
            return Fill(_last);
        }

        private string Fill(string line) => line.Replace("{name}", Name).Replace("{days}", TycoonBank.GRACE_DAYS.ToString());

        public string Name
        {
            get
            {
                if (_sections == null) Load();
                return _sections.TryGetValue("name", out List<string> lines) && lines.Count > 0 ? lines[0] : _defaultName;
            }
        }

        public List<string> All(string section)
        {
            if (_sections == null) Load();
            var all = new List<string>();
            if (_sections.TryGetValue(section, out List<string> lines))
                foreach (string line in lines) all.Add(Fill(line));
            return all;
        }

        private void Load()
        {
            _sections = new Dictionary<string, List<string>>();
            try
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_resource);
                if (stream == null) { Mod.LogWarning($"{_who}: dialogue file missing from the build."); return; }
                using var reader = new StreamReader(stream);
                string section = "greeting";
                int count = 0;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant(); continue; }
                    if (line.Length > MAX_LENGTH && _bubbleSections.Contains(section)) Mod.LogWarning($"{_who}: this line is longer than {MAX_LENGTH} characters and will be cut off: {line}");
                    if (!_sections.TryGetValue(section, out List<string> list)) _sections[section] = list = new List<string>();
                    list.Add(line);
                    count++;
                }
                Mod.LogInfo($"{_who}: {count} line(s) of dialogue in {_sections.Count} section(s).");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning($"{_who}: could not read the dialogue: " + e.Message);
            }
        }
    }

    /// <summary>The banker's lines.</summary>
    public static class TycoonBankerDialogue
    {
        public const string RESOURCE_NAME = "PlateUpTycoon.BankerDialogue.txt";
        public const int MAX_LENGTH = CatDialogue.MAX_LENGTH;
        private static readonly CatDialogue Lines = new CatDialogue(RESOURCE_NAME, "Banker", "Mr. Meowser", "loan", "payment", "plan", "site-bought", "site-leased", "stop-thief");

        public static string Say(params string[] sections) => Lines.Say(sections);
        public static string Name => Lines.Name;
        public static List<string> All(string section) => Lines.All(section);
    }

    /// <summary>Smokey the chef cat's lines.</summary>
    public static class TycoonChefDialogue
    {
        public const string RESOURCE_NAME = "PlateUpTycoon.ChefDialogue.txt";
        private static readonly CatDialogue Lines = new CatDialogue(RESOURCE_NAME, "Chef", "Smokey", "bought", "stop-thief");

        public static string Say(params string[] sections) => Lines.Say(sections);
        public static string Name => Lines.Name;
        public static List<string> All(string section) => Lines.All(section);
    }

    /// <summary>Whiskers the discount appliance seller's lines.</summary>
    public static class TycoonDiscountDialogue
    {
        public const string RESOURCE_NAME = "PlateUpTycoon.DiscountDialogue.txt";
        private static readonly CatDialogue Lines = new CatDialogue(RESOURCE_NAME, "Discount seller", "Whiskers", "bought", "stop-thief");

        public static string Say(params string[] sections) => Lines.Say(sections);
        public static string Name => Lines.Name;
        public static List<string> All(string section) => Lines.All(section);
    }

    /// <summary>Chubs the card dealer cat's lines.</summary>
    public static class TycoonCardDialogue
    {
        public const string RESOURCE_NAME = "PlateUpTycoon.CardDialogue.txt";
        private static readonly CatDialogue Lines = new CatDialogue(RESOURCE_NAME, "Card dealer", "Chubs", "bought", "stop-thief");

        public static string Say(params string[] sections) => Lines.Say(sections);
        public static string Name => Lines.Name;
        public static List<string> All(string section) => Lines.All(section);
    }
}
