using Kitchen;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Record of every purchase made at the Purchasing Desk.</summary>
    public static class TycoonPurchaseLog
    {
        public struct Entry
        {
            public int Day;
            public string Player;
            public string Item;
            public int Cost;
            public DateTime Time;

            public override string ToString() => $"Day {Day}  {Time:HH:mm:ss}  {Player} bought {Item} ({Cost})";
        }

        private const int MAX_IN_MEMORY = 200;
        private static readonly List<Entry> _entries = new List<Entry>();

        public static IReadOnlyList<Entry> Entries => _entries;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "TycoonPurchaseLog.txt");

        public static void Add(int day, string player, string item, int cost)
        {
            var entry = new Entry { Day = day, Player = player, Item = item, Cost = cost, Time = DateTime.Now };
            _entries.Add(entry);
            if (_entries.Count > MAX_IN_MEMORY) _entries.RemoveAt(0);

            if (!TycoonPreferences.DeveloperMode) return;
            try { File.AppendAllText(FilePath, $"{entry.Time:yyyy-MM-dd} {entry}{Environment.NewLine}"); }
            catch (Exception e) { Mod.LogWarning("Could not write the purchase log file: " + e.Message); }
        }

        public static string PlayerName(int playerID)
        {
            try
            {
                string name = Players.Main.Get(playerID).Name;
                return string.IsNullOrEmpty(name) ? $"Player {playerID}" : name;
            }
            catch
            {
                return $"Player {playerID}";
            }
        }
    }
}
