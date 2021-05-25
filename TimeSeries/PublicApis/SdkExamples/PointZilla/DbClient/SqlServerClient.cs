using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace PointZilla.DbClient
{
    public class SqlServerClient : DbClientBase
    {
        public SqlServerClient(string connectionString)
        {
            DbConnection = new SqlConnection(connectionString);
            DbConnection.Open();
        }

        protected override DbDataAdapter CreateAdapter(IDbCommand cmd)
        {
            return new SqlDataAdapter((SqlCommand)cmd);
        }
    }
}
