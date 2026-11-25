using Kitchen;
using Kitchen.Layouts;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>HQ: the recipe shop's tutorial, the bank tutorial's recipe popup.</summary>
    public class TycoonChefTutorialSystem : FranchiseSystem, IModSystem
    {
        public const float TUTORIAL_DEPTH = 2f;

        private EntityQuery _players;
        private EntityQuery _desks;
        private EntityQuery _recipePopups;
        private int _nextPage = -1;

        protected override void Initialise()
        {
            base.Initialise();
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CPosition)));
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonChefDesk)));
            _recipePopups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _desks.IsEmpty || TycoonRecipeShopSetupSystem.ShopRoom == TycoonRecipeShopSetupSystem.NO_ROOM) return;

            if (_nextPage >= 0)
            {
                if (!_recipePopups.IsEmpty) return;
                if (_nextPage >= Customs.ChefTutorialDish.PageCount || _nextPage >= Mod.ChefTutorialPages.Count) { _nextPage = -1; return; }
                PopupUtilities.RequestManagedPopup(KitchenData.PopupType.Recipe, new CPopupRecipe { ID = Mod.ChefTutorialPages[_nextPage] });
                _nextPage++;
                return;
            }

            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.ChefTutorialSeen || !TycoonPreferences.ShowCharacterTutorials || Mod.ChefTutorialPages.Count == 0 || !_recipePopups.IsEmpty) return;

            using NativeArray<CPosition> positions = _players.ToComponentDataArray<CPosition>(Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i].Position;
                CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(p.x), 0, Mathf.RoundToInt(p.z)));
                if (tile.Type == RoomType.NoRoom || tile.RoomID != TycoonRecipeShopSetupSystem.ShopRoom) continue;
                Vector3 fromDoor = p - TycoonRecipeShopSetupSystem.ShopDoor;
                fromDoor.y = 0f;
                if (fromDoor.magnitude < TUTORIAL_DEPTH) continue;

                TycoonCompany.ChefTutorialSeen = true;
                TycoonCompany.Save();
                _nextPage = 0;
                Mod.LogInfo($"Recipe shop: a player walked into the shop for the first time; showing its tutorial ({Customs.ChefTutorialDish.PageCount} page(s)).");
                return;
            }
        }
    }
}
