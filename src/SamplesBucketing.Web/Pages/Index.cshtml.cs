using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesBucketing.Web.Models;
using SamplesBucketing.Web.Services;

namespace SamplesBucketing.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IVpoBinService _vpoBinService;
    private readonly ExcelExportService _excelService;

    public IndexModel(IVpoBinService vpoBinService, ExcelExportService excelService)
    {
        _vpoBinService = vpoBinService;
        _excelService = excelService;
    }

    [BindProperty]
    public string VpoInput { get; set; } = "";

    /// <summary>Pivot rows — one entry per VPO.</summary>
    public IReadOnlyList<VpoPivotRow> PivotRows { get; private set; } = Array.Empty<VpoPivotRow>();

    /// <summary>All bin names across every result VPO, in sorted order (used for column headers).</summary>
    public IReadOnlyList<string> BinColumns { get; private set; } = Array.Empty<string>();

    /// <summary>Flat Visual ID detail rows — populated on search for the inline debug table.</summary>
    public IReadOnlyList<VisualIdDetailRow> VisualIdRows { get; private set; } = Array.Empty<VisualIdDetailRow>();
    public string? VisualIdError { get; private set; }
    /// <summary>Columns of vw_public_material_result — shown when VisualIdError is set to identify correct column name.</summary>
    public IReadOnlyList<string> SchemaColumns { get; private set; } = Array.Empty<string>();

    public string? ErrorMessage { get; private set; }
    public bool HasSearched { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var vpos = ParseVpos(VpoInput).ToList();

        if (!ValidateVpos(vpos))
            return Page();

        HasSearched = true;
        try
        {
            var rows = await _vpoBinService.GetBinSplitsAsync(vpos, ct);
            (PivotRows, BinColumns) = BuildPivot(rows);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Database error: {ex.Message}";
        }

        // Load Visual IDs inline for the debug table
        try
        {
            VisualIdRows = await _vpoBinService.GetAllVisualIdsForVposAsync(vpos, ct);
        }
        catch (Exception ex)
        {
            VisualIdError = ex.Message;
            // Fetch schema so we can show the actual column names in the error
            try { SchemaColumns = await _vpoBinService.GetMaterialResultColumnsAsync(ct); }
            catch { /* ignore */ }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostExcelAsync(CancellationToken ct)
    {
        var vpos = ParseVpos(VpoInput).ToList();

        if (!ValidateVpos(vpos))
            return Page();

        IReadOnlyList<BinSplitRow> results;
        try
        {
            results = await _vpoBinService.GetBinSplitsAsync(vpos, ct);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Database error: {ex.Message}";
            HasSearched = true;
            return Page();
        }
        var (pivotRows, binColumns) = BuildPivot(results);
        var bytes = _excelService.BuildBinSplitWorkbook(pivotRows, binColumns);
        var fileName = $"vpo_bin_split_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    public async Task<IActionResult> OnGetVisualIdsAsync(string vpo, string bin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vpo) || string.IsNullOrWhiteSpace(bin))
            return new JsonResult(Array.Empty<string>());

        try
        {
            var ids = await _vpoBinService.GetVisualIdsAsync(vpo.Trim(), bin.Trim(), ct);
            return new JsonResult(ids);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    public async Task<IActionResult> OnGetBinVisualIdsAsync(
        string[] vpo,
        string bin,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bin) || vpo.Length == 0)
            return new JsonResult(new { error = "Missing bin or vpo parameters.", vpoCount = vpo.Length, bin });

        try
        {
            var groups = await _vpoBinService.GetVisualIdsByBinAsync(vpo, bin.Trim(), ct);
            return new JsonResult(groups);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnGetSchemaAsync(CancellationToken ct)
    {
        try
        {
            var cols = await _vpoBinService.GetMaterialResultColumnsAsync(ct);
            return new JsonResult(cols);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Diagnostic: returns column names + top 3 sample rows from vw_public_material_result.
    /// Navigate to /?handler=SampleRows in the browser.
    /// </summary>
    public async Task<IActionResult> OnGetSampleRowsAsync(CancellationToken ct)
    {
        try
        {
            var (columns, rows) = await _vpoBinService.GetSampleMaterialResultRowsAsync(ct);
            return new JsonResult(new { columns, rows });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnGetBinVisualIdsExcelAsync(
        string[] vpo,
        string bin,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bin) || vpo.Length == 0)
            return BadRequest();

        IReadOnlyList<VisualIdGroup> groups;
        try
        {
            groups = await _vpoBinService.GetVisualIdsByBinAsync(vpo, bin.Trim(), ct);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }

        var bytes = _excelService.BuildBinVisualIdWorkbook(bin.Trim(), groups);
        var safeBin = string.Concat(bin.Trim().Where(c => char.IsLetterOrDigit(c) || c == '_'));
        var fileName = $"visual_ids_{safeBin}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    public async Task<IActionResult> OnGetVisualIdsExcelAsync(string vpo, string bin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vpo) || string.IsNullOrWhiteSpace(bin))
            return BadRequest();

        IReadOnlyList<string> ids;
        try
        {
            ids = await _vpoBinService.GetVisualIdsAsync(vpo.Trim(), bin.Trim(), ct);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }

        var bytes = _excelService.BuildVisualIdWorkbook(vpo.Trim(), bin.Trim(), ids);
        var fileName = $"visual_ids_{vpo}_{bin}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------

    /// <summary>
    /// Groups flat BinSplitRow results into one VpoPivotRow per VPO
    /// and returns the sorted list of unique bin names (for column headers).
    /// </summary>
    private static (IReadOnlyList<VpoPivotRow> rows, IReadOnlyList<string> bins)
        BuildPivot(IEnumerable<BinSplitRow> flatRows)
    {
        var groups = flatRows
            .GroupBy(r => r.VpoNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allBins = groups
            .SelectMany(g => g.Select(r => r.BinName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pivot = groups.Select(g =>
        {
            var first = g.First();
            return new VpoPivotRow
            {
                VpoNumber   = first.VpoNumber,
                Site        = first.Site,
                Operation   = first.Operation,
                Program     = first.Program,
                Flow        = first.Flow,
                TestEndDate = first.TestEndDate,
                Temperature = first.Temperature,
                Tested      = first.Tested,
                Good        = first.Good,
                BinCounts   = g.ToDictionary(
                    r => r.BinName,
                    r => r.Quantity,
                    StringComparer.OrdinalIgnoreCase)
            };
        }).ToList();

        return (pivot, allBins);
    }

    private static IEnumerable<string> ParseVpos(string? input) =>
        (input ?? "")
            .Split(new[] { '\n', '\r', ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private bool ValidateVpos(List<string> vpos)
    {
        if (vpos.Count == 0)
        {
            ErrorMessage = "Please enter at least one VPO number.";
            return false;
        }

        if (vpos.Count > 1000)
        {
            ErrorMessage = "Maximum 1,000 VPO numbers are allowed per search.";
            return false;
        }

        return true;
    }
}
