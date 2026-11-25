using KitchenData;
using KitchenLib.Customs;
using PlateUpTycoon.Components;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
#pragma warning disable CS0618
    public class BankDesk : CustomAppliance
    {
        public static readonly int MODEL_SOURCE = KitchenLib.References.ApplianceReferences.BlueprintOrderingDesk;

        public override string UniqueNameID => "TycoonBankDesk";

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (Prefab == null && gameData.TryGet<Appliance>(MODEL_SOURCE, out Appliance vanillaDesk))
                Prefab = vanillaDesk.Prefab;

            Properties = new List<IApplianceProperty>
            {
                new CTycoonBankDesk { DrawLocation = TycoonDesks.MenuDrawLocation(gameData) },
                new Kitchen.CTriggerPlayerSpecificUI(),
            };

            base.AttachDependentProperties(gameData, gameDataObject);
        }

        [System.Obsolete] public override string Name => "Bank";
        [System.Obsolete] public override string Description => "Speak to the banker about a loan.";

        public override bool IsPurchasable => false;
        public override PriceTier PriceTier => PriceTier.Free;
        public override int PurchaseCost => 0;
    }
#pragma warning restore CS0618
}
