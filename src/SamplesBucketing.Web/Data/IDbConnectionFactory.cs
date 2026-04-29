using System.Data;

namespace SamplesBucketing.Web.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
