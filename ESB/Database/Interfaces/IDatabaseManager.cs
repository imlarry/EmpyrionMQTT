using System.Data;
    
namespace ESB.Database
{
    public interface IDatabaseManager
    {
        void CreateDatabaseFile(string folderPath, string dbName);
        void OpenConnection();
        void CloseConnection();
        DataTable ExecuteQuery(string query);
        int ExecuteNonQuery(string query);
        object ExecuteScalar(string query);
    }
}
