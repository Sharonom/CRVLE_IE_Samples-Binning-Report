using SamplesBucketing.Web.Models;

namespace SamplesBucketing.Web.Services;

public interface IVpoBinService
{
    Task<IReadOnlyList<BinSplitRow>> GetBinSplitsAsync(
        IEnumerable<string> vpoNumbers,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetVisualIdsAsync(
        string vpoNumber,
        string binName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns Visual IDs grouped by VPO for the given bin across multiple VPOs.
    /// </summary>
    Task<IReadOnlyList<VisualIdGroup>> GetVisualIdsByBinAsync(
        IEnumerable<string> vpoNumbers,
        string binName,
        CancellationToken cancellationToken = default);

    /// <summary>Returns column names of vw_public_material_result for diagnostics.</summary>
    Task<IReadOnlyList<string>> GetMaterialResultColumnsAsync(
        CancellationToken cancellationToken = default);
}
