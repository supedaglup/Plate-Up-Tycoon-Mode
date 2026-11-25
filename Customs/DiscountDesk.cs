using KitchenData;
using KitchenLib.Customs;
using PlateUpTycoon.Components;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
#pragma warning disable CS0618
    public class DiscountDesk : CustomAppliance
    {
        public static readonly int MODEL_SOURCE = KitchenLib.References.ApplianceReferences.BlueprintOrderingDesk;

        public override string UniqueNameID => "TycoonDiscountDesk";

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (Prefab == null && gameData.TryGet<Appliance>(MODEL_SOURCE, out Appliance vanillaDesk))
                Prefab = vanillaDesk.Prefab;

            Properties = new List<IApplianceProperty>
            {
                new CTycoonDiscountDesk { DrawLocation = TycoonDesks.MenuDrawLocation(gameData) },
                new Kitchen.CTriggerPlayerSpecificUI(),
            };

            base.AttachDependentProperties(gameData, gameDataObject);
        }

        [System.Obsolete] public override string Name => "Discount Appliances";
        [System.Obsolete] public override string Description => "Bring Whiskers an appliance box from her stands to buy it.";

        public override bool IsPurchasable => false;
        public override PriceTier PriceTier => PriceTier.Free;
        public override int PurchaseCost => 0;
    }
#pragma warning restore CS0618
}
