using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using PlateUpTycoon.Components;
using PlateUpTycoon.UI;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>View-side changes that turn the game's dish-selector menu into the Purchasing Desk menu.</summary>
    internal static class PurchasingDeskMenu
    {
        internal const BindingFlags ANY = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        internal static bool IsDeskMenu => GameInfo.CurrentScene == SceneType.Kitchen
            || (GameInfo.CurrentScene == SceneType.Franchise && (TycoonSession.IsActive || TycoonDayBar.Seen || TycoonNetData.Get("co") != null));

        internal enum DeskKind { Purchasing, Management, Bank, Chef, Cards, Discount }

        internal static bool Talks(DeskKind kind) => kind == DeskKind.Bank || kind == DeskKind.Chef || kind == DeskKind.Cards || kind == DeskKind.Discount;

        internal static readonly Dictionary<DishSelectionIndicator, DeskKind> Kinds = new Dictionary<DishSelectionIndicator, DeskKind>();

        internal static DeskKind KindOf(DishSelectionIndicator indicator) =>
            Kinds.TryGetValue(indicator, out DeskKind kind) ? kind : DeskKind.Purchasing;

        internal static DeskKind DetectKind(DishSelectionIndicator indicator)
        {
            int playerID = (int)AccessTools.Field(typeof(DishSelectionIndicator), "PlayerID").GetValue(indicator);
            if (Systems.PurchasingDeskHelpers.LastDeskOpened.TryGetValue(playerID, out DeskKind recorded))
                return recorded;
            int[] sent = TycoonNetData.Ints("k" + playerID);
            if (!TycoonNetData.IsHost && sent != null && sent.Length > 0 && System.Enum.IsDefined(typeof(DeskKind), sent[0]))
                return (DeskKind)sent[0];

            try
            {
                Vector3 at = indicator.transform.position;
                EntityManager em = KitchenLib.Utils.EntityUtils.GetEntityManager();
                float best = float.MaxValue;
                DeskKind kind = DeskKind.Purchasing;
                using (EntityQuery desks = em.CreateEntityQuery(typeof(CPosition), typeof(CTycoonManagementDesk)))
                    Nearest(em, desks, at, DeskKind.Management, ref best, ref kind);
                using (EntityQuery desks = em.CreateEntityQuery(typeof(CPosition), typeof(CTycoonPurchasingDesk)))
                    Nearest(em, desks, at, DeskKind.Purchasing, ref best, ref kind);
                using (EntityQuery desks = em.CreateEntityQuery(typeof(CPosition), typeof(CTycoonBankDesk)))
                    Nearest(em, desks, at, DeskKind.Bank, ref best, ref kind);
                using (EntityQuery desks = em.CreateEntityQuery(typeof(CPosition), typeof(CTycoonChefDesk)))
                    Nearest(em, desks, at, DeskKind.Chef, ref best, ref kind);
                using (EntityQuery desks = em.CreateEntityQuery(typeof(CPosition), typeof(CTycoonCardDesk)))
                    Nearest(em, desks, at, DeskKind.Cards, ref best, ref kind);
                using (EntityQuery desks = em.CreateEntityQuery(typeof(CPosition), typeof(CTycoonDiscountDesk)))
                    Nearest(em, desks, at, DeskKind.Discount, ref best, ref kind);
                if (GameInfo.CurrentScene == SceneType.Franchise && !Talks(kind)) kind = DeskKind.Bank;
                return kind;
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Tycoon desk menu: could not tell which desk opened it: " + e.Message);
                return DeskKind.Purchasing;
            }
        }

        private static void Nearest(EntityManager em, EntityQuery desks, Vector3 at, DeskKind kind, ref float best, ref DeskKind bestKind)
        {
            using Unity.Collections.NativeArray<CPosition> positions = desks.ToComponentDataArray<CPosition>(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 d = positions[i].Position - at;
                d.y = 0f;
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; bestKind = kind; }
            }
        }

        internal const int ROWS = 2;

        private static GridMenuConfig _root;
        private static Texture2D _closeIcon;
        private static Texture2D _confirmIcon;
        private static readonly Dictionary<int, GridMenuConfig> _confirmPages = new Dictionary<int, GridMenuConfig>();
        private static TycoonMenuLayout.Folder _builtFrom;
        private static int _builtRate = -1;

        internal static GridMenuConfig Root
        {
            get
            {
                TycoonMenuLayout.Folder layout = TycoonMenuLayout.Root;
                int rate = Mathf.RoundToInt(TycoonShopPrices.ApplianceMultiplier * 1000f);
                if ((object)_root == null || !ReferenceEquals(layout, _builtFrom) || rate != _builtRate)
                {
                    _builtFrom = layout;
                    _builtRate = rate;
                    _root = BuildPage(layout, isRoot: true);
                }
                return _root;
            }
        }

        private static GridMenuConfig BuildPage(TycoonMenuLayout.Folder folder, bool isRoot)
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem>();

            if (isRoot)
            {
                if ((object)_closeIcon == null) _closeIcon = MakeCloseIcon();
                page.Items.Add(new GridItemDeskClose { Icon = _closeIcon });
            }
            else
            {
                page.Items.Add(new GridItemDeskBack());
            }

            foreach (TycoonMenuLayout.Folder sub in folder.Folders)
            {
                GameData.Main.TryGet<Appliance>(sub.IconApplianceID, out Appliance icon);
                page.Items.Add(new GridItemDeskFolder { Name = sub.Name, Page = BuildPage(sub, isRoot: false), Icon = icon });
            }

            foreach (TycoonCatalogue.Entry entry in folder.Appliances)
            {
                if (TycoonDecorShop.TryGetAny(entry.ApplianceID, out TycoonDecorShop.Offer decor))
                {
                    page.Items.Add(new GridItemDecor { DecorID = decor.DecorID, Name = decor.Name, Cost = decor.Cost, ReputationPerDay = decor.ReputationPerDay });
                    continue;
                }
                if (GameData.Main.TryGet<Appliance>(entry.ApplianceID, out Appliance appliance))
                    page.Items.Add(new GridItemAppliance { Appliance = appliance, Cost = TycoonShopPrices.OrderPrice(entry) });
            }
            return page;
        }

        internal static GridMenuConfig ConfirmPage(GridItemAppliance tile)
        {
            if (_confirmPages.TryGetValue(tile.Appliance.ID, out GridMenuConfig existing) && (object)existing != null)
                return existing;
            if ((object)_confirmIcon == null) _confirmIcon = MakeTickIcon();

            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem>
            {
                new GridItemDeskBack { Title = $"Buy {tile.Appliance.Name} for {tile.Cost} coins?" },
                new GridItemDeskConfirm { Appliance = tile.Appliance, Cost = tile.Cost, Icon = _confirmIcon },
            };
            _confirmPages[tile.Appliance.ID] = page;
            return page;
        }

        internal static GridMenuConfig ConfirmDecorPage(GridItemDecor tile)
        {
            if ((object)_confirmIcon == null) _confirmIcon = MakeTickIcon();
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem>
            {
                new GridItemDeskBack { Title = $"Buy {tile.Name} for {tile.Cost} coins? (+{tile.ReputationPerDay:0.#} reputation a day where customers can see it)" },
                new GridItemDecorConfirm { DecorID = tile.DecorID, Name = tile.Name, Cost = tile.Cost, Icon = _confirmIcon },
            };
            return page;
        }

        private static Texture2D MakeTickIcon()
        {
            const int size = 128;
            const float halfThickness = 9f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var green = new Color(0.2f, 0.75f, 0.25f, 1f);
            var clear = new Color(0f, 0f, 0f, 0f);
            Vector2 a = new Vector2(30, 64), b = new Vector2(54, 40), c = new Vector2(100, 92);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var p = new Vector2(x, y);
                    bool on = DistanceToSegment(p, a, b) <= halfThickness || DistanceToSegment(p, b, c) <= halfThickness;
                    texture.SetPixel(x, y, on ? green : clear);
                }
            }
            texture.Apply();
            return texture;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        internal static Texture2D TickIcon()
        {
            if ((object)_confirmIcon == null) _confirmIcon = MakeTickIcon();
            return _confirmIcon;
        }

        internal static GridItemDeskClose CloseTile()
        {
            if ((object)_closeIcon == null) _closeIcon = MakeCloseIcon();
            return new GridItemDeskClose { Icon = _closeIcon };
        }

        private static Texture2D MakeCloseIcon()
        {
            const int size = 128;
            const float halfThickness = 9f;
            const int margin = 28;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var red = new Color(0.85f, 0.15f, 0.12f, 1f);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    bool inside = x >= margin && x < size - margin && y >= margin && y < size - margin;
                    float d1 = Mathf.Abs(x - y) / 1.4142f;
                    float d2 = Mathf.Abs(x + y - (size - 1)) / 1.4142f;
                    texture.SetPixel(x, y, inside && (d1 <= halfThickness || d2 <= halfThickness) ? red : clear);
                }
            }
            texture.Apply();
            return texture;
        }
    }

    /// <summary>Show the desk's main page instead of the dish list.</summary>
    [HarmonyPatch(typeof(DishSelectionIndicator), "InitialiseForPlayer")]
    public static class DeskMenuItemsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance)
        {
            if (!PurchasingDeskMenu.IsDeskMenu) return;
            if (!TycoonNetData.IsHost)
                OpenedAt[__instance] = LastUsed.TryGetValue(PlayerOf(__instance), out int used) ? used : int.MinValue;
            Build(__instance);
        }

        private static readonly Dictionary<DishSelectionIndicator, int> OpenedAt = new Dictionary<DishSelectionIndicator, int>();
        private static readonly Dictionary<int, int> LastUsed = new Dictionary<int, int>();

        private static int PlayerOf(DishSelectionIndicator indicator) => (int)AccessTools.Field(typeof(DishSelectionIndicator), "PlayerID").GetValue(indicator);

        private static int? CountOf(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            int comma = value.LastIndexOf(',');
            return comma > 0 && int.TryParse(value.Substring(comma + 1), out int n) ? n : (int?)null;
        }

        private static bool WaitingForHost(DishSelectionIndicator indicator, string value)
        {
            if (!OpenedAt.TryGetValue(indicator, out int openedAt)) return false;
            int? count = CountOf(value);
            if (count == null || count.Value == openedAt) return true;
            LastUsed[PlayerOf(indicator)] = count.Value;
            return false;
        }

        private static readonly Dictionary<DishSelectionIndicator, string> BuiltFrom = new Dictionary<DishSelectionIndicator, string>();
        private static readonly List<DishSelectionIndicator> _stale = new List<DishSelectionIndicator>();

        private static string KeyOf(DishSelectionIndicator indicator) =>
            "k" + (int)AccessTools.Field(typeof(DishSelectionIndicator), "PlayerID").GetValue(indicator);

        internal static void RefreshGuestMenus()
        {
            if (BuiltFrom.Count == 0 || TycoonNetData.IsHost) return;
            _stale.Clear();
            foreach (KeyValuePair<DishSelectionIndicator, string> menu in BuiltFrom)
            {
                if (menu.Key == null) { _stale.Add(menu.Key); continue; }
                if (TycoonNetData.Get(KeyOf(menu.Key)) != menu.Value) _stale.Add(menu.Key);
            }
            foreach (DishSelectionIndicator indicator in _stale)
            {
                if (indicator == null) { BuiltFrom.Remove(indicator); continue; }
                Mod.LogInfo("Desk menu: newer data from the host arrived, menu rebuilt.");
                Build(indicator);
            }
        }

        internal static void Forget(DishSelectionIndicator indicator) { BuiltFrom.Remove(indicator); OpenedAt.Remove(indicator); }

        private static void Build(DishSelectionIndicator __instance)
        {
            DeskMenuContext.Current = __instance;
            if (!TycoonNetData.IsHost)
            {
                string value = TycoonNetData.Get(KeyOf(__instance));
                BuiltFrom[__instance] = value;
                if (WaitingForHost(__instance, value))
                {
                    var waiting = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
                    waiting.Items = new List<IGridItem> { PurchasingDeskMenu.CloseTile() };
                    object waitStack = AccessTools.Field(typeof(DishSelectionIndicator), "MenuStack").GetValue(__instance);
                    waitStack.GetType().GetMethod("Clear").Invoke(waitStack, null);
                    AccessTools.Method(typeof(DishSelectionIndicator), "SetNewMenu").Invoke(__instance, new object[] { waiting, 0, 0 });
                    if (TycoonDialogueView.IsOpen(__instance)) TycoonDialogueView.Forget(__instance);
                    Mod.LogInfo("Desk menu: waiting for the host to say which desk this is.");
                    return;
                }
            }
            PurchasingDeskMenu.DeskKind kind = PurchasingDeskMenu.DetectKind(__instance);
            PurchasingDeskMenu.Kinds[__instance] = kind;
            Mod.LogInfo($"{kind} Desk: menu shown.");

            GridMenuConfig root = kind == PurchasingDeskMenu.DeskKind.Management
                ? ManagementDeskMenu.Root(__instance)
                : kind == PurchasingDeskMenu.DeskKind.Bank ? TycoonBankMenu.Start(__instance)
                : kind == PurchasingDeskMenu.DeskKind.Chef ? TycoonChefMenu.Start(__instance)
                : kind == PurchasingDeskMenu.DeskKind.Cards ? TycoonCardMarketMenu.Start(__instance)
                : kind == PurchasingDeskMenu.DeskKind.Discount ? TycoonDiscountMenu.Start(__instance)
                : PurchasingDeskMenu.Root;

            object stack = AccessTools.Field(typeof(DishSelectionIndicator), "MenuStack").GetValue(__instance);
            stack.GetType().GetMethod("Clear").Invoke(stack, null);
            AccessTools.Method(typeof(DishSelectionIndicator), "SetNewMenu").Invoke(__instance, new object[] { root, DeskMenuNavigationPatch.FIRST_TILE, 0 });

            if (PurchasingDeskMenu.Talks(kind))
            {
                TycoonDialogueView.Open(__instance);
                TycoonDialogueView.Sync(__instance);
            }
            else if (TycoonDialogueView.IsOpen(__instance)) TycoonDialogueView.Forget(__instance);
        }
    }

    /// <summary>Let folder tiles open their page.</summary>
    [HarmonyPatch(typeof(DishSelectionIndicator), "SetNewMenu")]
    public static class DeskMenuNavigationPatch
    {
        internal const int FIRST_TILE = 1;

        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance)
        {
            if (!PurchasingDeskMenu.IsDeskMenu) return;
            var menu = AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(__instance) as GridMenu;
            if (menu == null) return;

            MethodInfo setNewMenu = AccessTools.Method(typeof(DishSelectionIndicator), "SetNewMenu");
            menu.OnRequestMenu += config =>
                setNewMenu.Invoke(__instance, new object[] { config, FIRST_TILE, menu.SelectedIndex() });

            DeskMenuTitle.Update(__instance);
        }
    }

    /// <summary>Slightly smaller icons in the desk menu, so the highlight border stays visible.</summary>
    internal static class DeskMenuIcons
    {
        internal const float SCALE = 0.75f;
        private static readonly Dictionary<int, Vector3> _baseScales = new Dictionary<int, Vector3>();

        internal static void Shrink(GridMenuElement element)
        {
            if (!PurchasingDeskMenu.IsDeskMenu) return;
            ShrinkList(AccessTools.Field(typeof(GridMenuElement), "IconRenderers").GetValue(element) as IEnumerable);
            ShrinkList(AccessTools.Field(typeof(GridMenuElement), "CosmeticRenderers").GetValue(element) as IEnumerable);
        }

        private static void ShrinkList(IEnumerable renderers)
        {
            if (renderers == null) return;
            foreach (object o in renderers)
            {
                if (!(o is Renderer r)) continue;
                Transform t = r.transform;
                int id = t.GetInstanceID();
                if (!_baseScales.TryGetValue(id, out Vector3 baseScale))
                {
                    baseScale = t.localScale;
                    _baseScales[id] = baseScale;
                }
                t.localScale = baseScale * SCALE;
            }
        }
    }

    [HarmonyPatch(typeof(GridMenuElement), "Set", new[] { typeof(Texture2D) })]
    public static class DeskMenuIconTexturePatch
    {
        [HarmonyPostfix]
        private static void Postfix(GridMenuElement __instance) => DeskMenuIcons.Shrink(__instance);
    }

    [HarmonyPatch(typeof(GridMenuElement), "Set", new[] { typeof(IGridItem) })]
    public static class DeskMenuIconItemPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GridMenuElement __instance) => DeskMenuIcons.Shrink(__instance);
    }

    /// <summary>Two rows of tiles on every desk page, like the wardrobe.</summary>
    [HarmonyPatch(typeof(GenericPaginatedGridMenu), "get_ColumnLength")]
    public static class DeskMenuRowsPatch
    {
        internal const int BANK_ROWS = 12;

        [HarmonyPostfix]
        private static void Postfix(GenericPaginatedGridMenu __instance, ref int __result)
        {
            if (!PurchasingDeskMenu.IsDeskMenu) return;
            __result = GameInfo.CurrentScene == SceneType.Franchise ? BANK_ROWS : PurchasingDeskMenu.ROWS;
            if (ManagementCardsView.IsCardGrid(__instance) && ManagementCardsView.Shape(__instance, out _, out int rows)) __result = rows + 2;
        }
    }

    /// <summary>Tiles per row: 1 for the bank's vertical list.</summary>
    [HarmonyPatch(typeof(GenericPaginatedGridMenu), "get_RowLength")]
    public static class DeskMenuColumnsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GenericPaginatedGridMenu __instance, ref int __result)
        {
            if (PurchasingDeskMenu.IsDeskMenu && GameInfo.CurrentScene == SceneType.Franchise) __result = 1;
            if (PurchasingDeskMenu.IsDeskMenu && ManagementCardsView.IsCardGrid(__instance) && ManagementCardsView.Shape(__instance, out int length, out _)) __result = length;
        }
    }

    /// <summary>Draw the back tile with the game's back arrow.</summary>
    [HarmonyPatch(typeof(GenericGridMenu), "SetupElement")]
    public static class DeskMenuBackTilePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(IGridItem item, GridMenuElement element)
        {
            if (item is GridItemDeskCard)
            {
                foreach (Renderer r in element.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                return false;
            }
            if (!(item is GridItemDeskBack)) return true;
            element.SetAsBack();
            return false;
        }
    }

    /// <summary>Picking an appliance tile: open its "Buy X for N coins?" page.</summary>
    [HarmonyPatch(typeof(GenericGridMenu), "OnSelect")]
    public static class DeskMenuSelectPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GenericGridMenu __instance, IGridItem item)
        {
            DeskMenuContext.Current = DeskMenuContext.IndicatorOf(__instance) ?? DeskMenuContext.Current;
            if (item is GridItemDeskBack)
            {
                AccessTools.Method(typeof(GridMenu), "RequestGoBack").Invoke(__instance, null);
                return false;
            }
            if (item is GridItemDeskFolder folder)
            {
                if ((object)folder.Page != null)
                    AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { folder.Page });
                return false;
            }
            if (item is GridItemManageCards)
            {
                if ((object)ManagementDeskMenu.Current != null)
                    AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { ManagementCardsView.Open(ManagementDeskMenu.Current) });
                return false;
            }
            if (item is GridItemPromotion promotion)
            {
                if (promotion.Kind == ManagementDeskMenu.P_OPEN_MARKETING || promotion.Kind == ManagementDeskMenu.P_OPEN_CONTRACTS)
                {
                    ManagementDeskMenu.Armed = 0;
                    if ((object)ManagementDeskMenu.Current != null)
                        AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[]
                            { ManagementCardsView.Open(ManagementDeskMenu.Current, promotion.Kind == ManagementDeskMenu.P_OPEN_MARKETING ? 1 : 2) });
                    return false;
                }
                int code = ManagementDeskMenu.Press(promotion);
                if (code != 0)
                    AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonApplianceChoice { ApplianceID = code } });
                return false;
            }
            if (item is GridItemQualityChoice quality)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonApplianceChoice { ApplianceID = TycoonIngredientQuality.Encode(quality.Tier) } });
                return false;
            }
            if (item is GridItemPricingChoice pricing)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonApplianceChoice { ApplianceID = TycoonDishPricing.Encode(pricing.Percent) } });
                return false;
            }
            if (item is GridItemHoursChoice hours)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonApplianceChoice { ApplianceID = Systems.TycoonBusinessHoursSystem.Encode(hours.Opening, hours.Seconds) } });
                return false;
            }
            if (item is GridItemDeskCard card)
            {
                if (TycoonNetData.IsHost) ManagementCardsView.Toggle(card.UnlockID);
                else
                {
                    AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonApplianceChoice { ApplianceID = card.UnlockID } });
                }
                return false;
            }
            if (item is GridItemDeskClose)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonDeskClose() });
                return false;
            }
            if (item is GridItemAppliance tile)
            {
                AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { PurchasingDeskMenu.ConfirmPage(tile) });
                return false;
            }
            if (item is GridItemDeskConfirm confirm)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonApplianceChoice { ApplianceID = confirm.Appliance.ID } });
                return false;
            }
            if (item is GridItemDecor decorTile)
            {
                AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { PurchasingDeskMenu.ConfirmDecorPage(decorTile) });
                return false;
            }
            if (item is GridItemDecorConfirm decorConfirm)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonApplianceChoice { ApplianceID = decorConfirm.DecorID } });
                return false;
            }
            if (item is GridItemBankTip tip)
            {
                AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { TycoonBankMenu.AfterTutorialPage(tip.Page) });
                return false;
            }
            if (item is GridItemBankChoice bankChoice)
            {
                GridMenuConfig next = TycoonBankMenu.NeedsPlan(bankChoice) ? TycoonBankMenu.PlansPage(bankChoice) : TycoonBankMenu.ConfirmPage(bankChoice);
                AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { next });
                return false;
            }
            if (item is GridItemBankConfirm bankConfirm)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonBankChoice { Value = bankConfirm.Value } });
                return false;
            }
            if (item is GridItemChefBuy chefBuy)
            {
                AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { TycoonChefMenu.ConfirmPage(chefBuy) });
                return false;
            }
            if (item is GridItemCardBuy cardBuy)
            {
                AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { TycoonCardMarketMenu.ConfirmPage(cardBuy) });
                return false;
            }
            if (item is GridItemCrateBuy crateBuy)
            {
                AccessTools.Method(typeof(GridMenu), "RequestNewMenu").Invoke(__instance, new object[] { TycoonDiscountMenu.ConfirmPage(crateBuy) });
                return false;
            }
            if (item is GridItemCrateConfirm crateConfirm)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonCrateChoice { Appliance = crateConfirm.Appliance } });
                return false;
            }
            if (item is GridItemCardConfirm cardConfirm)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonCardChoice { Card = cardConfirm.Card } });
                return false;
            }
            if (item is GridItemChefConfirm chefConfirm)
            {
                AccessTools.Method(typeof(GridMenu), "Request").Invoke(__instance, new object[] { new TycoonChefChoice { Dish = chefConfirm.Dish } });
                return false;
            }
            return true;
        }
    }

    /// <summary>Store the chosen appliance ID where the dish ID normally goes.</summary>
    [HarmonyPatch(typeof(DishSelectionIndicator), "HandleRequest")]
    public static class DeskMenuRequestPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(DishSelectionIndicator __instance, object obj)
        {
            if (obj is TycoonDeskClose)
            {
                AccessTools.Method(typeof(DishSelectionIndicator), "Remove").Invoke(__instance, null);
                return false;
            }
            if (obj is TycoonBankChoice bank)
            {
                AccessTools.Field(typeof(DishSelectionIndicator), "ChosenDish").SetValue(__instance, bank.Value);
                return false;
            }
            if (obj is TycoonCardChoice card)
            {
                AccessTools.Field(typeof(DishSelectionIndicator), "ChosenDish").SetValue(__instance, card.Card);
                return false;
            }
            if (obj is TycoonCrateChoice crate)
            {
                AccessTools.Field(typeof(DishSelectionIndicator), "ChosenDish").SetValue(__instance, crate.Appliance);
                return false;
            }
            if (obj is TycoonChefChoice chef)
            {
                AccessTools.Field(typeof(DishSelectionIndicator), "ChosenDish").SetValue(__instance, chef.Dish);
                return false;
            }
            if (!(obj is TycoonApplianceChoice choice)) return true;
            AccessTools.Field(typeof(DishSelectionIndicator), "ChosenDish").SetValue(__instance, choice.ApplianceID);
            return false;
        }
    }

    /// <summary>Show the highlighted tile's name in the menu title.</summary>
    internal static class DeskMenuTitle
    {
        private static bool _loggedTitleError;

        internal static void Update(DishSelectionIndicator indicator)
        {
            if (!PurchasingDeskMenu.IsDeskMenu) return;
            DeskMenuContext.Current = indicator;
            try
            {
                var menu = AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(indicator) as GridMenu;
                if (menu == null || menu.Grid == null || menu.Grid.Selected == null) return;
                object item = menu.Grid.Selected.Item;
                string text = null;
                if (item is GridItemAppliance tile)
                    text = $"{tile.Appliance.Name}  -  {tile.Cost} coins";
                else if (item is GridItemDeskConfirm confirm)
                    text = $"Confirm: buy {confirm.Appliance.Name} for {confirm.Cost} coins";
                else if (item is GridItemDecor decor)
                    text = $"{decor.Name}  -  {decor.Cost} coins, +{decor.ReputationPerDay:0.#} reputation a day where customers can see it";
                else if (item is GridItemDecorConfirm decorConfirm)
                    text = $"Confirm: buy {decorConfirm.Name} for {decorConfirm.Cost} coins";
                else if (item is GridItemDeskFolder folder)
                    text = folder.Name;

                else if (item is GridItemDeskBack back)
                    text = back.Title ?? "Back";
                else if (item is GridItemDeskClose)
                    text = "Close";
                else if (item is GridItemDeskCard card)
                    text = ManagementCardsView.TitleFor(card.UnlockID);
                else if (ManagementDeskMenu.TitleFor(item) is string management)
                    text = management;
                if (PurchasingDeskMenu.KindOf(indicator) == PurchasingDeskMenu.DeskKind.Bank && item is IGridItem gridItem)
                    text = TycoonBankMenu.TitleFor(gridItem) ?? text;
                if (PurchasingDeskMenu.KindOf(indicator) == PurchasingDeskMenu.DeskKind.Chef && item is IGridItem chefItem)
                    text = TycoonChefMenu.TitleFor(chefItem) ?? text;
                if (PurchasingDeskMenu.KindOf(indicator) == PurchasingDeskMenu.DeskKind.Cards && item is IGridItem cardItem)
                    text = TycoonCardMarketMenu.TitleFor(cardItem) ?? text;
                if (PurchasingDeskMenu.KindOf(indicator) == PurchasingDeskMenu.DeskKind.Discount && item is IGridItem crateItem)
                    text = TycoonDiscountMenu.TitleFor(crateItem) ?? text;
                if (text == null) return;

                object title = AccessTools.Field(typeof(DishSelectionIndicator), "Title").GetValue(indicator);
                title?.GetType().GetProperty("text")?.SetValue(title, text);
            }
            catch (System.Exception e)
            {
                if (!_loggedTitleError) { _loggedTitleError = true; Mod.LogWarning("Purchasing Desk: could not set the menu title: " + e.Message); }
            }
        }
    }

    [HarmonyPatch(typeof(DishSelectionIndicator), "TakeInput")]
    public static class DeskMenuTitlePatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance) => DeskMenuTitle.Update(__instance);
    }

    /// <summary>Forget which desk a menu belonged to once it is gone.</summary>
    [HarmonyPatch(typeof(DishSelectionIndicator), "OnDestroy")]
    public static class DeskMenuForgetKindPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance)
        {
            PurchasingDeskMenu.Kinds.Remove(__instance);
            DeskMenuItemsPatch.Forget(__instance);
            DeskMenuContext.Forget(__instance);
        }
    }

    /// <summary>No rotating a Tycoon desk from its front: that button press opens the menu instead.</summary>
    [HarmonyPatch(typeof(RotateAppliances), "IsPossible")]
    public static class DeskNoFrontRotatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(RotateAppliances __instance, ref InteractionData data, ref bool __result)
        {
            if (!__result) return;
            EntityManager em = __instance.EntityManager;
            if (Systems.PurchasingDeskHelpers.IsDesk(em, data.Target)
                && Systems.PurchasingDeskHelpers.IsInFront(em, data.Target, data.Interactor, out _))
                __result = false;
        }
    }

    /// <summary>Stops the game's own interaction from opening a desk menu from any side.</summary>
    [HarmonyPatch(typeof(ActivatePlayerSpecificUIIndicators), "IsPossible")]
    public static class DeskVanillaTriggerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ActivatePlayerSpecificUIIndicators __instance, ref InteractionData data, ref bool __result)
        {
            if (__result && Systems.PurchasingDeskHelpers.IsDesk(__instance.EntityManager, data.Target))
                __result = false;
        }
    }
}
