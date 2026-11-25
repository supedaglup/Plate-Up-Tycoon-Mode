using Kitchen;
using KitchenData;
using KitchenLib.Customs;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.Customs
{
#pragma warning disable CS0618
    public class TakeOutPlates : CustomAppliance
    {
        public static int MODEL_SOURCE => KitchenLib.References.ApplianceReferences.PlateStack;
        public override string UniqueNameID => "TycoonTakeOutPlates";

        private List<Appliance.ApplianceProcesses> _processes = new List<Appliance.ApplianceProcesses>();
        public override List<Appliance.ApplianceProcesses> Processes => _processes;

        public override void AttachDependentProperties(GameData gameData, GameDataObject gameDataObject)
        {
            if (gameData.TryGet<Appliance>(MODEL_SOURCE, out Appliance plates))
            {
                var properties = new List<IApplianceProperty>();
                int plate = KitchenLib.References.ItemReferences.Plate;
                foreach (IApplianceProperty property in plates.Properties)
                {
                    if (property is CItemProvider provider)
                    {
                        Mod.LogInfo($"Take-Out Plates: provider from the Plates: available {provider.Available}, maximum {provider.Maximum}.");
                        properties.Add(provider);
                    }
                    else properties.Add(property);
                }
                properties.Add(new CTycoonTakeOutPlateRack());
                Properties = properties;
                _processes = plates.Processes ?? _processes;
                if (Prefab == null && plates.Prefab != null) Prefab = TakeOutPlateLook.TintedCopy(gameData, plates.Prefab, "Tycoon Take-Out Plates");
            }
            else Mod.LogWarning("Take-Out Plates: the game's Plates appliance was not found.");
            base.AttachDependentProperties(gameData, gameDataObject);
        }

        [System.Obsolete] public override string Name => "Disposable Plates";
        [System.Obsolete] public override string Description => "Light-brown plates that never run out. No washing up, but each one is paid for at closing and customers think a little less of food served on them.";

        public override bool IsPurchasable => true;
        public override PriceTier PriceTier => PriceTier.Cheap;
        public override int PurchaseCost => 40;
        public override ShoppingTags ShoppingTags => ShoppingTags.Basic;
        public override RarityTier RarityTier => RarityTier.Common;
    }
#pragma warning restore CS0618

    /// <summary>The take-out look: the plate's own materials swapped for a light-brown flat material.</summary>
    public static class TakeOutPlateLook
    {
        public static readonly Color COLOUR = new Color(0.80f, 0.64f, 0.46f);
        private static HashSet<Material> _plateMaterials;
        private static Material _brown;
        private static GameObject _holder;

        public static Material Brown => _brown != null ? _brown : (_brown = KitchenLib.Utils.MaterialUtils.CreateFlat("Tycoon Take-Out Plate", COLOUR, 0.1f, 0f));

        public static HashSet<Material> PlateMaterials(GameData gameData)
        {
            if (_plateMaterials != null) return _plateMaterials;
            var set = new HashSet<Material>();
            if (gameData != null && gameData.TryGet<Item>(KitchenLib.References.ItemReferences.Plate, out Item plate) && plate.Prefab != null)
                foreach (Renderer r in plate.Prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials) if (m != null) set.Add(m);
            if (set.Count > 0) _plateMaterials = set;
            Mod.LogInfo($"Take-Out Plates: the plate model uses {set.Count} material(s): {string.Join(", ", System.Linq.Enumerable.Select(set, m => m.name))}.");
            return set;
        }

        public static int Tint(GameObject root, GameData gameData)
        {
            HashSet<Material> plate = PlateMaterials(gameData);
            if (root == null || plate.Count == 0) return 0;
            int changed = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool any = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && plate.Contains(mats[i])) { mats[i] = Brown; any = true; changed++; }
                if (any) r.sharedMaterials = mats;
            }
            return changed;
        }

        public static GameObject TintedCopy(GameData gameData, GameObject source, string name)
        {
            if (_holder == null)
            {
                _holder = new GameObject("Tycoon take-out prefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(source, _holder.transform);
            copy.name = name;
            int changed = Tint(copy, gameData);
            Mod.LogInfo($"Take-Out Plates: rack model made ({changed} plate material slot(s) tinted).");
            return copy;
        }
    }
}
