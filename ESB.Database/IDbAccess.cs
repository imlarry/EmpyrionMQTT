using System;
using System.Data.SQLite;
using Newtonsoft.Json.Linq;

namespace ESBLog.Database
{
    public interface IDbAccess
    {
        void DoWork(Action<SQLiteConnection> work);
        void Open(string connectionString);
        void CloseConnection();
        void JsonDataset(JObject json, string datasetName, string sql, params SQLiteParameter[] parameters);
    }
}