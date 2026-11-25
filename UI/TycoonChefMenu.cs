using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using PlateUpTycoon.Patches;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>Smokey's menu, drawn as a dialogue box.</summary>
    internal static class TycoonChefMenu
    {
        internal struct Held
        {
            public int Dish;
            public bool Paid;
            public bool Owned;
        }

        internal static readonly Dictionary<int, Held> Holding = new Dictionary<int, Held>();

        private static string _speech { get => DeskMenuContext.Get<string>("chef.speech"); set => DeskMenuContext.Set("chef.speech", value); }

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
                if (k != null && k.Length >= 4) held = new Held { Dish = k[1], Paid = k[2] != 0, Owned = k[3] != 0 };
            }
            return Root(held);
        }

        private static GridMenuConfig Root(Held held)
        {
            var items = new List<IGridItem> { PurchasingDeskMenu.CloseTile() };
            TycoonCompany.EnsureLoaded();
            string Fill(string line, string recipe, int price) => line.Replace("{recipe}", recipe).Replace("{price}", price.ToString());

            if (held.Dish == 0 || !GameData.Main.TryGet<Dish>(held.Dish, out Dish dish))
            {
                string hello;
                if (!TycoonCompany.MetChef && TycoonChefDialogue.All("introduction").Count > 0)
                {
                    hello = TycoonChefDialogue.Say("introduction");
                    TycoonCompany.MetChef = true;
                    TycoonCompany.Save();
                }
                else hello = TycoonChefDialogue.Say("greeting");
                _speech = hello;
            }
            else
            {
                int price = TycoonRecipeShop.Price(dish);
                int cash = TycoonNetData.IsHost ? TycoonCompany.Cash : TycoonBank.FromHost().Cash;
                if (held.Paid)
                    _speech = Fill(TycoonChefDialogue.Say("already-bought"), dish.Name, price);
                else if (held.Owned)
                    _speech = Fill(TycoonChefDialogue.Say("already-owned"), dish.Name, price);
                else if (cash < price)
                    _speech = Fill(TycoonChefDialogue.Say("too-poor"), dish.Name, price);
                else
                {
                    _speech = Fill(TycoonChefDialogue.Say("offer"), dish.Name, price);
                    items.Add(new GridItemChefBuy { Dish = dish.ID, Price = price });
                }
            }
            return Page(items);
        }

        internal static GridMenuConfig ConfirmPage(GridItemChefBuy buy) =>
            Page(new List<IGridItem>
            {
                new GridItemDeskBack { Title = "Back" },
                new GridItemChefConfirm { Dish = buy.Dish, Price = buy.Price },
            });

        internal static string SpeechFor(List<IGridItem> items)
        {
            foreach (IGridItem item in items)
                if (item is GridItemChefConfirm c)
                    return TycoonChefDialogue.Say("confirm").Replace("{recipe}", TycoonRecipeShop.NameOf(c.Dish)).Replace("{price}", c.Price.ToString());
            return _speech ?? TycoonChefDialogue.Say("greeting");
        }

        internal static string LabelFor(IGridItem item)
        {
            switch (item)
            {
                case GridItemDeskClose _: return "Leave";
                case GridItemDeskBack _: return "Back";
                case GridItemChefBuy b: return $"Buy {TycoonRecipeShop.NameOf(b.Dish)} for {b.Price} coins";
                case GridItemChefConfirm _: return "Yes, I'll take it";
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
