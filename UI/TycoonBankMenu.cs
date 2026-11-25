using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using PlateUpTycoon.Components;
using PlateUpTycoon.Patches;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>The banker's menu, rebuilt from TycoonBank each time it opens.</summary>
    internal static class TycoonBankMenu
    {
        private static Texture2D _coinIcon, _payIcon, _planIcon, _dayIcon;

        internal static TycoonBank.Standing Standing;

        internal static GridMenuConfig Start() => Root();

        internal static GridMenuConfig Start(DishSelectionIndicator indicator)
        {
            int playerID = (int)AccessTools.Field(typeof(DishSelectionIndicator), "PlayerID").GetValue(indicator);
            if (!TycoonNetData.IsHost)
            {
                int[] k = TycoonNetData.Ints("k" + playerID);
                if (k != null && k.Length >= 5) return SitePage(new CTycoonBlueprint { Setting = k[1], Size = k[2], Price = k[3], Paid = k[4] != 0 });
                return Root();
            }
            if (Holding.TryGetValue(playerID, out CTycoonBlueprint site)) { Holding.Remove(playerID); return SitePage(site); }
            return Root();
        }

        internal static readonly Dictionary<int, CTycoonBlueprint> Holding = new Dictionary<int, CTycoonBlueprint>();
        private static CTycoonBlueprint? _site { get => DeskMenuContext.Get<CTycoonBlueprint?>("bank.site"); set => DeskMenuContext.Set("bank.site", value); }
        private static string _siteSpeech { get => DeskMenuContext.Get<string>("bank.siteSpeech"); set => DeskMenuContext.Set("bank.siteSpeech", value); }
        private static string _guestSpeech { get => DeskMenuContext.Get<string>("bank.guestSpeech"); set => DeskMenuContext.Set("bank.guestSpeech", value); }

        private static GridMenuConfig GuestPage()
        {
            NewConversation();
            _site = null;
            _guestSpeech = TycoonBankerDialogue.Say("host-only");
            return Page(new List<IGridItem> { PurchasingDeskMenu.CloseTile() });
        }

        internal static GridMenuConfig SitePage(CTycoonBlueprint site)
        {
            Standing = TycoonNetData.IsHost ? TycoonBank.Read(KitchenLib.Utils.EntityUtils.GetEntityManager()) : TycoonBank.FromHost();
            NewConversation();
            _site = site;
            _guestSpeech = null;
            string Fill(string line) => line.Replace("{setting}", TycoonSites.SettingName(site.Setting)).Replace("{size}", site.Size.ToString())
                .Replace("{price}", site.Price.ToString()).Replace("{credit}", Standing.Credit.ToString()).Replace("{cash}", Standing.Cash.ToString());
            var items = new List<IGridItem> { PurchasingDeskMenu.CloseTile() };
            string buy = site.Paid ? "yours" : TycoonSites.CanBuy(Standing, site.Price);
            string lease = site.Paid ? "yours" : TycoonSites.CanLease(Standing, site.Price);
            if (site.Paid || buy == "full") _siteSpeech = Fill(TycoonBankerDialogue.Say("site-" + buy));
            else if (buy != null && lease != null)
                _siteSpeech = Fill(TycoonBankerDialogue.Say("site-offer")) + "\n\n" + Fill(TycoonBankerDialogue.Say("site-" + lease)) + $" You have {Standing.Cash} coins.";
            else
            {
                _siteSpeech = Fill(TycoonBankerDialogue.Say("site-offer"));
                if (buy == null)
                    items.Add(new GridItemBankChoice { Kind = CTycoonBankRequest.SITE_BUY, Amount = site.Price, Icon = Icon(ref _coinIcon, new Color(0.95f, 0.75f, 0.2f)) });
                else _siteSpeech += $"\n\nYou have {Standing.Cash} coins, so you would have to lease it.";
                if (lease == null)
                    items.Add(new GridItemBankChoice { Kind = CTycoonBankRequest.SITE_LEASE, Amount = site.Price, Icon = Icon(ref _planIcon, new Color(0.35f, 0.55f, 0.9f)) });
            }
            Mod.LogInfo($"Bank: site menu for {TycoonSites.SettingName(site.Setting)} {site.Size} sqm at {site.Price} (buy: {buy ?? "yes"}, lease: {lease ?? "yes"}); credit {Standing.Credit}, cash {Standing.Cash}.");
            return Page(items);
        }

        internal static GridMenuConfig TutorialPage(int page) =>
            Page(new List<IGridItem> { new GridItemBankTip { Page = page } });

        internal static GridMenuConfig AfterTutorialPage(int page) =>
            page + 1 < TycoonBankerDialogue.All("tutorial").Count ? TutorialPage(page + 1) : Root();

        internal static GridMenuConfig Root()
        {
            Standing = TycoonNetData.IsHost ? TycoonBank.Read(KitchenLib.Utils.EntityUtils.GetEntityManager()) : TycoonBank.FromHost();
            NewConversation();
            _site = null;
            _guestSpeech = null;
            var items = new List<IGridItem> { PurchasingDeskMenu.CloseTile() };
            if (!Standing.HasLoan)
            {
                if (Standing.Offer > 0)
                    items.Add(new GridItemBankChoice { Kind = CTycoonBankRequest.LOAN, Amount = Standing.Offer, Icon = Icon(ref _coinIcon, new Color(0.95f, 0.75f, 0.2f)) });
            }
            else if (Standing.Owed > 0 && !Standing.PendingPlan)
            {
                foreach (int amount in TycoonBank.Payments(Standing))
                    items.Add(new GridItemBankChoice { Kind = CTycoonBankRequest.PAYMENT, Amount = amount, Icon = Icon(ref _payIcon, new Color(0.3f, 0.7f, 0.35f)) });
                items.Add(new GridItemBankChoice { Kind = CTycoonBankRequest.PLAN, Icon = Icon(ref _planIcon, new Color(0.35f, 0.55f, 0.9f)) });
            }
            Mod.LogInfo($"Bank: menu built; credit {Standing.Credit}, offer {Standing.Offer}, owed {Standing.Owed} ({Standing.DailyPayment} a day), cash {Standing.Cash}, {Standing.Restaurants} restaurant(s).");
            return Page(items);
        }

        internal static GridMenuConfig PlansPage(GridItemBankChoice choice)
        {
            var items = new List<IGridItem> { new GridItemDeskBack { Title = choice.Kind == CTycoonBankRequest.LOAN || choice.Kind == CTycoonBankRequest.SITE_LEASE ? "Back (choose how many days to repay over)" : "Back" } };
            foreach (int days in TycoonBank.TERMS)
                items.Add(new GridItemBankChoice { Kind = choice.Kind, Amount = choice.Amount, Days = days, Icon = Icon(ref _dayIcon, new Color(0.85f, 0.85f, 0.85f)) });
            return Page(items);
        }

        internal static GridMenuConfig ConfirmPage(GridItemBankChoice choice)
        {
            return Page(new List<IGridItem>
            {
                new GridItemDeskBack { Title = Describe(choice) + "?" },
                new GridItemBankConfirm { Value = TycoonBank.Encode(choice.Kind, choice.Days, choice.Amount), Text = Describe(choice), Icon = PurchasingDeskMenu.TickIcon() },
            });
        }

        internal static bool NeedsPlan(GridItemBankChoice choice) =>
            choice.Days == 0 && (choice.Kind == CTycoonBankRequest.LOAN || choice.Kind == CTycoonBankRequest.PLAN || choice.Kind == CTycoonBankRequest.SITE_LEASE);

        internal static string Describe(GridItemBankChoice c)
        {
            switch (c.Kind)
            {
                case CTycoonBankRequest.LOAN when c.Days == 0:
                    return $"Borrow {c.Amount} coins";
                case CTycoonBankRequest.LOAN:
                    int total = TycoonBank.Repayment(c.Amount, c.Days, TycoonBank.OnFirstLoan);
                    return $"Borrow {c.Amount}, repay {total} over {c.Days} days ({TycoonBank.DailyPaymentFor(total, c.Days)} a day, {TycoonBank.LoanRate(c.Days, TycoonBank.OnFirstLoan):P1})";
                case CTycoonBankRequest.PAYMENT:
                    return $"Pay {c.Amount} coins off the loan now";
                case CTycoonBankRequest.SITE_BUY:
                    return $"Buy the {SiteName()} site for {c.Amount} coins";
                case CTycoonBankRequest.SITE_LEASE when c.Days == 0:
                    return $"Lease the {SiteName()} site";
                case CTycoonBankRequest.SITE_LEASE:
                    int lease = TycoonBank.Repayment(c.Amount, c.Days);
                    return $"Lease the {SiteName()} site: {lease} over {c.Days} days ({TycoonBank.DailyPaymentFor(lease, c.Days)} a day, {TycoonBank.LoanRate(c.Days):P1})";
                case CTycoonBankRequest.PLAN when c.Days == 0:
                    return "Change the repayment plan";
                default:
                    int owed = TycoonBank.ReplanOutstanding(Standing.OwedAfterPending, Standing.DailyPayment, c.Days);
                    return $"New plan: {owed} over {c.Days} days ({TycoonBank.DailyPaymentFor(owed, c.Days)} a day)";
            }
        }

        internal static string SpeechFor(List<IGridItem> items)
        {
            if (_guestSpeech != null) return _guestSpeech;
            if (_site != null && _siteSpeech != null)
            {
                bool siteRoot = false, siteConfirm = false;
                foreach (IGridItem item in items) { if (item is GridItemDeskClose) siteRoot = true; if (item is GridItemBankConfirm) siteConfirm = true; }
                if (siteRoot && !siteConfirm) return _siteSpeech;
            }
            bool confirm = false, plans = false, newDebt = false, root = false;
            foreach (IGridItem item in items)
            {
                if (item is GridItemBankTip tip)
                {
                    List<string> pages = TycoonBankerDialogue.All("tutorial");
                    string text = tip.Page < pages.Count ? pages[tip.Page] : "";
                    return $"BANK TUTORIAL ({tip.Page + 1}/{pages.Count})\n{text}";
                }
                if (item is GridItemBankConfirm) confirm = true;
                else if (item is GridItemBankChoice c && c.Days > 0) { plans = true; newDebt |= c.Kind != CTycoonBankRequest.PLAN; }
                else if (item is GridItemDeskClose) root = true;
            }
            if (confirm)
                foreach (IGridItem item in items)
                    if (item is GridItemBankConfirm k) return k.Text + ". Deal?";
            if (plans) return "How many days do you want to repay over? Longer costs more."
                + (newDebt ? "\n" + (_graceLine ?? (_graceLine = TycoonBankerDialogue.Say("grace"))) : "");
            if (root)
            {
                TycoonCompany.EnsureLoaded();
                if (_greeting == null && !TycoonCompany.MetBanker && TycoonBankerDialogue.All("introduction").Count > 0)
                {
                    _greeting = TycoonBankerDialogue.Say("introduction");
                    TycoonCompany.MetBanker = true;
                    TycoonCompany.Save();
                }
                if (_greeting == null)
                    _greeting = Standing.HasLoan ? TycoonBankerDialogue.Say("greeting", "in-debt")
                        : Standing.Offer <= 0 ? TycoonBankerDialogue.Say("no-credit")
                        : TycoonBankerDialogue.Say("greeting", "no-loan");
                string standing = Standing.HasLoan
                    ? (Standing.GraceNights > 0
                        ? $"You currently owe {Standing.OwedAfterPending} coins. No payment for {Plural(Standing.GraceNights, "more night")}, then {Standing.DailyPayment} coins a day."
                        : $"You currently owe {Standing.OwedAfterPending} coins, which is {Standing.DailyPayment} coins a day for {Plural(Standing.DaysLeft, "more day")}.")
                    : Standing.Offer > 0 ? $"I can lend you {Standing.Offer}." + (TycoonBank.OnFirstLoan ? " " + TycoonBankerDialogue.Say("first-loan") : "")
                    : "I can't lend you anything right now.";
                return _greeting + "\n\n" + standing;
            }
            return "...";
        }

        private static string _greeting { get => DeskMenuContext.Get<string>("bank.greeting"); set => DeskMenuContext.Set("bank.greeting", value); }

        private static string _graceLine { get => DeskMenuContext.Get<string>("bank.grace"); set => DeskMenuContext.Set("bank.grace", value); }

        private static string Plural(int n, string word) => $"{n} {word}{(n == 1 ? "" : "s")}";

        private static string SiteName() => _site.HasValue ? TycoonSites.SettingName(_site.Value.Setting) : "restaurant";

        internal static void NewConversation() => _greeting = null;

        internal static string LabelFor(IGridItem item)
        {
            switch (item)
            {
                case GridItemBankTip tip: return tip.Page + 1 < TycoonBankerDialogue.All("tutorial").Count ? "Next" : "Got it";
                case GridItemDeskClose _: return "Leave";
                case GridItemDeskBack _: return "Back";
                case GridItemBankConfirm _: return "Yes, it's a deal";
                case GridItemBankChoice c when c.Kind == CTycoonBankRequest.LOAN && c.Days == 0: return $"Borrow {c.Amount} coins";
                case GridItemBankChoice c when c.Kind == CTycoonBankRequest.PAYMENT: return $"Pay {c.Amount} coins now";
                case GridItemBankChoice c when c.Kind == CTycoonBankRequest.SITE_BUY: return $"Buy it for {c.Amount} coins";
                case GridItemBankChoice c when c.Kind == CTycoonBankRequest.SITE_LEASE && c.Days == 0: return "Lease it (pay it off like a loan)";
                case GridItemBankChoice c when c.Kind == CTycoonBankRequest.SITE_LEASE:
                    int lease = TycoonBank.Repayment(c.Amount, c.Days);
                    return $"{c.Days} days: {TycoonBank.DailyPaymentFor(lease, c.Days)} a day, {lease} in total ({TycoonBank.LoanRate(c.Days):P1})";
                case GridItemBankChoice c when c.Kind == CTycoonBankRequest.PLAN && c.Days == 0: return "Change the repayment plan";
                case GridItemBankChoice c when c.Kind == CTycoonBankRequest.LOAN:
                    int total = TycoonBank.Repayment(c.Amount, c.Days, TycoonBank.OnFirstLoan);
                    return $"{c.Days} days: {TycoonBank.DailyPaymentFor(total, c.Days)} a day, {total} in total ({TycoonBank.LoanRate(c.Days, TycoonBank.OnFirstLoan):P1})";
                case GridItemBankChoice c:
                    int owed = TycoonBank.ReplanOutstanding(Standing.OwedAfterPending, Standing.DailyPayment, c.Days);
                    return $"{c.Days} days: {TycoonBank.DailyPaymentFor(owed, c.Days)} a day, {owed} in total";
                default: return "?";
            }
        }

        internal static string TitleFor(IGridItem item) => "";

        internal static string TitleLine(IGridItem item)
        {
            string standing = Standing.HasLoan
                ? $"Owed {Standing.OwedAfterPending}, {Standing.DailyPayment} a day (about {Standing.DaysLeft} days left)"
                : Standing.Offer > 0 ? $"The bank will lend you {Standing.Offer}" : "No credit right now";
            if (item is GridItemBankChoice choice) return Describe(choice) + (choice.Days == 0 ? "  -  " + standing : "");
            if (item is GridItemBankConfirm confirm) return "Confirm: " + confirm.Text;
            if (item is GridItemDeskClose) return TycoonBankerDialogue.Name + "  -  " + standing;
            return null;
        }

        private static GridMenuConfig Page(List<IGridItem> items)
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = items;
            return page;
        }

        private static Texture2D Icon(ref Texture2D cache, Color fill)
        {
            if ((object)cache != null) return cache;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            Color rim = fill * 0.6f;
            rim.a = 1f;
            Vector2 centre = new Vector2(size / 2f, size / 2f);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), centre);
                    texture.SetPixel(x, y, d > 48f ? clear : d > 40f ? rim : fill);
                }
            texture.Apply();
            cache = texture;
            return texture;
        }
    }
}
