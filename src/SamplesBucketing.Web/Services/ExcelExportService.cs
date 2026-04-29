using ClosedXML.Excel;
using SamplesBucketing.Web.Models;

namespace SamplesBucketing.Web.Services;

public sealed class ExcelExportService
{
    // Fixed metadata column headers (PROGRAM and TEMPERATURE excluded from display)
    private static readonly string[] MetaHeaders =
        ["SITE", "LOT", "OPERATION", "FLOW",
         "TEST_END_DATE", "WW_END_TEST", "#TESTED", "T_GOOD"];

    public byte[] BuildBinSplitWorkbook(IReadOnlyList<VpoPivotRow> pivotRows, IReadOnlyList<string> binColumns)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Bin Split");

        // ── Header row ────────────────────────────────────────────────────────
        int col = 1;
        foreach (var h in MetaHeaders)
            ws.Cell(1, col++).Value = h;

        foreach (var bin in binColumns)
            ws.Cell(1, col++).Value = bin;

        int totalCols = MetaHeaders.Length + binColumns.Count;
        var headerRange = ws.Range(1, 1, 1, totalCols);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // ── Data rows ─────────────────────────────────────────────────────────
        int rowIdx = 2;
        foreach (var row in pivotRows)
        {
            col = 1;
            ws.Cell(rowIdx, col++).Value = row.Site;
            ws.Cell(rowIdx, col++).Value = row.VpoNumber;
            ws.Cell(rowIdx, col++).Value = row.Operation;
            ws.Cell(rowIdx, col++).Value = row.Flow;
            ws.Cell(rowIdx, col).Value   = row.TestEndDate.HasValue
                ? row.TestEndDate.Value.ToString("dd-MMM-yyyy HH:mm:ss").ToUpperInvariant()
                : "";
            col++;
            ws.Cell(rowIdx, col++).Value = row.WwEndTest;
            ws.Cell(rowIdx, col++).Value = row.Tested;
            ws.Cell(rowIdx, col++).Value = row.Good;

            foreach (var bin in binColumns)
            {
                ws.Cell(rowIdx, col++).Value =
                    row.BinCounts.TryGetValue(bin, out var q) ? q : 0;
            }

            // Alternating row background
            bool isEven = (rowIdx % 2 == 0);
            var rowRange = ws.Range(rowIdx, 1, rowIdx, totalCols);
            rowRange.Style.Fill.BackgroundColor = isEven
                ? XLColor.FromHtml("#EAF3FB")
                : XLColor.White;
            // Center all cells in this row
            rowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rowRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

            rowIdx++;
        }

        // ── Table (stop before totals row) + formatting ───────────────────────
        if (rowIdx > 2)
        {
            ws.Range(1, 1, rowIdx - 1, totalCols)
              .CreateTable("BinSplit")
              .ShowAutoFilter = true;

            // Borders for the whole data+header block
            var tableRange = ws.Range(1, 1, rowIdx - 1, totalCols);
            tableRange.Style.Border.OutsideBorder     = XLBorderStyleValues.Medium;
            tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1F4E79");
            tableRange.Style.Border.InsideBorder      = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#90C4E8");
        }

        // ── Totals row ────────────────────────────────────────────────────────
        if (pivotRows.Count > 0)
        {
            int metaLabelCols = MetaHeaders.Length - 2;
            ws.Cell(rowIdx, metaLabelCols).Value = "TOTAL";
            ws.Range(rowIdx, 1, rowIdx, metaLabelCols).Merge();
            ws.Range(rowIdx, 1, rowIdx, metaLabelCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            col = MetaHeaders.Length - 1; // #TESTED column
            ws.Cell(rowIdx, col++).Value = pivotRows.Sum(r => r.Tested);
            ws.Cell(rowIdx, col++).Value = pivotRows.Sum(r => r.Good);

            foreach (var bin in binColumns)
                ws.Cell(rowIdx, col++).Value = pivotRows.Sum(r => r.BinCounts.TryGetValue(bin, out var q) ? q : 0);

            var totalsRange = ws.Range(rowIdx, 1, rowIdx, totalCols);
            totalsRange.Style.Font.Bold = true;
            totalsRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D0E8F8");
            totalsRange.Style.Font.FontColor = XLColor.FromHtml("#00438A");
            totalsRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            totalsRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            totalsRange.Style.Border.OutsideBorder     = XLBorderStyleValues.Medium;
            totalsRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1F4E79");
        }

        // Row height for header + data
        ws.Row(1).Height = 20;
        for (int r = 2; r < rowIdx; r++) ws.Row(r).Height = 16;
        if (pivotRows.Count > 0) ws.Row(rowIdx).Height = 18;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] BuildVisualIdWorkbook(string vpoNumber, string binName, IReadOnlyList<string> visualIds)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Visual IDs");

