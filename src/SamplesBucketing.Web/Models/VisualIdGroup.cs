namespace SamplesBucketing.Web.Models;

/// <summary>Visual IDs for one VPO within a given bin.</summary>
public sealed class VisualIdGroup
{
    public string VpoNumber { get; init; } = "";
    public IReadOnlyList<string> VisualIds { get; init; } = Array.Empty<string>();
}
