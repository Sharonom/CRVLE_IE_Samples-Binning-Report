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

    /// <summary>Returns all Visual ID detail rows for the given VPOs across every bin.</summary>
    Task<IReadOnlyList<VisualIdDetailRow>> GetAllVisualIdsForVposAsync(
        IEnumerable<string> vpoNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>Diagnostic: returns column names + top 3 rows from vw_public_material_result as raw dictionaries.</summary>
    Task<(IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, object?>> Rows)>
        GetSampleMaterialResultRowsAsync(CancellationToken cancellationToken = default);
}
