using System.Data;
using System.Data.SQLite;
using Newtonsoft.Json.Linq;

namespace ESB.Database
{
    public interface IDbAccess
    {
        void ExecuteCommand(string sql, params object[] parameters);
        DataTable ExecuteSelect(string sql, params object[] parameters);
        void CreateDatabaseFile(string folderPath, string dbName);
        void JsonDataset(JObject json, string datasetName, string sql, params SQLiteParameter[] parameters);
    }
}