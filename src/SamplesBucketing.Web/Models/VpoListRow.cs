namespace SamplesBucketing.Web.Models;

/// <summary>One row in the VPO browse list.</summary>
public sealed class VpoListRow
{
    public string    VpoNumber    { get; init; } = "";
    public string    Status       { get; init; } = "";
    public string    Site         { get; init; } = "";
    public string    Operation    { get; init; } = "";
    public string    Flow         { get; init; } = "";
    public DateTime? TestEndDate  { get; init; }
    public string    WwEndTest    => TestEndDate.HasValue
        ? $"{TestEndDate.Value.Year}{System.Globalization.ISOWeek.GetWeekOfYear(TestEndDate.Value):D2}"
        : "";
    public int       Tested       { get; init; }
    public int       Good         { get; init; }
    public string    Description  { get; init; } = "";
}
