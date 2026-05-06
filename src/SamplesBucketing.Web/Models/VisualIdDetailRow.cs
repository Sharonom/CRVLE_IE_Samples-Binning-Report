namespace SamplesBucketing.Web.Models;

/// <summary>One Visual ID row — used for the inline debug/detail table.</summary>
public sealed class VisualIdDetailRow
{
    public string VpoNumber { get; init; } = "";
    public string BinName   { get; init; } = "";
    public string VisualId  { get; init; } = "";
}
