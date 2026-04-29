using Dapper;
using SamplesBucketing.Web.Data;
using SamplesBucketing.Web.Models;

namespace SamplesBucketing.Web.Services;

public sealed class VpoBinService : IVpoBinService
{
    private const int MaxVpoCount = 1000;
    private const int MaxVpoLength = 100;

    private readonly IDbConnectionFactory _factory;

    public VpoBinService(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<BinSplitRow>> GetBinSplitsAsync(
        IEnumerable<string> vpoNumbers,
        CancellationToken cancellationToken = default)
    {
        var vpos = vpoNumbers
            .Select(v => v.Trim())
            .Where(v => v.Length > 0 && v.Length <= MaxVpoLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxVpoCount)
            .ToList();

        if (vpos.Count == 0)
            return Array.Empty<BinSplitRow>();

        // Parameterised IN list — Dapper expands @vpos into individual parameters.
        // Join path (all read-only SELECTs):
        //   vw_vpo → vw_public_task       (task metadata: operation, site, program, dates, qty)
        //          → vw_public_result_bin  (bin definitions, via current_task_id = task_id)
        //          → vw_public_material_result (per-unit Visual IDs, via result_id = result_id)
        // COUNT(mr.material_result_id) = number of Visual IDs assigned to each bin.
        const string sql = """
            SELECT
                v.vpo_number                                    AS VpoNumber,
                ISNULL(t.processing_site_id, '')                AS Site,
                ISNULL(t.operation_code,     '')                AS Operation,
                ISNULL(t.TestProgramName,    '')                AS Program,
                ISNULL(t.task_name,          '')                AS Flow,
                t.complete_date                                 AS TestEndDate,
                ISNULL(t.task_temperature,   '')                AS Temperature,
                ISNULL(t.step_in_quantity,   0)                 AS Tested,
                ISNULL(t.step_out_quantity,  0)                 AS Good,
                b.bin_name                                      AS BinName,
                COALESCE(COUNT(mr.material_result_id), 0)       AS Quantity
            FROM  vortex_dbo.vw_vpo v
            INNER JOIN vortex_dbo.vw_public_task t
                   ON t.task_id = v.current_task_id
            INNER JOIN vortex_dbo.vw_public_result_bin b
                   ON b.task_id = v.current_task_id
            LEFT  JOIN vortex_dbo.vw_public_material_result mr
                   ON mr.result_id = b.result_id
            WHERE v.vpo_number IN @vpos
            GROUP BY v.vpo_number,
                     t.processing_site_id, t.operation_code, t.TestProgramName,
                     t.task_name, t.complete_date, t.task_temperature,
                     t.step_in_quantity, t.step_out_quantity,
                     b.bin_name
            ORDER BY v.vpo_number, b.bin_name;
            """;

        using var conn = _factory.CreateConnection();
        var command = new CommandDefinition(sql, new { vpos }, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<BinSplitRow>(command);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<string>> GetVisualIdsAsync(
        string vpoNumber,
        string binName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vpoNumber) || string.IsNullOrWhiteSpace(binName))
            return Array.Empty<string>();

        const string sql = """
            SELECT mr.visual_id
            FROM  vortex_dbo.vw_vpo v
            INNER JOIN vortex_dbo.vw_public_task t
                   ON t.task_id = v.current_task_id
            INNER JOIN vortex_dbo.vw_public_result_bin b
                   ON b.task_id = v.current_task_id
            INNER JOIN vortex_dbo.vw_public_material_result mr
                   ON mr.result_id = b.result_id
            WHERE v.vpo_number = @vpoNumber
              AND b.bin_name   = @binName
            ORDER BY mr.visual_id;
            """;

        using var conn = _factory.CreateConnection();
        var command = new CommandDefinition(sql, new { vpoNumber, binName }, cancellationToken: cancellationToken);
        var ids = await conn.QueryAsync<string>(command);
        return ids.AsList();
    }

    public async Task<IReadOnlyList<VisualIdGroup>> GetVisualIdsByBinAsync(
        IEnumerable<string> vpoNumbers,
        string binName,
        CancellationToken cancellationToken = default)
    {
        var vpos = vpoNumbers
            .Select(v => v.Trim())
            .Where(v => v.Length > 0 && v.Length <= MaxVpoLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxVpoCount)
            .ToList();

        if (vpos.Count == 0 || string.IsNullOrWhiteSpace(binName))
            return Array.Empty<VisualIdGroup>();

        const string sql = """
            SELECT v.vpo_number  AS VpoNumber,
                   mr.visual_id  AS VisualId
            FROM  vortex_dbo.vw_vpo v
            INNER JOIN vortex_dbo.vw_public_task t
                   ON t.task_id = v.current_task_id
            INNER JOIN vortex_dbo.vw_public_result_bin b
                   ON b.task_id = v.current_task_id
            INNER JOIN vortex_dbo.vw_public_material_result mr
                   ON mr.result_id = b.result_id
            WHERE v.vpo_number IN @vpos
              AND b.bin_name    = @binName
            ORDER BY v.vpo_number, mr.visual_id;
            """;

        using var conn = _factory.CreateConnection();
        var command = new CommandDefinition(sql, new { vpos, binName }, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<VpoVisualIdRow>(command);

        return rows
            .GroupBy(r => r.VpoNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => new VisualIdGroup
            {
                VpoNumber = g.Key,
                VisualIds = g.Select(r => r.VisualId).ToList()
            })
            .ToList();
    }

    private sealed class VpoVisualIdRow
    {
        public string VpoNumber { get; init; } = "";
        public string VisualId  { get; init; } = "";
    }

    public async Task<IReadOnlyList<string>> GetMaterialResultColumnsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM   INFORMATION_SCHEMA.COLUMNS
            WHERE  TABLE_NAME = 'vw_public_material_result'
            ORDER  BY ORDINAL_POSITION;
            """;
        using var conn = _factory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var cols = await conn.QueryAsync<string>(command);
        return cols.AsList();
    }
}
