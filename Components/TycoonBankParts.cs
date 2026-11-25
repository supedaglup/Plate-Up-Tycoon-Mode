using KitchenData;
using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Singleton: what this restaurant owes the bank.</summary>
    public struct STycoonLoan : IComponentData, IModComponent
    {
        public int Outstanding;
        public int DailyPayment;
        public int TotalBorrowed;
        public int LoansTaken;
    }

    /// <summary>Unused; kept so older saves still load.</summary>
    public struct CTycoonLoanRequest : IComponentData, IModComponent
    {
        public int Amount;
        public bool Repay;
    }

    /// <summary>An agreement made at the HQ bank, settled in the restaurant by TycoonLoanSystem.</summary>
    public struct CTycoonBankRequest : IComponentData, IModComponent
    {
        public const int SITE_BUY = 4, SITE_LEASE = 5;
        public const int LOAN = 1, PAYMENT = 2, PLAN = 3;
        public int Kind;
        public int Amount;
        public int Days;
    }

    /// <summary>Appliance property: the bank counter in the HQ. Same menu plumbing as the desks.</summary>
    public struct CTycoonBankDesk : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent, IPlayerSpecificUISource
    {
        public UnityEngine.Vector3 DrawLocation;

        UnityEngine.Vector3 IPlayerSpecificUISource.DrawLocation => DrawLocation;
    }

    /// <summary>Tag on the banker.</summary>
    public struct CTycoonBanker : IComponentData, IModComponent { }

    /// <summary>Singleton while the till is linked to the company wallet.</summary>
    public struct STycoonCompanyLinked : IComponentData, IModComponent { }

    /// <summary>The coin counter shown in the HQ. Never saved.</summary>
    public struct CTycoonHqMoney : IComponentData, IModComponent { }
}
