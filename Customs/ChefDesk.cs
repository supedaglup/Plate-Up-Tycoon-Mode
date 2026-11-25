using KitchenData;
using KitchenLib.Customs;
using PlateUpTycoon.Components;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
#pragma warning disable CS0618
    public class ChefDesk : CustomAppliance
    {
        public static readonly int MODEL_SOURCE = KitchenLib.References.ApplianceReferences.BlueprintOrderingDesk;

        public override string UniqueNameID => "TycoonChefDesk";

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (Prefab == null && gameData.TryGet<Appliance>(MODEL_SOURCE, out Appliance vanillaDesk))
                Prefab = vanillaDesk.Prefab;

            Properties = new List<IApplianceProperty>
            {
                new CTycoonChefDesk { DrawLocation = TycoonDesks.MenuDrawLocation(gameData) },
                new Kitchen.CTriggerPlayerSpecificUI(),
            };

            base.AttachDependentProperties(gameData, gameDataObject);
        }

        [System.Obsolete] public override string Name => "Recipe Shop";
        [System.Obsolete] public override string Description => "Bring Smokey a recipe from the shelves to buy it.";

        public override bool IsPurchasable => false;
        public override PriceTier PriceTier => PriceTier.Free;
        public override int PurchaseCost => 0;
    }
#pragma warning restore CS0618
}
