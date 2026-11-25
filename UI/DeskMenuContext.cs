using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>Several players on one machine can each have a desk menu open at once.</summary>
    internal static class DeskMenuContext
    {
        internal static DishSelectionIndicator Current;

        private static readonly Dictionary<int, Dictionary<string, object>> Values = new Dictionary<int, Dictionary<string, object>>();

        private static int Key(DishSelectionIndicator indicator) => (object)indicator == null ? 0 : indicator.GetInstanceID();

        internal static T Get<T>(string name)
        {
            if (Values.TryGetValue(Key(Current), out Dictionary<string, object> mine) && mine.TryGetValue(name, out object value) && value is T t) return t;
            return default;
        }

        internal static void Set<T>(string name, T value)
        {
            int key = Key(Current);
            if (!Values.TryGetValue(key, out Dictionary<string, object> mine)) Values[key] = mine = new Dictionary<string, object>();
            if (value == null) mine.Remove(name); else mine[name] = value;
        }

        internal static void Forget(DishSelectionIndicator indicator)
        {
            Values.Remove(Key(indicator));
            if ((object)Current == (object)indicator) Current = null;
        }

        internal static DishSelectionIndicator IndicatorOf(GridMenu menu)
        {
            if (menu == null) return null;
            foreach (DishSelectionIndicator indicator in Patches.PurchasingDeskMenu.Kinds.Keys)
            {
                if ((object)indicator == null || indicator == null) continue;
                if (ReferenceEquals(AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(indicator), menu)) return indicator;
            }
            return null;
        }

        internal static int ScreenSlot(DishSelectionIndicator indicator, out float x)
        {
            x = 0.5f;
            var open = new List<(int player, DishSelectionIndicator i)>();
            foreach (KeyValuePair<DishSelectionIndicator, Patches.PurchasingDeskMenu.DeskKind> kv in Patches.PurchasingDeskMenu.Kinds)
            {
                if ((object)kv.Key == null || kv.Key == null) continue;
                if (!Patches.PurchasingDeskMenu.Talks(kv.Value) && kv.Value != Patches.PurchasingDeskMenu.DeskKind.Management) continue;
                open.Add(((int)AccessTools.Field(typeof(DishSelectionIndicator), "PlayerID").GetValue(kv.Key), kv.Key));
            }
            if (open.Count <= 1) return 1;
            open.Sort((a, b) => a.player.CompareTo(b.player));
            int index = open.FindIndex(o => ReferenceEquals(o.i, indicator));
            if (index < 0) return 1;
            x = (index + 0.5f) / open.Count;
            return open.Count;
        }
    }
}
