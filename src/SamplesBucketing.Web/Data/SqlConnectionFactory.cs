using System.Data;
using Microsoft.Data.SqlClient;

namespace SamplesBucketing.Web.Data;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
