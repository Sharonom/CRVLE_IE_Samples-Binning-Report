using SamplesBucketing.Web.Models;

namespace SamplesBucketing.Web.Services;

public interface IVpoListService
{
    /// <summary>
    /// Returns VPOs whose vpo_number starts with <paramref name="prefix"/>
    /// (case-insensitive). Returns up to <paramref name="maxRows"/> results.
    /// Pass an empty prefix to get the most recent VPOs.
    /// </summary>
    Task<IReadOnlyList<VpoListRow>> SearchAsync(
        string prefix,
        int maxRows = 200,
        CancellationToken ct = default);
}
