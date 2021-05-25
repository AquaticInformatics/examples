using System.Data;
using System.Data.Common;
using MySql.Data.MySqlClient;

namespace PointZilla.DbClient
{
    public class MySqlClient : DbClientBase
    {
        public MySqlClient(string connectionString)
        {
            DbConnection = new MySqlConnection(connectionString);
            DbConnection.Open();
        }

        protected override DbDataAdapter CreateAdapter(IDbCommand cmd)
        {
            return new MySqlDataAdapter((MySqlCommand)cmd);
        }
    }
}
