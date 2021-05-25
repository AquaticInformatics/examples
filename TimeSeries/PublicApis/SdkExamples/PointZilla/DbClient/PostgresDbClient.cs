using System.Data;
using System.Data.Common;
using Npgsql;

namespace PointZilla.DbClient
{
    public class PostgresDbClient : DbClientBase
    {
        public PostgresDbClient(string connectionString)
        {
            DbConnection = new NpgsqlConnection(connectionString);
            DbConnection.Open();
        }

        protected override DbDataAdapter CreateAdapter(IDbCommand cmd)
        {
            return new NpgsqlDataAdapter((NpgsqlCommand)cmd);
        }
    }
}
