using System;
using System.Data;

namespace PointZilla.DbClient
{
    public interface IDbClient : IDisposable
    {
        DataTable ExecuteTable(string query);
    }
}
