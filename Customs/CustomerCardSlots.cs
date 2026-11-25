using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using System.Collections.Generic;

namespace PlateUpTycoon.Customs
{
    /// <summary>Chubs's custom customer cards: fixed slot classes.</summary>
    public abstract class CustomerCardSlot : CustomUnlockCard
    {
        public abstract int Slot { get; }
        public override string UniqueNameID => "TycoonCustomerCard" + Slot.ToString("00");
        public override List<(Locale, UnlockInfo)> InfoList
        {
            get
            {
                TycoonCustomerCards.Card c = TycoonCustomerCards.At(Slot);
                string name = c != null ? c.Name : "Customer card (unused)";
                string text = c != null ? Text(c) : "Not in use.";
                return new List<(Locale, UnlockInfo)> { (Locale.English, LocalisationUtils.CreateUnlockInfo(name, text, c != null ? c.Flavour : "")) };
            }
        }
        public override bool IsUnlockable => false;
        public override UnlockGroup UnlockGroup => UnlockGroup.Generic;
        public override CardType CardType => CardType.Default;
        public override List<UnlockEffect> Effects
        {
            get
            {
                TycoonCustomerCards.Card c = TycoonCustomerCards.At(Slot);
                var effects = new List<UnlockEffect>();
                if (c != null && c.ChangesGroups)
                    effects.Add(new ParameterEffect { Parameters = new KitchenParameters { MinimumGroupSize = c.MinGroup, MaximumGroupSize = c.MaxGroup } });
                return effects;
            }
        }

        private static string Text(TycoonCustomerCards.Card c)
        {
            var parts = new List<string>();
            if (c.Customers != 0) parts.Add($"{c.Customers:+0;-0}% customers every day.");
            if (c.MaxGroup != 0) parts.Add($"+{c.MaxGroup} maximum group size");
            if (c.MinGroup != 0) parts.Add($"+{c.MinGroup} minimum group size");
            return parts.Count > 0 ? string.Join("\n", parts) : "No effect.";
        }
    }

    public class CustomerCard00 : CustomerCardSlot { public override int Slot => 0; }
    public class CustomerCard01 : CustomerCardSlot { public override int Slot => 1; }
    public class CustomerCard02 : CustomerCardSlot { public override int Slot => 2; }
    public class CustomerCard03 : CustomerCardSlot { public override int Slot => 3; }
    public class CustomerCard04 : CustomerCardSlot { public override int Slot => 4; }
    public class CustomerCard05 : CustomerCardSlot { public override int Slot => 5; }
    public class CustomerCard06 : CustomerCardSlot { public override int Slot => 6; }
    public class CustomerCard07 : CustomerCardSlot { public override int Slot => 7; }
    public class CustomerCard08 : CustomerCardSlot { public override int Slot => 8; }
    public class CustomerCard09 : CustomerCardSlot { public override int Slot => 9; }
    public class CustomerCard10 : CustomerCardSlot { public override int Slot => 10; }
    public class CustomerCard11 : CustomerCardSlot { public override int Slot => 11; }
    public class CustomerCard12 : CustomerCardSlot { public override int Slot => 12; }
    public class CustomerCard13 : CustomerCardSlot { public override int Slot => 13; }
    public class CustomerCard14 : CustomerCardSlot { public override int Slot => 14; }
    public class CustomerCard15 : CustomerCardSlot { public override int Slot => 15; }
    public class CustomerCard16 : CustomerCardSlot { public override int Slot => 16; }
    public class CustomerCard17 : CustomerCardSlot { public override int Slot => 17; }
    public class CustomerCard18 : CustomerCardSlot { public override int Slot => 18; }
    public class CustomerCard19 : CustomerCardSlot { public override int Slot => 19; }
}
