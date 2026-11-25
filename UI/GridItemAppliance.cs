using Kitchen;
using Kitchen.Modules;
using KitchenData;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>One appliance tile in the Purchasing Desk menu.</summary>
    public struct GridItemAppliance : IGridItem
    {
        public Appliance Appliance;
        public int Cost;

        public int SnapshotKey => Appliance.ID;

        public Texture2D GetSnapshot() => PrefabSnapshot.GetSnapshot(Appliance.Prefab, false);
    }

    /// <summary>A wallpaper or flooring tile.</summary>
    public struct GridItemDecor : IGridItem
    {
        public int DecorID;
        public string Name;
        public int Cost;
        public float ReputationPerDay;

        public int SnapshotKey => DecorID;

        public Texture2D GetSnapshot()
        {
            if (!GameData.Main.TryGet<Decor>(DecorID, out Decor decor, false)) return null;
            if (decor.Material != null && decor.Material.mainTexture is Texture2D texture) return texture;
            return decor.ApplicatorAppliance != null ? PrefabSnapshot.GetSnapshot(decor.ApplicatorAppliance.Prefab, false) : null;
        }
    }

    /// <summary>The green tick on a wallpaper/flooring confirmation page.</summary>
    public struct GridItemDecorConfirm : IGridItem
    {
        public int DecorID;
        public string Name;
        public int Cost;
        public Texture2D Icon;

        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>The "back to categories" tile, first in every category page.</summary>
    public struct GridItemDeskBack : IGridItem
    {
        public string Title;

        public int SnapshotKey => 0;

        public Texture2D GetSnapshot() => null;
    }

    /// <summary>A folder tile: opens another page of the menu.</summary>
    public struct GridItemDeskFolder : IGridItem
    {
        public string Name;
        public GridMenuConfig Page;
        public Appliance Icon;
        public Texture2D IconTexture;

        public int SnapshotKey => (object)Icon == null ? 0 : Icon.ID;

        public Texture2D GetSnapshot() => (object)Icon == null ? IconTexture : PrefabSnapshot.GetSnapshot(Icon.Prefab, false);
    }

    /// <summary>The green tick on a purchase confirmation page: buys the appliance.</summary>
    public struct GridItemDeskConfirm : IGridItem
    {
        public Appliance Appliance;
        public int Cost;
        public Texture2D Icon;

        public int SnapshotKey => 0;

        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>One card on the Management Desk's card manager.</summary>
    public struct GridItemDeskCard : IGridItem
    {
        public int UnlockID;

        public int SnapshotKey => 0;

        public Texture2D GetSnapshot() => null;
    }

    /// <summary>The red X tile on the main page: closes the menu.</summary>
    public struct GridItemDeskClose : IGridItem
    {
        public Texture2D Icon;

        public int SnapshotKey => 0;

        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>A bank tile: the loan, an extra payment, or a plan change, with its repayment days once chosen.</summary>
    public struct GridItemBankChoice : IGridItem
    {
        public int Kind;
        public int Amount;
        public int Days;
        public Texture2D Icon;

        public int SnapshotKey => 0;

        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>The green tick on a bank confirmation page: agrees the choice.</summary>
    public struct GridItemBankConfirm : IGridItem
    {
        public int Value;
        public string Text;
        public Texture2D Icon;

        public int SnapshotKey => 0;

        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>"Next" / "Got it" on a bank tutorial page.</summary>
    public struct GridItemBankTip : IGridItem
    {
        public int Page;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => null;
    }

    /// <summary>What the bank menu reports.</summary>
    public class TycoonBankChoice
    {
        public int Value;
    }

    /// <summary>Smokey's "buy this recipe" choice.</summary>
    public struct GridItemChefBuy : IGridItem
    {
        public int Dish;
        public int Price;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => null;
    }

    /// <summary>"Yes, I'll take it" on Smokey's confirmation page.</summary>
    public struct GridItemChefConfirm : IGridItem
    {
        public int Dish;
        public int Price;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => null;
    }

    /// <summary>What Smokey's menu reports: the recipe bought.</summary>
    public class TycoonChefChoice
    {
        public int Dish;
    }

    /// <summary>Chubs's "buy this card" choice.</summary>
    public struct GridItemCardBuy : IGridItem
    {
        public int Card;
        public int Price;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => null;
    }

    /// <summary>"Yes, deal" on Chubs's confirmation page.</summary>
    public struct GridItemCardConfirm : IGridItem
    {
        public int Card;
        public int Price;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => null;
    }

    /// <summary>What Chubs's menu reports: the card bought.</summary>
    public class TycoonCardChoice
    {
        public int Card;
    }

    /// <summary>What the close tile reports.</summary>
    public class TycoonDeskClose { }

    /// <summary>What the menu reports when a tile is picked.</summary>
    public class TycoonApplianceChoice
    {
        public int ApplianceID;
    }
}
