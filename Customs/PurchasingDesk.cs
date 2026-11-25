using KitchenData;
using KitchenLib.Customs;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.Customs
{
#pragma warning disable CS0618
    public class PurchasingDesk : CustomAppliance
    {
        public static readonly int MODEL_SOURCE = KitchenLib.References.ApplianceReferences.BlueprintDiscountDesk;

        public override string UniqueNameID => "TycoonApplianceDesk";

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (Prefab == null && gameData.TryGet<Appliance>(MODEL_SOURCE, out Appliance vanillaDesk))
                Prefab = vanillaDesk.Prefab;

            Properties = new List<IApplianceProperty>
            {
                new CTycoonPurchasingDesk { DrawLocation = TycoonDesks.MenuDrawLocation(gameData) },
                new Kitchen.CTriggerPlayerSpecificUI(),
            };

            base.AttachDependentProperties(gameData, gameDataObject);
        }

        [System.Obsolete] public override string Name => "Purchasing Desk";
        [System.Obsolete] public override string Description => "Stand in front of it and interact to order appliances. They arrive as parcels the next morning.";

        public override bool IsPurchasable => false;
        public override PriceTier PriceTier => PriceTier.Free;
        public override int PurchaseCost => 0;
    }
#pragma warning restore CS0618

#pragma warning disable CS0618
    public class ManagementDesk : CustomAppliance
    {
        public static readonly int MODEL_SOURCE = KitchenLib.References.ApplianceReferences.BlueprintOrderingDesk;

        public override string UniqueNameID => "TycoonManagementDesk";

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (Prefab == null && gameData.TryGet<Appliance>(MODEL_SOURCE, out Appliance vanillaDesk))
                Prefab = vanillaDesk.Prefab;

            Properties = new List<IApplianceProperty>
            {
                new CTycoonManagementDesk { DrawLocation = TycoonDesks.MenuDrawLocation(gameData) },
                new Kitchen.CTriggerPlayerSpecificUI(),
            };

            base.AttachDependentProperties(gameData, gameDataObject);
        }

        [System.Obsolete] public override string Name => "Management Desk";
        [System.Obsolete] public override string Description => "During the prep phase, stand in front of it and interact to manage your restaurant's cards. Only the host can use it.";

        public override bool IsPurchasable => false;
        public override PriceTier PriceTier => PriceTier.Free;
        public override int PurchaseCost => 0;
    }
#pragma warning restore CS0618

#pragma warning disable CS0618
    public class OrderingCounter : CustomAppliance
    {
        public static readonly int MODEL_SOURCE = KitchenLib.References.ApplianceReferences.TableBar;

        public override string UniqueNameID => "TycoonOrderingCounter";

        private List<Appliance.ApplianceProcesses> _processes = new List<Appliance.ApplianceProcesses>();
        private EffectRepresentation _effectRepresentation;

        public override List<Appliance.ApplianceProcesses> Processes => _processes;
        public override EffectRepresentation EffectRepresentation => _effectRepresentation;

        public static Kitchen.Orientation CustomerSide { get; private set; } = Kitchen.Orientation.Down;

        public static Vector3 CustomerOffset(Kitchen.Orientation side)
        {
            switch (side)
            {
                case Kitchen.Orientation.Right: return Vector3.right;
                case Kitchen.Orientation.Left: return Vector3.left;
                case Kitchen.Orientation.Up: return Vector3.forward;
                default: return Vector3.back;
            }
        }

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (gameData.TryGet<Appliance>(MODEL_SOURCE, out Appliance barTable))
            {
                var properties = new List<IApplianceProperty>();
                foreach (IApplianceProperty property in barTable.Properties)
                {
                    if (property is Kitchen.CApplianceTable table)
                    {
                        CustomerSide = PickCustomerSide(table);
                        table.PreventSittingUp = CustomerSide != Kitchen.Orientation.Up;
                        table.PreventSittingDown = CustomerSide != Kitchen.Orientation.Down;
                        table.PreventSittingLeft = CustomerSide != Kitchen.Orientation.Left;
                        table.PreventSittingRight = CustomerSide != Kitchen.Orientation.Right;
                        properties.Add(table);
                    }
                    else properties.Add(property);
                }
                properties.Add(new CTycoonOrderingCounter());
                properties.Add(new Kitchen.CApplianceRequiresPathable());
                Properties = properties;
                _processes = barTable.Processes ?? _processes;
                _effectRepresentation = barTable.EffectRepresentation;

                if (Prefab == null)
                    Prefab = OrderingCounterPrefab.Build(gameData, barTable.Prefab, CustomerOffset(CustomerSide)) ?? barTable.Prefab;
            }
            base.AttachDependentProperties(gameData, gameDataObject);
        }

        private static Kitchen.Orientation PickCustomerSide(Kitchen.CApplianceTable vanilla)
        {
            var candidates = new List<Kitchen.Orientation>();
            if (!vanilla.PreventSittingUp) candidates.Add(Kitchen.Orientation.Down);
            if (!vanilla.PreventSittingDown) candidates.Add(Kitchen.Orientation.Up);
            if (!vanilla.PreventSittingLeft) candidates.Add(Kitchen.Orientation.Right);
            if (!vanilla.PreventSittingRight) candidates.Add(Kitchen.Orientation.Left);
            Kitchen.Orientation side = candidates.Count > 0 ? candidates[0] : Kitchen.Orientation.Down;
            Mod.LogInfo($"Ordering Counter: vanilla bar seats on up={!vanilla.PreventSittingUp} down={!vanilla.PreventSittingDown} left={!vanilla.PreventSittingLeft} right={!vanilla.PreventSittingRight}; customers stand on the {side} side.");
            return side;
        }

        [System.Obsolete] public override string Name => "Ordering Counter";
        [System.Obsolete] public override string Description => "A walk-up counter. With the Take-Out card on, customers order the moment they reach it, grab their order and go.";

        public override bool IsPurchasable => true;
        public override PriceTier PriceTier => PriceTier.Cheap;
        public override int PurchaseCost => 20;
        public override ShoppingTags ShoppingTags => ShoppingTags.FrontOfHouse;
        public override RarityTier RarityTier => RarityTier.Common;
    }
#pragma warning restore CS0618

    /// <summary>Shared helper for the Tycoon desks.</summary>
    public static class TycoonDesks
    {
        public static Vector3 MenuDrawLocation(GameData gameData)
        {
            Vector3 drawLocation = Vector3.zero;
            foreach (Appliance appliance in gameData.Get<Appliance>())
            {
                foreach (IApplianceProperty property in appliance.Properties)
                {
                    if (property is Kitchen.CDishSourceCabinet cabinet)
                        drawLocation = cabinet.DrawLocation;
                }
            }
            return drawLocation;
        }
    }
}
