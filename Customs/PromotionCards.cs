using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>Marketing and Contract cards.</summary>
    public abstract class MarketingSlotCard : CustomUnlockCard
    {
        public abstract int Slot { get; }
        public override string UniqueNameID => "TycoonMarketing" + Slot.ToString("00");
        public override List<(Locale, UnlockInfo)> InfoList
        {
            get
            {
                TycoonPromotions.Marketing m = TycoonPromotions.MarketingAt(Slot);
                string name = m != null ? m.Name : "Marketing (unused)";
                string text = m != null ? $"{m.EffectText()} for {m.Days} day{(m.Days == 1 ? "" : "s")}. Costs {m.Price} coins." : "Not in use.";
                return new List<(Locale, UnlockInfo)> { (Locale.English, LocalisationUtils.CreateUnlockInfo(name, text, m != null ? m.Flavour : "")) };
            }
        }
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }

    public abstract class ContractSlotCard : CustomUnlockCard
    {
        public abstract int Slot { get; }
        public override string UniqueNameID => "TycoonContract" + Slot.ToString("00");
        public override List<(Locale, UnlockInfo)> InfoList
        {
            get
            {
                TycoonPromotions.Contract c = TycoonPromotions.ContractAt(Slot);
                string name = c != null ? (c.Luxury ? "Luxury Contract: " : "Contract: ") + c.Name : "Contract (unused)";
                string text = c != null ? c.Summary() : "Not in use.";
                return new List<(Locale, UnlockInfo)> { (Locale.English, LocalisationUtils.CreateUnlockInfo(name, text, c != null ? c.Flavour : "")) };
            }
        }
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects => new List<UnlockEffect>();
    }

    public class MarketingCard00 : MarketingSlotCard { public override int Slot => 0; }
    public class MarketingCard01 : MarketingSlotCard { public override int Slot => 1; }
    public class MarketingCard02 : MarketingSlotCard { public override int Slot => 2; }
    public class MarketingCard03 : MarketingSlotCard { public override int Slot => 3; }
    public class MarketingCard04 : MarketingSlotCard { public override int Slot => 4; }
    public class MarketingCard05 : MarketingSlotCard { public override int Slot => 5; }
    public class MarketingCard06 : MarketingSlotCard { public override int Slot => 6; }
    public class MarketingCard07 : MarketingSlotCard { public override int Slot => 7; }
    public class MarketingCard08 : MarketingSlotCard { public override int Slot => 8; }
    public class MarketingCard09 : MarketingSlotCard { public override int Slot => 9; }
    public class MarketingCard10 : MarketingSlotCard { public override int Slot => 10; }
    public class MarketingCard11 : MarketingSlotCard { public override int Slot => 11; }
    public class MarketingCard12 : MarketingSlotCard { public override int Slot => 12; }
    public class MarketingCard13 : MarketingSlotCard { public override int Slot => 13; }
    public class MarketingCard14 : MarketingSlotCard { public override int Slot => 14; }
    public class MarketingCard15 : MarketingSlotCard { public override int Slot => 15; }
    public class MarketingCard16 : MarketingSlotCard { public override int Slot => 16; }
    public class MarketingCard17 : MarketingSlotCard { public override int Slot => 17; }
    public class MarketingCard18 : MarketingSlotCard { public override int Slot => 18; }
    public class MarketingCard19 : MarketingSlotCard { public override int Slot => 19; }
    public class MarketingCard20 : MarketingSlotCard { public override int Slot => 20; }
    public class MarketingCard21 : MarketingSlotCard { public override int Slot => 21; }
    public class MarketingCard22 : MarketingSlotCard { public override int Slot => 22; }
    public class MarketingCard23 : MarketingSlotCard { public override int Slot => 23; }
    public class MarketingCard24 : MarketingSlotCard { public override int Slot => 24; }
    public class MarketingCard25 : MarketingSlotCard { public override int Slot => 25; }
    public class MarketingCard26 : MarketingSlotCard { public override int Slot => 26; }
    public class MarketingCard27 : MarketingSlotCard { public override int Slot => 27; }
    public class MarketingCard28 : MarketingSlotCard { public override int Slot => 28; }
    public class MarketingCard29 : MarketingSlotCard { public override int Slot => 29; }
    public class MarketingCard30 : MarketingSlotCard { public override int Slot => 30; }
    public class MarketingCard31 : MarketingSlotCard { public override int Slot => 31; }
    public class MarketingCard32 : MarketingSlotCard { public override int Slot => 32; }
    public class MarketingCard33 : MarketingSlotCard { public override int Slot => 33; }
    public class MarketingCard34 : MarketingSlotCard { public override int Slot => 34; }
    public class MarketingCard35 : MarketingSlotCard { public override int Slot => 35; }
    public class MarketingCard36 : MarketingSlotCard { public override int Slot => 36; }
    public class MarketingCard37 : MarketingSlotCard { public override int Slot => 37; }
    public class MarketingCard38 : MarketingSlotCard { public override int Slot => 38; }
    public class MarketingCard39 : MarketingSlotCard { public override int Slot => 39; }
    public class ContractCard00 : ContractSlotCard { public override int Slot => 0; }
    public class ContractCard01 : ContractSlotCard { public override int Slot => 1; }
    public class ContractCard02 : ContractSlotCard { public override int Slot => 2; }
    public class ContractCard03 : ContractSlotCard { public override int Slot => 3; }
    public class ContractCard04 : ContractSlotCard { public override int Slot => 4; }
    public class ContractCard05 : ContractSlotCard { public override int Slot => 5; }
    public class ContractCard06 : ContractSlotCard { public override int Slot => 6; }
    public class ContractCard07 : ContractSlotCard { public override int Slot => 7; }
    public class ContractCard08 : ContractSlotCard { public override int Slot => 8; }
    public class ContractCard09 : ContractSlotCard { public override int Slot => 9; }
    public class ContractCard10 : ContractSlotCard { public override int Slot => 10; }
    public class ContractCard11 : ContractSlotCard { public override int Slot => 11; }
    public class ContractCard12 : ContractSlotCard { public override int Slot => 12; }
    public class ContractCard13 : ContractSlotCard { public override int Slot => 13; }
    public class ContractCard14 : ContractSlotCard { public override int Slot => 14; }
    public class ContractCard15 : ContractSlotCard { public override int Slot => 15; }
    public class ContractCard16 : ContractSlotCard { public override int Slot => 16; }
    public class ContractCard17 : ContractSlotCard { public override int Slot => 17; }
    public class ContractCard18 : ContractSlotCard { public override int Slot => 18; }
    public class ContractCard19 : ContractSlotCard { public override int Slot => 19; }
    public class ContractCard20 : ContractSlotCard { public override int Slot => 20; }
    public class ContractCard21 : ContractSlotCard { public override int Slot => 21; }
    public class ContractCard22 : ContractSlotCard { public override int Slot => 22; }
    public class ContractCard23 : ContractSlotCard { public override int Slot => 23; }
    public class ContractCard24 : ContractSlotCard { public override int Slot => 24; }
    public class ContractCard25 : ContractSlotCard { public override int Slot => 25; }
    public class ContractCard26 : ContractSlotCard { public override int Slot => 26; }
    public class ContractCard27 : ContractSlotCard { public override int Slot => 27; }
    public class ContractCard28 : ContractSlotCard { public override int Slot => 28; }
    public class ContractCard29 : ContractSlotCard { public override int Slot => 29; }
}
