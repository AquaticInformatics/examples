using System.Data;
using System.Data.Common;
using System.Data.Odbc;

namespace PointZilla.DbClient
{
    public class OdbcClient : DbClientBase
    {
        public OdbcClient(string connectionString)
        {
            DbConnection = new OdbcConnection(connectionString);
            DbConnection.Open();
        }

        protected override DbDataAdapter CreateAdapter(IDbCommand cmd)
        {
            return new OdbcDataAdapter((OdbcCommand)cmd);
        }
    }
}