        // ── Header row ────────────────────────────────────────────────────────
        ws.Cell(1, 1).Value = "#";
        ws.Cell(1, 2).Value = "VPO";
        ws.Cell(1, 3).Value = "BIN";
        ws.Cell(1, 4).Value = "VISUAL_ID";

        var headerRange = ws.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // ── Data rows ─────────────────────────────────────────────────────────
        int rowIdx = 2;
        foreach (var id in visualIds)
        {
            ws.Cell(rowIdx, 1).Value = rowIdx - 1;
            ws.Cell(rowIdx, 2).Value = vpoNumber;
            ws.Cell(rowIdx, 3).Value = binName;
            ws.Cell(rowIdx, 4).Value = id;

            bool isEven = (rowIdx % 2 == 0);
            var rowRange = ws.Range(rowIdx, 1, rowIdx, 4);
            rowRange.Style.Fill.BackgroundColor = isEven
                ? XLColor.FromHtml("#EAF3FB")
                : XLColor.White;
            rowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rowRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            rowIdx++;
        }

        if (rowIdx > 2)
        {
            ws.Range(1, 1, rowIdx - 1, 4)
              .CreateTable("VisualIds")
              .ShowAutoFilter = true;

            var tableRange = ws.Range(1, 1, rowIdx - 1, 4);
            tableRange.Style.Border.OutsideBorder      = XLBorderStyleValues.Medium;
            tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1F4E79");
            tableRange.Style.Border.InsideBorder       = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorderColor  = XLColor.FromHtml("#90C4E8");
        }

        ws.Row(1).Height = 20;
        for (int r = 2; r < rowIdx; r++) ws.Row(r).Height = 16;

        ws.Columns().AdjustToContents();

        using var ms2 = new MemoryStream();
        workbook.SaveAs(ms2);
        return ms2.ToArray();
    }

    public byte[] BuildBinVisualIdWorkbook(string binName, IReadOnlyList<VisualIdGroup> groups)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Visual IDs");

        // ── Header row ────────────────────────────────────────────────────────
        ws.Cell(1, 1).Value = "#";
        ws.Cell(1, 2).Value = "VPO";
        ws.Cell(1, 3).Value = "BIN";
        ws.Cell(1, 4).Value = "VISUAL_ID";

        var headerRange = ws.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // ── Data rows — one per Visual ID ─────────────────────────────────────
        int rowIdx = 2;
        int globalSeq = 1;
        foreach (var group in groups)
        {
            foreach (var id in group.VisualIds)
            {
                ws.Cell(rowIdx, 1).Value = globalSeq++;
                ws.Cell(rowIdx, 2).Value = group.VpoNumber;
                ws.Cell(rowIdx, 3).Value = binName;
                ws.Cell(rowIdx, 4).Value = id;

                bool isEven = (rowIdx % 2 == 0);
                var rowRange = ws.Range(rowIdx, 1, rowIdx, 4);
                rowRange.Style.Fill.BackgroundColor = isEven
                    ? XLColor.FromHtml("#EAF3FB")
                    : XLColor.White;
                rowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rowRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                rowIdx++;
            }
        }

        if (rowIdx > 2)
        {
            ws.Range(1, 1, rowIdx - 1, 4)
              .CreateTable("BinVisualIds")
              .ShowAutoFilter = true;

            var tableRange = ws.Range(1, 1, rowIdx - 1, 4);
            tableRange.Style.Border.OutsideBorder      = XLBorderStyleValues.Medium;
            tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1F4E79");
            tableRange.Style.Border.InsideBorder       = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorderColor  = XLColor.FromHtml("#90C4E8");
        }

        ws.Row(1).Height = 20;
        for (int r = 2; r < rowIdx; r++) ws.Row(r).Height = 16;

        ws.Columns().AdjustToContents();

        using var ms3 = new MemoryStream();
        workbook.SaveAs(ms3);
        return ms3.ToArray();
    }
}

