namespace SamplesBucketing.Web.Models;

/// <summary>Raw DB row returned by the query — one row per VPO+Bin combination.</summary>
public sealed class BinSplitRow
{
    // Per-VPO metadata (same value repeated for each bin row of the same VPO)
    public string VpoNumber      { get; init; } = "";
    public string Site           { get; init; } = "";   // processing_site_id
    public string Operation      { get; init; } = "";   // operation_code
    public string Program        { get; init; } = "";   // TestProgramName
    public string Flow           { get; init; } = "";   // task_name
    public DateTime? TestEndDate { get; init; }         // complete_date
    public string Temperature    { get; init; } = "";   // task_temperature
    public int    Tested         { get; init; }         // step_in_quantity
    public int    Good           { get; init; }         // step_out_quantity

    // Per-bin data
    public string BinName { get; init; } = "";
    public int    Quantity { get; init; }
}
