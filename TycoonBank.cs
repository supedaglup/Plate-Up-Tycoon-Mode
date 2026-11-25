using Kitchen;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>The bank: loans are how a restaurant buys its equipment.</summary>
    public static class TycoonBank
    {
        public const int STARTER_CREDIT = 300;
        public const int LOAN_BASE_BONUS = 200;
        public const float FIRST_LOAN_RATE_FACTOR = 0.5f;
        public const int GRACE_DAYS = 3;
        private static int _graceFromHost;

        public static int GraceNights
        {
            get
            {
                if (!TycoonNetData.IsHost) return _graceFromHost;
                TycoonCompany.EnsureLoaded();
                return TycoonCompany.Outstanding > 0 ? TycoonCompany.GraceNights : 0;
            }
        }
        private static bool _firstLoanFromHost, _debtDiscountedFromHost;

        public static bool OnFirstLoan
        {
            get
            {
                if (!TycoonNetData.IsHost) return _firstLoanFromHost;
                TycoonCompany.EnsureLoaded();
                return TycoonCompany.PersonalLoans == 0;
            }
        }

        public static bool DebtDiscounted
        {
            get
            {
                if (!TycoonNetData.IsHost) return _debtDiscountedFromHost;
                TycoonCompany.EnsureLoaded();
                return TycoonCompany.DiscountedDebt && TycoonCompany.Outstanding > 0;
            }
        }
        public const int CREDIT_PER_STAR = 300;
        public const float CREDIT_PER_REPUTATION = 0.14f;
        public const int LOAN_ROUNDING = 10;

        public const int MAX_TERM = 30;
        public static readonly int[] GENTLE_TERMS = { 10, 15, 20, 25, 30 };
        public static readonly int[] TOUGH_TERMS = { 5, 10, 15, 20, 30 };
        public const float GENTLE_BASE_RATE = 0.03f, TOUGH_BASE_RATE = 0.06f;
        public const float GENTLE_RATE_PER_DAY = 0.0015f, TOUGH_RATE_PER_DAY = 0.004f;
        public const int FULL_TERMS_CREDIT = 3000;

        public static float Tier { get; private set; }

        public static float TierFor(int credit) => Mathf.Clamp01((credit - STARTER_CREDIT) / (float)(FULL_TERMS_CREDIT - STARTER_CREDIT));

        public static int[] TERMS
        {
            get
            {
                var terms = new List<int>();
                for (int i = 0; i < GENTLE_TERMS.Length; i++)
                {
                    int days = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(GENTLE_TERMS[i], TOUGH_TERMS[i], Tier) / 5f) * 5, 5, MAX_TERM);
                    if (!terms.Contains(days)) terms.Add(days);
                }
                return terms.ToArray();
            }
        }

        public static readonly int[] PAYMENTS = { 25, 50, 100, 250, 500, 1000, 2500 };

        public struct Standing
        {
            public int Credit;
            public int Owed;
            public int DailyPayment;
            public int Cash;
            public int Restaurants;
            public int PendingLoan;
            public int PendingPayments;
            public bool PendingPlan;
            public int Stars;
            public int Reputation;
            public float GrossReputation;
            public int SitesBought;

            public int OwedAfterPending => Mathf.Max(0, Owed - PendingPayments);
            public int CashAfterPending => Mathf.Max(0, Cash - PendingPayments);
            public bool HasLoan => Owed > 0 || PendingLoan > 0;
            public int Offer => HasLoan ? 0 : (Credit + LOAN_BASE_BONUS) / LOAN_ROUNDING * LOAN_ROUNDING;
            public int DaysLeft => DailyPayment > 0 ? Mathf.CeilToInt(OwedAfterPending / (float)DailyPayment) + GraceNights : 0;
            public int GraceNights;
        }

        public static float RateFor(int days, bool firstLoan = false) =>
            (Mathf.Lerp(GENTLE_BASE_RATE, TOUGH_BASE_RATE, Tier) + Mathf.Lerp(GENTLE_RATE_PER_DAY, TOUGH_RATE_PER_DAY, Tier) * Mathf.Max(1, days))
            * (firstLoan ? FIRST_LOAN_RATE_FACTOR : 1f);
        public static float LoanRate(int days, bool firstLoan = false) => RateFor(Mathf.Max(1, days) + GRACE_DAYS, firstLoan);
        public static int Repayment(int amount, int days, bool firstLoan = false) => Mathf.CeilToInt(amount * (1f + LoanRate(days, firstLoan)));
        public static int DailyPaymentFor(int outstanding, int days) => Mathf.Max(1, Mathf.CeilToInt(outstanding / (float)Mathf.Max(1, days)));

        public static int ReplanOutstanding(int outstanding, int dailyPayment, int days)
        {
            int daysLeft = dailyPayment > 0 ? Mathf.CeilToInt(outstanding / (float)dailyPayment) : days;
            if (days <= daysLeft) return outstanding;
            return outstanding + Mathf.CeilToInt(outstanding * (RateFor(days, DebtDiscounted) - RateFor(daysLeft, DebtDiscounted)));
        }

        public static int CreditFor(int stars, float reputation) =>
            CREDIT_PER_STAR * (TycoonEconomy.ClampStars(stars) + 1) + Mathf.RoundToInt(Mathf.Max(0f, reputation) * CREDIT_PER_REPUTATION);

        public static Standing Read(EntityManager hq)
        {
            if (!TycoonNetData.IsHost) return FromHost();
            var standing = new Standing();
            try
            {
                foreach ((World world, SaveState state) in Persistence.FullWorld.All())
                {
                    if (state != SaveState.Loaded || world == null) continue;
                    EntityManager em = world.EntityManager;
                    standing.Restaurants++;
                    int stars = TycoonEconomy.MIN_STARS;
                    float reputation = 0f;
                    using (EntityQuery q = em.CreateEntityQuery(typeof(STycoonEconomy)))
                        if (!q.IsEmpty) stars = q.GetSingleton<STycoonEconomy>().Stars;
                    using (EntityQuery q = em.CreateEntityQuery(typeof(STycoonReputation)))
                        if (!q.IsEmpty) reputation = q.GetSingleton<STycoonReputation>().Reputation;
                    standing.Credit += CreditFor(stars, reputation);
                    standing.Stars += TycoonEconomy.ClampStars(stars);
                    standing.Reputation += Mathf.RoundToInt(Mathf.Max(0f, reputation));
                    standing.GrossReputation += reputation;
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Bank: could not read the saved restaurants: " + e.Message);
            }
            if (standing.Restaurants == 0) standing.Credit = STARTER_CREDIT;

            TycoonCompany.EnsureLoaded();
            standing.Cash = TycoonCompany.Cash;
            standing.Owed = TycoonCompany.Outstanding;
            standing.DailyPayment = TycoonCompany.DailyPayment;
            standing.GraceNights = GraceNights;

            using (EntityQuery pending = hq.CreateEntityQuery(typeof(CTycoonBankRequest)))
            using (NativeArray<CTycoonBankRequest> requests = pending.ToComponentDataArray<CTycoonBankRequest>(Allocator.Temp))
                for (int i = 0; i < requests.Length; i++)
                {
                    if (requests[i].Kind == CTycoonBankRequest.LOAN) standing.PendingLoan += requests[i].Amount;
                    else if (requests[i].Kind == CTycoonBankRequest.PAYMENT) standing.PendingPayments += requests[i].Amount;
                    else if (requests[i].Kind == CTycoonBankRequest.PLAN) standing.PendingPlan = true;
                }
            standing.SitesBought = TycoonCompany.SitesBought.Count;
            Tier = TierFor(standing.Credit);
            TycoonNetData.Set("co", $"{standing.Cash},{standing.Credit},{standing.Owed},{standing.DailyPayment},{standing.Restaurants},{standing.SitesBought},{standing.Stars},{standing.Reputation},{(standing.PendingPlan ? 1 : 0)},{(OnFirstLoan ? 1 : 0)},{(DebtDiscounted ? 1 : 0)},{standing.GraceNights}");
            return standing;
        }

        public static Standing FromHost()
        {
            var s = new Standing();
            int[] v = TycoonNetData.Ints("co");
            if (v == null || v.Length < 9) { s.Credit = STARTER_CREDIT; Tier = 0f; return s; }
            s.Cash = v[0]; s.Credit = v[1]; s.Owed = v[2]; s.DailyPayment = v[3]; s.Restaurants = v[4];
            s.SitesBought = v[5]; s.Stars = v[6]; s.Reputation = v[7]; s.PendingPlan = v[8] != 0;
            _firstLoanFromHost = v.Length > 9 && v[9] != 0;
            _debtDiscountedFromHost = v.Length > 10 && v[10] != 0;
            s.GraceNights = _graceFromHost = v.Length > 11 ? Mathf.Max(0, v[11]) : 0;
            Tier = TierFor(s.Credit);
            return s;
        }

        public static List<int> Payments(Standing standing)
        {
            var list = new List<int>();
            int most = Mathf.Min(standing.CashAfterPending, standing.OwedAfterPending);
            foreach (int p in PAYMENTS) if (p < most) list.Add(p);
            if (most > 0) list.Add(most);
            return list;
        }

        public static string Apply(int kind, int days, int amount)
        {
            TycoonCompany.EnsureLoaded();
            string done = null;
            if (kind == CTycoonBankRequest.LOAN && amount > 0 && TycoonCompany.Outstanding <= 0)
            {
                days = Mathf.Max(1, days);
                TycoonCompany.Cash += amount;
                bool first = OnFirstLoan;
                TycoonCompany.Outstanding = Repayment(amount, days, first);
                TycoonCompany.DailyPayment = DailyPaymentFor(TycoonCompany.Outstanding, days);
                TycoonCompany.TotalBorrowed += amount;
                TycoonCompany.LoansTaken++;
                TycoonCompany.PersonalLoans++;
                TycoonCompany.DiscountedDebt = first;
                TycoonCompany.GraceNights = GRACE_DAYS;
                done = $"loan of {amount} over {days} days (first payment after {GRACE_DAYS} closings) at {LoanRate(days, first):P1}{(first ? " (first loan: half interest)" : "")}: {TycoonCompany.Outstanding} owed, {TycoonCompany.DailyPayment} a day; wallet {TycoonCompany.Cash}.";
            }
            else if (kind == CTycoonBankRequest.PAYMENT && TycoonCompany.Outstanding > 0)
            {
                int paid = Mathf.Max(0, Mathf.Min(amount, Mathf.Min(TycoonCompany.Cash, TycoonCompany.Outstanding)));
                if (paid > 0)
                {
                    TycoonCompany.Cash -= paid;
                    TycoonCompany.Outstanding -= paid;
                    if (TycoonCompany.Outstanding == 0) TycoonCompany.DailyPayment = TycoonCompany.GraceNights = 0;
                    done = $"paid {paid} off the loan; {TycoonCompany.Outstanding} still owed, wallet {TycoonCompany.Cash}.";
                }
            }
            else if (kind == CTycoonBankRequest.PLAN && TycoonCompany.Outstanding > 0)
            {
                days = Mathf.Max(1, days);
                int before = TycoonCompany.Outstanding;
                TycoonCompany.Outstanding = ReplanOutstanding(TycoonCompany.Outstanding, TycoonCompany.DailyPayment, days);
                TycoonCompany.DailyPayment = DailyPaymentFor(TycoonCompany.Outstanding, days);
                done = $"new plan of {days} days: owed {before} -> {TycoonCompany.Outstanding}, {TycoonCompany.DailyPayment} a day.";
            }
            if (done != null) TycoonCompany.Save();
            return done;
        }

        public static int Encode(int kind, int days, int amount) =>
            ((kind * 100) + Mathf.Clamp(days, 0, 99)) * 1000000 + Mathf.Clamp(amount, 0, 999999);

        public static void Decode(int value, out int kind, out int days, out int amount)
        {
            amount = value % 1000000;
            int head = value / 1000000;
            days = head % 100;
            kind = head / 100;
        }
    }
}
