namespace SamplesBucketing.Web.Models;

/// <summary>
/// One row per VPO in the pivot view.
/// BinCounts maps bin_name → unit quantity (Visual IDs in that bin).
/// </summary>
public sealed class VpoPivotRow
{
    public string    VpoNumber   { get; init; } = "";
    public string    Site        { get; init; } = "";
    public string    Operation   { get; init; } = "";
    public string    Program     { get; init; } = "";
    public string    Flow        { get; init; } = "";
    public DateTime? TestEndDate { get; init; }
    public string    Temperature { get; init; } = "";
    public int       Tested      { get; init; }
    public int       Good        { get; init; }

    /// <summary>Work-week string derived from TestEndDate, e.g. "202614".</summary>
    public string WwEndTest => TestEndDate.HasValue
        ? $"{TestEndDate.Value.Year}{System.Globalization.ISOWeek.GetWeekOfYear(TestEndDate.Value):D2}"
        : "";

    /// <summary>bin_name → unit count.</summary>
    public Dictionary<string, int> BinCounts { get; init; } = new();
}
