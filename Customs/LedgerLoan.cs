namespace PlateUpTycoon.Customs
{
    /// <summary>End-of-day row: the day's loan repayment.</summary>
    public class LedgerLoan : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerLoan";
        protected override string Label => "Loan repayment";
    }

    /// <summary>End-of-day row: the health inspector's fine for messes and full bins.</summary>
    public class LedgerInspectionFine : LedgerText
    {
        public override string UniqueNameID => "TycoonLedgerInspectionFine";
        protected override string Label => "Health inspection fine";
    }
}
