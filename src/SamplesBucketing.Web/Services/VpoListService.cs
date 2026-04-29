using Dapper;
using SamplesBucketing.Web.Data;
using SamplesBucketing.Web.Models;

namespace SamplesBucketing.Web.Services;

public sealed class VpoListService : IVpoListService
{
    private readonly IDbConnectionFactory _factory;
    public VpoListService(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<VpoListRow>> SearchAsync(
        string prefix,
        int maxRows = 200,
        CancellationToken ct = default)
    {
        // Clamp max rows for safety
        if (maxRows < 1 || maxRows > 500) maxRows = 200;

        var safePrefixParam = (prefix ?? "").Trim().ToUpperInvariant();

        // When a prefix is given, filter by vpo_number LIKE 'PREFIX%'.
        // Otherwise return the most recently completed VPOs.
        const string sql = """
            SELECT TOP (@maxRows)
                v.vpo_number                                AS VpoNumber,
                ISNULL(v.status,             '')            AS Status,
                ISNULL(t.processing_site_id, '')            AS Site,
                ISNULL(t.operation_code,     '')            AS Operation,
                ISNULL(t.task_name,          '')            AS Flow,
                t.complete_date                             AS TestEndDate,
                ISNULL(t.step_in_quantity,   0)             AS Tested,
                ISNULL(t.step_out_quantity,  0)             AS Good,
                ISNULL(v.description,        '')            AS Description
            FROM  vortex_dbo.vw_vpo v
            LEFT  JOIN vortex_dbo.vw_public_task t
                   ON t.task_id = v.current_task_id
            WHERE (@prefix = '' OR v.vpo_number LIKE @prefixLike)
            ORDER BY t.complete_date DESC, v.vpo_number;
            """;

        using var conn = _factory.CreateConnection();
        var rows = await conn.QueryAsync<VpoListRow>(
            new CommandDefinition(
                sql,
                new
                {
                    maxRows,
                    prefix    = safePrefixParam,
                    prefixLike = safePrefixParam + "%"
                },
                cancellationToken: ct));

        return rows.AsList();
    }
}
