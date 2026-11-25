using KitchenData;
using KitchenLib.References;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>The folder tree of the Purchasing Desk menu, read from Resources/PurchasingDeskMenu.txt.</summary>
    public static class TycoonMenuLayout
    {
        public const string RESOURCE_NAME = "PlateUpTycoon.PurchasingDeskMenu.txt";
        public const string LOG_FILE = "TycoonMenuLayout.txt";

        public class Folder
        {
            public string Name;
            public int IconApplianceID;
            public TycoonCatalogue.Category? Fallback;
            public readonly List<Folder> Folders = new List<Folder>();
            public readonly List<TycoonCatalogue.Entry> Appliances = new List<TycoonCatalogue.Entry>();
            public readonly HashSet<int> Auto = new HashSet<int>();

            public int TotalAppliances()
            {
                int n = Appliances.Count;
                foreach (Folder f in Folders) n += f.TotalAppliances();
                return n;
            }

            public int FirstApplianceID()
            {
                if (Appliances.Count > 0) return Appliances[0].ApplianceID;
                foreach (Folder f in Folders)
                {
                    int id = f.FirstApplianceID();
                    if (id != 0) return id;
                }
                return 0;
            }
        }

        private static Folder _root;
        private static bool _builtWithUpgrades, _builtWithDecor;

        public static Folder Root
        {
            get
            {
                if (_root == null || _builtWithUpgrades != TycoonUnlocks.UpgradesForSale || _builtWithDecor != TycoonUnlocks.DecorForSale)
                {
                    _builtWithUpgrades = TycoonUnlocks.UpgradesForSale;
                    _builtWithDecor = TycoonUnlocks.DecorForSale;
                    _root = Build();
                }
                return _root;
            }
        }

        private class RawFolder
        {
            public string Name;
            public string IconRef;
            public string FallbackName;
            public readonly List<object> Children = new List<object>();
            public int Line;
        }

        private static RawFolder Parse(string text)
        {
            var root = new RawFolder { Name = "Purchasing Desk" };
            var stack = new List<(int indent, RawFolder folder)> { (-1, root) };
            string[] lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
                int indent = raw.Length - raw.TrimStart(' ').Length;

                while (stack.Count > 1 && stack[stack.Count - 1].indent >= indent) stack.RemoveAt(stack.Count - 1);
                RawFolder parent = stack[stack.Count - 1].folder;

                if (trimmed.StartsWith("+"))
                {
                    var folder = new RawFolder { Line = i + 1 };
                    string rest = trimmed.Substring(1).Trim();
                    rest = TakeBracketed(rest, '{', '}', out folder.FallbackName);
                    rest = TakeBracketed(rest, '[', ']', out folder.IconRef);
                    folder.Name = rest.Trim();
                    parent.Children.Add(folder);
                    stack.Add((indent, folder));
                }
                else
                {
                    parent.Children.Add(trimmed);
                }
            }
            return root;
        }

        private static string TakeBracketed(string text, char open, char close, out string inside)
        {
            inside = null;
            int a = text.IndexOf(open);
            int b = a < 0 ? -1 : text.IndexOf(close, a + 1);
            if (a < 0 || b < 0) return text;
            inside = text.Substring(a + 1, b - a - 1).Trim();
            return text.Remove(a, b - a + 1);
        }

        private static Dictionary<string, int> _references;

        private static bool TryResolve(string name, out int id)
        {
            if (_references == null)
            {
                _references = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyInfo p in typeof(ApplianceReferences).GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (p.PropertyType == typeof(int)) _references[p.Name] = (int)p.GetValue(null);
                }
            }
            if (_references.TryGetValue(name, out id)) return true;
            if (Mod.CustomApplianceNames.TryGetValue(name, out id)) return true;
            return int.TryParse(name, out id);
        }

        private static Folder Build()
        {
            string text = ReadLayoutText();
            RawFolder raw = Parse(text ?? "");

            var placed = new HashSet<int>();
            _collapsed.Clear();
            Folder root = Resolve(raw, placed, isRoot: true);
            DropLooseDuplicates(root);

            foreach (TycoonCatalogue.Entry entry in TycoonCatalogue.Entries)
            {
                if (placed.Contains(entry.ApplianceID)) continue;
                Folder target = null;
                foreach (Folder top in root.Folders)
                    if (top.Fallback == entry.Category) { target = top; break; }
                if (target == null)
                {
                    foreach (Folder top in root.Folders)
                        if (top.Fallback == TycoonCatalogue.Category.Miscellaneous) { target = top; break; }
                }
                if (target == null)
                {
                    target = new Folder { Name = "Other", Fallback = TycoonCatalogue.Category.Miscellaneous };
                    root.Folders.Add(target);
                }
                target.Appliances.Add(entry);
                target.Auto.Add(entry.ApplianceID);
            }

            if (TycoonUnlocks.DecorForSale) root.Folders.Add(DecorFolder());

            Finish(root);
            WriteLog(root);
            return root;
        }

        private static Folder DecorFolder()
        {
            var top = new Folder { Name = "Wallpaper & Flooring" };
            var walls = new Folder { Name = "Wallpaper" };
            var floors = new Folder { Name = "Flooring" };
            foreach (TycoonDecorShop.Offer o in TycoonDecorShop.Offers)
            {
                var entry = new TycoonCatalogue.Entry { ApplianceID = o.DecorID, Name = o.Name, Cost = o.Cost, Category = TycoonCatalogue.Category.Decorations };
                Folder target = o.Type == KitchenData.LayoutMaterialType.Floor ? floors : walls;
                target.Appliances.Add(entry);
                if (target.IconApplianceID == 0) target.IconApplianceID = o.ApplicatorID;
                if (top.IconApplianceID == 0) top.IconApplianceID = o.ApplicatorID;
            }
            if (walls.Appliances.Count > 0) top.Folders.Add(walls);
            if (floors.Appliances.Count > 0) top.Folders.Add(floors);
            return top;
        }

        private static Folder Resolve(RawFolder raw, HashSet<int> placed, bool isRoot)
        {
            var folder = new Folder { Name = raw.Name };
            if (raw.FallbackName != null)
            {
                if (Enum.TryParse(raw.FallbackName, true, out TycoonCatalogue.Category category)) folder.Fallback = category;
                else Mod.LogWarning($"Menu layout line {raw.Line}: unknown fallback category '{raw.FallbackName}'.");
            }
            if (raw.IconRef != null)
            {
                if (TryResolve(raw.IconRef, out int iconID)) folder.IconApplianceID = iconID;
                else Mod.LogWarning($"Menu layout line {raw.Line}: unknown icon appliance '{raw.IconRef}'.");
            }

            var seen = new HashSet<int>();
            foreach (object child in raw.Children)
            {
                if (child is RawFolder rawChild)
                {
                    Folder sub = Resolve(rawChild, placed, isRoot: false);
                    int total = sub.TotalAppliances();
                    if (total == 0 && !isRoot) continue;
                    if (!isRoot && sub.Folders.Count == 0 && sub.Appliances.Count == 1)
                    {
                        TycoonCatalogue.Entry only = sub.Appliances[0];
                        if (seen.Add(only.ApplianceID))
                        {
                            folder.Appliances.Add(only);
                            _collapsed.Add((folder, only.ApplianceID));
                        }
                        continue;
                    }
                    folder.Folders.Add(sub);
                }
                else if (child is string name)
                {
                    if (!TryResolve(name, out int id))
                    {
                        Mod.LogWarning($"Menu layout: unknown appliance '{name}' (folder '{raw.Name}').");
                        continue;
                    }
                    if (!TycoonCatalogue.TryGet(id, out TycoonCatalogue.Entry entry)) continue;
                    placed.Add(id);
                    if (seen.Add(id)) folder.Appliances.Add(entry);
                }
            }
            return folder;
        }

        private static readonly List<(Folder folder, int applianceID)> _collapsed = new List<(Folder, int)>();

        private static void CountAppliances(Folder folder, Dictionary<int, int> counts)
        {
            foreach (TycoonCatalogue.Entry e in folder.Appliances)
                counts[e.ApplianceID] = counts.TryGetValue(e.ApplianceID, out int n) ? n + 1 : 1;
            foreach (Folder sub in folder.Folders) CountAppliances(sub, counts);
        }

        private static void DropLooseDuplicates(Folder root)
        {
            var counts = new Dictionary<int, int>();
            CountAppliances(root, counts);
            foreach ((Folder folder, int id) in _collapsed)
            {
                if (counts[id] <= 1) continue;
                folder.Appliances.RemoveAll(e => e.ApplianceID == id);
                counts[id]--;
            }
        }

        private static void Finish(Folder folder)
        {
            folder.Appliances.Sort((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : string.CompareOrdinal(a.Name, b.Name));
            folder.Folders.RemoveAll(f => f.TotalAppliances() == 0);
            foreach (Folder sub in folder.Folders) Finish(sub);
            if (folder.IconApplianceID == 0 || !GameData.Main.TryGet<Appliance>(folder.IconApplianceID, out _))
                folder.IconApplianceID = folder.FirstApplianceID();
        }

        private static string ReadLayoutText()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCE_NAME))
                {
                    if (stream == null)
                    {
                        Mod.LogError($"Menu layout '{RESOURCE_NAME}' is missing from the mod DLL; the menu only has fallback folders.");
                        return null;
                    }
                    using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Mod.LogError("Could not read the menu layout: " + e.Message);
                return null;
            }
        }

        private static void WriteLog(Folder root)
        {
            if (!PlateUpTycoon.TycoonPreferences.DeveloperMode) return;
            var sb = new StringBuilder();
            sb.AppendLine("Purchasing Desk menu layout");
            sb.AppendLine($"Written {DateTime.Now:yyyy-MM-dd HH:mm}. {TycoonCatalogue.Entries.Count} appliances for sale (stars {TycoonUnlocks.Stars}: upgrades {(TycoonUnlocks.UpgradesForSale ? "included" : "not sold")}, wallpaper and flooring {(TycoonUnlocks.DecorForSale ? "included" : "not sold")}).");
            sb.AppendLine("Folders are marked with >. Prices in coins. (upgrade) = upgrade-only appliance. (auto) = not named in the layout file, placed by the game's shop tags.");
            sb.AppendLine("Main page: [X Close] then the folders below.");
            sb.AppendLine();
            foreach (Folder top in root.Folders) WriteFolder(sb, top, 0, top.Name);

            string text = sb.ToString();
            Mod.LogInfo("\n" + text);
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, LOG_FILE), text);
            }
            catch (Exception e)
            {
                Mod.LogWarning("Could not write " + LOG_FILE + ": " + e.Message);
            }
        }

        private static void WriteFolder(StringBuilder sb, Folder folder, int depth, string path)
        {
            string pad = new string(' ', depth * 4);
            sb.AppendLine($"{pad}> {folder.Name}    [{path}]");
            foreach (Folder sub in folder.Folders) WriteFolder(sb, sub, depth + 1, path + " > " + sub.Name);
            foreach (TycoonCatalogue.Entry entry in folder.Appliances)
                sb.AppendLine($"{pad}    - {entry.Name} ({entry.Cost}){(entry.IsUpgrade ? "  (upgrade)" : "")}{(folder.Auto.Contains(entry.ApplianceID) ? "  (auto)" : "")}");
        }
    }
}
