using HarmonyLib;
using Kitchen;
using Kitchen.Layouts;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Restaurant: the till is the company wallet.</summary>
    public class TycoonCompanyMoneySystem : RestaurantSystem, IModSystem
    {
        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SMoney>();
            RequireSingletonForUpdate<STycoonStartingMoneyGiven>();
        }

        protected override void OnUpdate()
        {
            if (HasSingleton<SGameOver>()) return;
            TycoonCompany.EnsureLoaded();
            SMoney money = GetSingleton<SMoney>();

            if (!HasSingleton<STycoonCompanyLinked>())
            {
                if (HasSingleton<STycoonLoan>())
                {
                    STycoonLoan old = GetSingleton<STycoonLoan>();
                    if (old.Outstanding > 0 && TycoonCompany.Outstanding == 0)
                    {
                        TycoonCompany.Outstanding = old.Outstanding;
                        TycoonCompany.DailyPayment = old.DailyPayment;
                        Mod.LogInfo($"Company: the restaurant's loan ({old.Outstanding} owed) moved to the company.");
                    }
                    Set(new STycoonLoan());
                }
                Entity tag = EntityManager.CreateEntity(typeof(STycoonCompanyLinked), typeof(CDoNotPersist));
                money.Amount = TycoonCompany.Cash;
                Set(money);
                TycoonCompany.Save();
                Mod.LogInfo($"Company: the till is linked to the company wallet ({money.Amount} coins).");
                return;
            }

            if (money.Amount != TycoonCompany.Cash)
            {
                TycoonCompany.Cash = money.Amount;
                TycoonCompany.MarkDirty();
            }
            TycoonCompany.SaveIfDirty();
        }
    }

    /// <summary>HQ: the coin counter, showing the company wallet.</summary>
    public class TycoonHqMoneySystem : FranchiseSystem, IModSystem
    {
        private const ViewType MONEY_VIEW = (ViewType)8;

        private EntityQuery _displays;
        private int _shown = int.MinValue;
        private float _nextCheck;

        protected override void Initialise()
        {
            base.Initialise();
            _displays = GetEntityQuery(new QueryHelper().All(typeof(CTycoonHqMoney)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive) return;
            TycoonCompany.EnsureLoaded();
            if (_displays.IsEmpty)
            {
                Entity e = EntityManager.CreateEntity(typeof(CTycoonHqMoney), typeof(CMoneyDisplay), typeof(CPosition), typeof(CRequiresView), typeof(CDoNotPersist));
                EntityManager.SetComponentData(e, new CPosition(new Vector3(0f, 1f, 0f)));
                EntityManager.SetComponentData(e, new CRequiresView { Type = MONEY_VIEW, ViewMode = ViewMode.Screen });
                _shown = int.MinValue;
            }
            if (TycoonCompany.Cash == _shown && Time.RealTotalTime < _nextCheck) return;
            _nextCheck = Time.RealTotalTime + 1f;
            if (Show(TycoonCompany.Cash)) _shown = TycoonCompany.Cash;
        }

        private static readonly System.Type ViewDataType = AccessTools.Inner(typeof(MoneyDisplayView), "ViewData");
        private static readonly System.Reflection.MethodInfo UpdateData =
            AccessTools.Method(typeof(MoneyDisplayView), "UpdateData", new[] { ViewDataType });

        private static bool Show(int cash)
        {
            bool any = false;
            try
            {
                foreach (MoneyDisplayView view in Object.FindObjectsOfType<MoneyDisplayView>())
                {
                    object data = System.Activator.CreateInstance(ViewDataType);
                    AccessTools.Field(ViewDataType, "Money").SetValue(data, cash);
                    UpdateData.Invoke(view, new[] { data });
                    any = true;
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("HQ coin counter: " + e.Message);
                return true;
            }
            return any;
        }
    }

    /// <summary>HQ: the first time a player walks into the bank's office, the game's own recipe popup teaches the bank.</summary>
    public class TycoonBankTutorialSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery _players;
        private EntityQuery _desks;
        private EntityQuery _recipePopups;
        public const float TUTORIAL_DISTANCE = 1.6f;

        private int _nextPage = -1;

        protected override void Initialise()
        {
            base.Initialise();
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CPosition)));
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBankDesk)));
            _recipePopups = GetEntityQuery(new QueryHelper().All(typeof(CPopupRecipe)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _desks.IsEmpty || TycoonBankSetupSystem.OfficeRoom == int.MinValue) return;

            if (_nextPage >= 0)
            {
                if (!_recipePopups.IsEmpty) return;
                if (_nextPage >= Customs.BankTutorialDish.PageCount || _nextPage >= Mod.BankTutorialPages.Count) { _nextPage = -1; return; }
                PopupUtilities.RequestManagedPopup(KitchenData.PopupType.Recipe, new CPopupRecipe { ID = Mod.BankTutorialPages[_nextPage] });
                _nextPage++;
                return;
            }

            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.BankTutorialSeen || !TycoonPreferences.ShowCharacterTutorials || Mod.BankTutorialPages.Count == 0) return;

            Vector3 desk = EntityManager.GetComponentData<CPosition>(_desks.GetSingletonEntity()).Position;
            using NativeArray<CPosition> positions = _players.ToComponentDataArray<CPosition>(Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i].Position;
                CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(p.x), 0, Mathf.RoundToInt(p.z)));
                if (tile.Type == RoomType.NoRoom || tile.RoomID != TycoonBankSetupSystem.OfficeRoom) continue;
                Vector3 offset = p - desk;
                offset.y = 0f;
                if (offset.magnitude > TUTORIAL_DISTANCE) continue;

                TycoonCompany.BankTutorialSeen = true;
                TycoonCompany.Save();
                _nextPage = 0;
                Mod.LogInfo($"Bank: a player walked into the office for the first time; showing the bank tutorial ({Customs.BankTutorialDish.PageCount} page(s)).");
                return;
            }
        }
    }
}
