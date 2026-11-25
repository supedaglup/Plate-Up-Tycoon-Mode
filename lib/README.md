# lib

Game/modding DLLs the project compiles against. They are not included in the public repository: copy them
here from your own PlateUp! install before building.

From `PlateUp\PlateUp_Data\Managed\`:
- Kitchen.Common.dll, Kitchen.GameData.dll (also holds the `KitchenData` namespace),
  Kitchen.RestaurantMode.dll, KitchenMode.dll, KitchenMods.dll
- Unity.Entities.dll, Unity.Collections.dll, Unity.Mathematics.dll
- UnityEngine.CoreModule.dll (PlateUp ships no single UnityEngine.dll; add other UnityEngine.*Module.dll files only if needed)

From the KitchenLib Steam Workshop folder:
- KitchenLib-Workshop.dll
