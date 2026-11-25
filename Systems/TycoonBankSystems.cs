using Kitchen;
using Kitchen.Layouts;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>HQ: puts the bank in the old franchise office: the bank counter at the room's anchor facing +z.</summary>
    public class TycoonBankSetupSystem : FranchiseSystem, IModSystem
    {
        private static readonly Vector3 DESK_OFFSET = Vector3.zero;
        private static readonly Vector3 BANKER_OFFSET = new Vector3(0f, 0f, 1f);

        private EntityQuery _desks;

        internal static int OfficeRoom = int.MinValue;
        internal static Vector3 BankerAt, BankerSide;

        protected override void Initialise()
        {
            base.Initialise();
            _desks = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBankDesk)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || !_desks.IsEmpty || Mod.BankDesk == null) return;
            if (!HasSingleton<SSelectedLayoutPedestal>()) return;

            Vector3 anchor = LobbyPositionAnchors.Contracts;
            Vector3 centre = RoomCentre(anchor, out int tiles);
            try
            {
                CLayoutRoomTile office = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(anchor.x), 0, Mathf.RoundToInt(anchor.z)));
                OfficeRoom = office.Type == RoomType.NoRoom ? int.MinValue : office.RoomID;
            }
            catch (System.Exception) { OfficeRoom = int.MinValue; }
            Vector3 bankerAt = centre + BANKER_OFFSET;
            if (tiles > 0 && !InRoom(anchor, bankerAt)) bankerAt = centre - BANKER_OFFSET;
            Vector3 toBanker = (bankerAt - centre).normalized;
            BankerAt = bankerAt;
            BankerSide = toBanker;

            Entity desk = Create(Mod.BankDesk.ID, centre, toBanker);
            EntityManager.AddComponent<CDoNotPersist>(desk);
            Mod.LogInfo($"Bank: counter at {centre} (office of {tiles} tiles around {anchor}), banker side {toBanker}.");

            try
            {
                Entity banker = EntityManager.CreateEntity(typeof(CTycoonBanker), typeof(CCustomer), typeof(CCustomerState), typeof(CPosition),
                    typeof(CPlayerCosmetics), typeof(CRequiresView), typeof(CMoveToLocation), typeof(CDoNotPersist));
                EntityManager.SetComponentData(banker, new CCustomer { Scale = 1f, Speed = 0f });
                EntityManager.SetComponentData(banker, new CPosition(bankerAt, Quaternion.LookRotation(-toBanker)));
                EntityManager.SetComponentData(banker, new CMoveToLocation { Location = bankerAt, DesiredFacing = -toBanker, StoppingDistance = 0f });
                Patches.SpecialCats.Mark(EntityManager, banker, Patches.SpecialCats.BANKER_HATS);
                EntityManager.SetComponentData(banker, new CRequiresView { Type = ViewType.CustomerCat });
                Mod.LogInfo($"Bank: the banker (cat in the {string.Join("/", Patches.SpecialCats.BANKER_HATS)}) stands at {bankerAt} facing {-toBanker}.");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Bank: could not place the banker cat: " + e.Message);
            }
        }

        private static readonly (int dx, int dz)[] Steps = { (0, 1), (1, 0), (0, -1), (-1, 0) };

        private Vector3 RoomCentre(Vector3 anchor, out int count)
        {
            count = 0;
            try
            {
                var start = (x: Mathf.RoundToInt(anchor.x), z: Mathf.RoundToInt(anchor.z));
                CLayoutRoomTile startTile = TileManager.GetTile(new IntVector3(start.x, 0, start.z));
                if (startTile.Type == RoomType.NoRoom) return anchor;
                int room = startTile.RoomID;
                var seen = new System.Collections.Generic.HashSet<(int, int)> { start };
                var queue = new System.Collections.Generic.Queue<(int x, int z)>();
                queue.Enqueue(start);
                float sx = 0f, sz = 0f;
                while (queue.Count > 0 && seen.Count < 400)
                {
                    (int x, int z) p = queue.Dequeue();
                    count++;
                    sx += p.x; sz += p.z;
                    CLayoutRoomTile tile = TileManager.GetTile(new IntVector3(p.x, 0, p.z));
                    foreach ((int dx, int dz) in Steps)
                    {
                        var n = (x: p.x + dx, z: p.z + dz);
                        if (seen.Contains(n)) continue;
                        CLayoutRoomTile next = TileManager.GetTile(new IntVector3(n.x, 0, n.z));
                        if (next.Type == RoomType.NoRoom || next.RoomID != room) continue;
                        seen.Add(n);
                        queue.Enqueue(n);
                    }
                }
                return new Vector3(Mathf.Round(sx / count), 0f, Mathf.Round(sz / count));
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Bank: could not read the office's tiles: " + e.Message);
                return anchor;
            }
        }

        private bool InRoom(Vector3 anchor, Vector3 at)
        {
            try
            {
                CLayoutRoomTile a = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(anchor.x), 0, Mathf.RoundToInt(anchor.z)));
                CLayoutRoomTile b = TileManager.GetTile(new IntVector3(Mathf.RoundToInt(at.x), 0, Mathf.RoundToInt(at.z)));
                return b.Type != RoomType.NoRoom && b.RoomID == a.RoomID;
            }
            catch (System.Exception) { return true; }
        }

        private static bool _hatsLogged;

        internal static int PickHat(string[] preferences)
        {
            var hats = new System.Collections.Generic.List<PlayerCosmetic>();
            foreach (PlayerCosmetic c in GameData.Main.Get<PlayerCosmetic>())
                if (c != null && c.CosmeticType == CosmeticType.Hat && !c.DisableInGame) hats.Add(c);
            if (!_hatsLogged)
            {
                _hatsLogged = true;
                var names = new System.Text.StringBuilder();
                foreach (PlayerCosmetic c in hats) names.Append(c.name).Append(", ");
                Mod.LogInfo("Hats in the game data: " + names);
            }
            foreach (string wanted in preferences)
                foreach (PlayerCosmetic c in hats)
                    if (!string.IsNullOrEmpty(c.name) && c.name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0) return c.ID;
            return hats.Count > 0 ? hats[0].ID : 0;
        }
    }

    /// <summary>The bank counter's menu, same plumbing as the desks.</summary>
    public class ManageBankIndicators : PlayerSpecificUIIndicator<CTycoonBankDesk, CDishSelectionInfo>, IModSystem
    {
        private const float SPEECH_SECONDS = 6f;
        private static readonly Vector3 SPEECH_OFFSET = new Vector3(0f, -1f, 0f);

        private EntityQuery _bankers;

        protected override void Initialise()
        {
            base.Initialise();
            _bankers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBanker), typeof(CPosition)));
        }

        protected override ViewType ViewType => ViewType.DishSelectorUI;

        protected override CDishSelectionInfo GetInfo(Entity source, CTycoonBankDesk selector, CTriggerPlayerSpecificUI trigger, CPlayer player)
        {
            Mod.LogInfo($"Bank: opening the bank menu for player {player.ID}.");
            if (Require(trigger.TriggerEntity, out CItemHolder hands) && hands.HeldItem != Entity.Null && Require(hands.HeldItem, out CTycoonBlueprint site))
            {
                UI.TycoonBankMenu.Holding[player.ID] = site;
                PurchasingDeskHelpers.SendDesk(player.ID, $"{(int)Patches.PurchasingDeskMenu.DeskKind.Bank},{site.Setting},{site.Size},{site.Price},{(site.Paid ? 1 : 0)}");
            }
            else
            {
                UI.TycoonBankMenu.Holding.Remove(player.ID);
                PurchasingDeskHelpers.SendDesk(player.ID, ((int)Patches.PurchasingDeskMenu.DeskKind.Bank).ToString());
            }
            TycoonBank.Read(EntityManager);
            return new CDishSelectionInfo { Player = player, PlayerEntity = trigger.TriggerEntity };
        }

        protected override bool ShouldDismiss(CDishSelectionInfo info)
        {
            if (info.Dish == 0) return base.ShouldDismiss(info);
            TycoonBank.Decode(info.Dish, out int kind, out int days, out int amount);
            if (kind == CTycoonBankRequest.SITE_BUY || kind == CTycoonBankRequest.SITE_LEASE)
            {
                SiteDeal(info.PlayerEntity, kind, days, amount);
                return true;
            }
            string done = TycoonBank.Apply(kind, days, amount);
            if (done != null)
            {
                Speak(TycoonBankerDialogue.Say(kind == CTycoonBankRequest.LOAN ? "loan" : kind == CTycoonBankRequest.PAYMENT ? "payment" : "plan"));
                Mod.LogInfo("Bank: " + done);
            }
            return true;
        }

        private void SiteDeal(Entity player, int kind, int days, int price)
        {
            if (!Require(player, out CItemHolder hands) || hands.HeldItem == Entity.Null || !Require(hands.HeldItem, out CTycoonBlueprint site)
                || site.Paid || site.Price != price)
            {
                Mod.LogWarning("Bank: no deal, the player no longer holds that unpaid blueprint.");
                return;
            }
            TycoonBank.Standing standing = TycoonBank.Read(EntityManager);
            string reason = kind == CTycoonBankRequest.SITE_BUY ? TycoonSites.CanBuy(standing, price) : TycoonSites.CanLease(standing, price);
            if (reason != null)
            {
                Mod.LogWarning($"Bank: no deal for the site ({reason}).");
                if (reason != "poor") Speak(TycoonBankerDialogue.Say("site-" + reason).Replace("{price}", price.ToString()).Replace("{credit}", standing.Credit.ToString()));
                return;
            }
            TycoonCompany.EnsureLoaded();
            if (kind == CTycoonBankRequest.SITE_BUY) TycoonCompany.Cash -= price;
            else
            {
                days = Mathf.Max(1, days);
                TycoonCompany.Outstanding = TycoonBank.Repayment(price, days);
                TycoonCompany.DailyPayment = TycoonBank.DailyPaymentFor(TycoonCompany.Outstanding, days);
                TycoonCompany.TotalBorrowed += price;
                TycoonCompany.LoansTaken++;
                TycoonCompany.DiscountedDebt = false;
                TycoonCompany.GraceNights = TycoonBank.GRACE_DAYS;
            }
            string key = TycoonSites.Key(site.Source, site.Layout, site.Setting);
            if (!TycoonCompany.SitesBought.Contains(key)) TycoonCompany.SitesBought.Add(key);
            TycoonCompany.Save();
            site.Paid = true;
            EntityManager.SetComponentData(hands.HeldItem, site);
            Speak(TycoonBankerDialogue.Say(kind == CTycoonBankRequest.SITE_BUY ? "site-bought" : "site-leased"));
            Mod.LogInfo(kind == CTycoonBankRequest.SITE_BUY
                ? $"Bank: sold the {TycoonSites.SettingName(site.Setting)} site ({site.Size} sqm) for {price}; wallet {TycoonCompany.Cash}."
                : $"Bank: leased the {TycoonSites.SettingName(site.Setting)} site ({site.Size} sqm, {price}) over {days} days: {TycoonCompany.Outstanding} owed, {TycoonCompany.DailyPayment} a day.");
        }

        private void Speak(string line)
        {
            if (_bankers.IsEmpty) return;
            using NativeArray<CPosition> positions = _bankers.ToComponentDataArray<CPosition>(Allocator.Temp);
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, positions[0].Position + SPEECH_OFFSET, line, SPEECH_SECONDS);
        }
    }

    /// <summary>Restaurant: settles loans and leases agreed at the HQ bank.</summary>
    public class TycoonLoanSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _requests;
        private EntityQuery _oldRequests;

        protected override void Initialise()
        {
            base.Initialise();
            _requests = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBankRequest)));
            _oldRequests = GetEntityQuery(new QueryHelper().All(typeof(CTycoonLoanRequest)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (_requests.IsEmpty && _oldRequests.IsEmpty) return;
            using (NativeArray<CTycoonBankRequest> requests = _requests.ToComponentDataArray<CTycoonBankRequest>(Allocator.Temp))
                for (int i = 0; i < requests.Length; i++) TycoonBank.Apply(requests[i].Kind, requests[i].Days, requests[i].Amount);
            using (NativeArray<CTycoonLoanRequest> old = _oldRequests.ToComponentDataArray<CTycoonLoanRequest>(Allocator.Temp))
                for (int i = 0; i < old.Length; i++)
                    if (old[i].Repay) TycoonBank.Apply(CTycoonBankRequest.PAYMENT, 0, int.MaxValue / 2);
                    else TycoonBank.Apply(CTycoonBankRequest.LOAN, 40, old[i].Amount);
            EntityManager.DestroyEntity(_requests);
            EntityManager.DestroyEntity(_oldRequests);
            if (HasSingleton<STycoonCompanyLinked>() && HasSingleton<SMoney>()) Set(new SMoney { Amount = TycoonCompany.Cash });
        }
    }
}
