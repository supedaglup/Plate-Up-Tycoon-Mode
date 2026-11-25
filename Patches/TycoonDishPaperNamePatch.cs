using HarmonyLib;
using Kitchen;
using KitchenData;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>Names the recipe papers of base dishes that have no name in the game data.</summary>
    [HarmonyPatch(typeof(DishChoiceView), "UpdateData")]
    public static class TycoonDishPaperNamePatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishChoiceView __instance, object[] __args)
        {
            try
            {
                object data = __args[0];
                int id = (int)AccessTools.Field(data.GetType(), "Dish").GetValue(data);
                if (id == 0 || !GameData.Main.TryGet<Dish>(id, out Dish dish, false)) return;
                if (!(AccessTools.Field(typeof(DishChoiceView), "Label").GetValue(__instance) is Component label) || label == null) return;
                System.Reflection.PropertyInfo text = label.GetType().GetProperty("text");
                string shown = text?.GetValue(label) as string;
                if (!string.IsNullOrEmpty(shown) && shown != "?") return;
                text?.SetValue(label, TycoonRecipeShop.DisplayName(dish));
            }
            catch (System.Exception) { }
        }
    }
}
