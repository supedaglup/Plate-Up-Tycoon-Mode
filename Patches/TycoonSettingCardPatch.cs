using HarmonyLib;
using Kitchen;
using KitchenData;
using System.Collections.Generic;

namespace PlateUpTycoon.Patches
{
    /// <summary>Some locations hand out a card when the restaurant is made.</summary>
    [HarmonyPatch(typeof(CreateCardSetsFromSetting), "OnUpdate")]
    public static class TycoonSettingCardPatch
    {
        private static readonly string[] BLOCKED = { "Community", "Turbo" };
        private static readonly List<(RestaurantSetting setting, Unlock card)> _held = new List<(RestaurantSetting, Unlock)>();
        private static readonly HashSet<int> _logged = new HashSet<int>();

        private static bool Blocked(Unlock card)
        {
            string name = card.Name ?? "";
            foreach (string b in BLOCKED)
                if (name.IndexOf(b, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        [HarmonyPrefix]
        private static void Prefix()
        {
            _held.Clear();
            if (!TycoonSession.IsActive) return;
            foreach (RestaurantSetting setting in GameData.Main.Get<RestaurantSetting>())
            {
                Unlock card = setting?.StartingUnlock;
                if (card == null || !Blocked(card)) continue;
                _held.Add((setting, card));
                setting.StartingUnlock = null;
                if (_logged.Add(setting.ID)) Mod.LogInfo($"Locations: '{setting.Name}' gives no '{card.Name}' card in Tycoon.");
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            foreach ((RestaurantSetting setting, Unlock card) in _held) setting.StartingUnlock = card;
            _held.Clear();
        }
    }
}
