namespace PointZilla.DbClient
{
    public static class DbClientFactory
    {
        public static IDbClient CreateOpened(DbType dbType, string connectionString)
        {
            switch (dbType)
            {
                case DbType.SqlServer:
                    return new SqlServerClient(connectionString);
                case DbType.Postgres:
                    return new PostgresDbClient(connectionString);
                case DbType.MySql:
                    return new MySqlClient(connectionString);
                case DbType.Odbc:
                    return new OdbcClient(connectionString);
            }

            throw new ExpectedException($"Unknown database type {dbType}");
        }
    }
}
