using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using PlateUpTycoon.Patches;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>"Buy this appliance box" on Whiskers's menu.</summary>
    public struct GridItemCrateBuy : IGridItem
    {
        public int Appliance;
        public int Price;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => null;
    }

    /// <summary>"Yes, please" on Whiskers's confirmation page.</summary>
    public struct GridItemCrateConfirm : IGridItem
    {
        public int Appliance;
        public int Price;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => null;
    }

    /// <summary>What Whiskers's menu reports: the appliance bought.</summary>
    public class TycoonCrateChoice
    {
        public int Appliance;
    }

    /// <summary>Whiskers's menu, drawn as a dialogue box.</summary>
    internal static class TycoonDiscountMenu
    {
        internal struct Held
        {
            public int Appliance;
            public int Price;
            public int Discount;
            public bool Paid;
        }

        internal static readonly Dictionary<int, Held> Holding = new Dictionary<int, Held>();
        private static string _speech { get => DeskMenuContext.Get<string>("discount.speech"); set => DeskMenuContext.Set("discount.speech", value); }
        private static Held _held { get => DeskMenuContext.Get<Held>("discount.held"); set => DeskMenuContext.Set("discount.held", value); }

        internal static string Fill(string line, Held h)
        {
            int full = KitchenData.GameData.Main.TryGet<KitchenData.Appliance>(h.Appliance, out _) && TycoonCatalogue.TryGetAny(h.Appliance, out TycoonCatalogue.Entry e) ? TycoonShopPrices.OrderPrice(e) : h.Price;
            return line.Replace("{appliance}", TycoonDiscountShop.NameOf(h.Appliance)).Replace("{price}", h.Price.ToString())
                .Replace("{discount}", h.Discount.ToString()).Replace("{full}", full.ToString());
        }

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
                if (k != null && k.Length >= 5 && k[0] == (int)PurchasingDeskMenu.DeskKind.Discount)
                    held = new Held { Appliance = k[1], Price = k[2], Discount = k[3], Paid = k[4] != 0 };
            }
            return Root(held);
        }

        private static GridMenuConfig Root(Held held)
        {
            _held = held;
            var items = new List<IGridItem> { PurchasingDeskMenu.CloseTile() };
            TycoonCompany.EnsureLoaded();
            if (held.Appliance == 0)
            {
                if (!TycoonCompany.MetWhiskers && TycoonDiscountDialogue.All("introduction").Count > 0)
                {
                    _speech = TycoonDiscountDialogue.Say("introduction");
                    TycoonCompany.MetWhiskers = true;
                    TycoonCompany.Save();
                }
                else _speech = TycoonDiscountDialogue.Say("greeting");
            }
            else
            {
                int cash = TycoonNetData.IsHost ? TycoonCompany.Cash : TycoonBank.FromHost().Cash;
                if (held.Paid) _speech = Fill(TycoonDiscountDialogue.Say("already-bought"), held);
                else if (cash < held.Price) _speech = Fill(TycoonDiscountDialogue.Say("too-poor"), held);
                else
                {
                    _speech = Fill(TycoonDiscountDialogue.Say("offer"), held);
                    items.Add(new GridItemCrateBuy { Appliance = held.Appliance, Price = held.Price });
                }
            }
            return Page(items);
        }

        internal static GridMenuConfig ConfirmPage(GridItemCrateBuy buy) =>
            Page(new List<IGridItem>
            {
                new GridItemDeskBack { Title = "Back" },
                new GridItemCrateConfirm { Appliance = buy.Appliance, Price = buy.Price },
            });

        internal static string SpeechFor(List<IGridItem> items)
        {
            foreach (IGridItem item in items)
                if (item is GridItemCrateConfirm) return Fill(TycoonDiscountDialogue.Say("confirm"), _held);
            return _speech ?? TycoonDiscountDialogue.Say("greeting");
        }

        internal static string LabelFor(IGridItem item)
        {
            switch (item)
            {
                case GridItemDeskClose _: return "Leave";
                case GridItemDeskBack _: return "Back";
                case GridItemCrateBuy b: return $"Buy {TycoonDiscountShop.NameOf(b.Appliance)} for {b.Price} coins";
                case GridItemCrateConfirm _: return "Yes, please";
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
