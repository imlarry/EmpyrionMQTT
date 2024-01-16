using System.Data.SQLite;
using System.Data;
using System.IO;

namespace ESB.Database
{
    public class DatabaseManager
    {
        readonly private SQLiteConnection _connection;

        public DatabaseManager(string connectionString)
        {
            _connection = new SQLiteConnection(connectionString);
        }

        public void CreateDatabaseFile(string folderPath, string dbName)
        {
            string dbPath = Path.Combine(folderPath, dbName);

            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }
        }

        public void OpenConnection()
        {
            _connection.Open();
        }

        public void CloseConnection()
        {
            _connection.Close();
        }

        public DataTable ExecuteQuery(string query)
        {
            using (var command = new SQLiteCommand(query, _connection))
            {
                using (var adapter = new SQLiteDataAdapter(command))
                {
                    var result = new DataTable();
                    adapter.Fill(result);
                    return result;
                }
            }
        }

        public int ExecuteNonQuery(string query)
        {
            using (var command = new SQLiteCommand(query, _connection))
            {
                return command.ExecuteNonQuery();
            }
        }

        public object ExecuteScalar(string query)
        {
            using (var command = new SQLiteCommand(query, _connection))
            {
                return command.ExecuteScalar();
            }
        }
    }
}
