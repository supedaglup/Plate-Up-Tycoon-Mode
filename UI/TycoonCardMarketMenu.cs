using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using PlateUpTycoon.Patches;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>Chubs's menu, drawn as a dialogue box.</summary>
    internal static class TycoonCardMarketMenu
    {
        internal struct Held
        {
            public int Card;
            public bool Paid;
            public bool Owned;
        }

        internal static readonly Dictionary<int, Held> Holding = new Dictionary<int, Held>();

        private static string _speech { get => DeskMenuContext.Get<string>("card.speech"); set => DeskMenuContext.Set("card.speech", value); }

        internal static string RepText(int cardID) => TycoonReputation.CardValue(cardID).ToString("0.#");

        internal static string Fill(string line, int card, int price) =>
            line.Replace("{card}", TycoonCardMarket.NameOf(card)).Replace("{price}", price.ToString()).Replace("{rep}", RepText(card));

        internal static GridMenuConfig Start(DishSelectionIndicator indicator)
        {
            int playerID = (int)AccessTools.Field(typeof(DishSelectionIndicator), "PlayerID").GetValue(indicator);
            Held held;
            if (TycoonNetData.IsHost)
            {
                Holding.TryGetValue(playerID, out held);
                Holding.Remove(playerID);
            }
            else
            {
                held = new Held();
                int[] k = TycoonNetData.Ints("k" + playerID);
                if (k != null && k.Length >= 4 && k[0] == (int)PurchasingDeskMenu.DeskKind.Cards) held = new Held { Card = k[1], Paid = k[2] != 0, Owned = k[3] != 0 };
            }
            return Root(held);
        }

        private static GridMenuConfig Root(Held held)
        {
            var items = new List<IGridItem> { PurchasingDeskMenu.CloseTile() };
            TycoonCompany.EnsureLoaded();

            if (held.Card == 0 || !GameData.Main.TryGet<Unlock>(held.Card, out Unlock card))
            {
                if (!TycoonCompany.MetDealer && TycoonCardDialogue.All("introduction").Count > 0)
                {
                    _speech = TycoonCardDialogue.Say("introduction");
                    TycoonCompany.MetDealer = true;
                    TycoonCompany.Save();
                }
                else _speech = TycoonCardDialogue.Say("greeting");
            }
            else
            {
                int price = TycoonCardMarket.Price(card);
                int cash = TycoonNetData.IsHost ? TycoonCompany.Cash : TycoonBank.FromHost().Cash;
                if (held.Paid) _speech = Fill(TycoonCardDialogue.Say("already-bought"), card.ID, price);
                else if (held.Owned) _speech = Fill(TycoonCardDialogue.Say("already-owned"), card.ID, price);
                else if (cash < price) _speech = Fill(TycoonCardDialogue.Say("too-poor"), card.ID, price);
                else
                {
                    _speech = Fill(TycoonCardDialogue.Say("offer"), card.ID, price);
                    items.Add(new GridItemCardBuy { Card = card.ID, Price = price });
                }
            }
            return Page(items);
        }

        internal static GridMenuConfig ConfirmPage(GridItemCardBuy buy) =>
            Page(new List<IGridItem>
            {
                new GridItemDeskBack { Title = "Back" },
                new GridItemCardConfirm { Card = buy.Card, Price = buy.Price },
            });

        internal static string SpeechFor(List<IGridItem> items)
        {
            foreach (IGridItem item in items)
                if (item is GridItemCardConfirm c) return Fill(TycoonCardDialogue.Say("confirm"), c.Card, c.Price);
            return _speech ?? TycoonCardDialogue.Say("greeting");
        }

        internal static string LabelFor(IGridItem item)
        {
            switch (item)
            {
                case GridItemDeskClose _: return "Leave";
                case GridItemDeskBack _: return "Back";
                case GridItemCardBuy b: return $"Buy {TycoonCardMarket.NameOf(b.Card)} for {b.Price} coins";
                case GridItemCardConfirm _: return "Yes, deal";
                default: return "?";
            }
        }

        internal static string TitleFor(IGridItem item) => "";

        private static GridMenuConfig Page(List<IGridItem> items)
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = items;
            return page;
        }
    }
}
