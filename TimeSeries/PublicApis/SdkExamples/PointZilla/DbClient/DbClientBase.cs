using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using ServiceStack.Logging;

namespace PointZilla.DbClient
{
    public abstract class DbClientBase : IDbClient
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IDbConnection DbConnection;
        private IDbTransaction Transaction { get; set; }

        public virtual DataTable ExecuteTable(string query)
        {
            using (var cmd = CreateNoTimedOutCommand())
            {
                cmd.CommandText = query;
                using (var adapter = CreateAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);

                    return table;
                }
            }
        }

        protected IDbCommand CreateNoTimedOutCommand()
        {
            var cmd = DbConnection.CreateCommand();
            cmd.CommandTimeout = 0;
            cmd.Transaction = Transaction;

            return cmd;
        }

        protected abstract DbDataAdapter CreateAdapter(IDbCommand cmd);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DbConnection == null)
                return;

            using (DbConnection) { }
            DbConnection = null;
        }
    }
}
