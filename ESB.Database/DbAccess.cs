/*
 * This class, DbAccess, is used to manage SQLite database connections and operations.
 * It provides the ability to reuse a connection or create a new one for each operation.
 * 
 * Fields:
 * - _connection: Represents the SQLite database connection.
 * - _reuseConnection: A boolean flag indicating whether to reuse the connection or not.
 * 
 * Constructor:
 * - DbAccess(string connectionString, bool reuseConnection): Initializes a new instance of the DbAccess class.
 *   If reuseConnection is true, the connection is opened immediately.
 * 
 * Methods:
 * - DoWork(Action<SQLiteConnection> work): Executes the provided work on the database connection.
 *   If _reuseConnection is false, a new connection is opened and closed for each operation.
 * - Open(string connectionString): Opens a new database connection with the provided connection string.
 *   If a connection is already open, an InvalidOperationException is thrown.
 * - CloseConnection(): Closes the database connection if it is not already closed.
 * - CreateDatabaseFile(string folderPath, string dbName): Creates a new SQLite database file at the specified location.
 * - ExecuteQuery(string query): Executes the provided SQL query and returns the result as a DataTable.
 * - ExecuteNonQuery(string query): Executes the provided SQL non-query and returns the number of affected rows.
 * - ExecuteScalar(string query): Executes the provided SQL query and returns the first column of the first row in the result set.
 */

using System.Data.SQLite;
using System.Data;
using System;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;

namespace ESB.Database
{
    public class DbAccess : IDbAccess
    {
        readonly private SQLiteConnection _connection;
        readonly private bool _reuseConnection;

        public DbAccess(string connectionString, bool reuseConnection)
        {
            _connection = new SQLiteConnection(connectionString);
            _reuseConnection = reuseConnection;
            if (_reuseConnection)
            {
                _connection.Open();
            }
        }

        ~DbAccess() // Finalizer
        {
            CloseConnection();
        }

        private void DoWork(Action<SQLiteConnection> work)
        {
            if (!_reuseConnection)
            {
                _connection.Open();
            }

            try
            {
                work(_connection);
            }
            finally
            {
                if (!_reuseConnection)
                {
                    _connection.Close();
                }
            }
        }


        public void CloseConnection()
        {
            if (_connection != null && _connection.State != System.Data.ConnectionState.Closed)
            {
                _connection.Close();
            }
        }

        public void CreateDatabaseFile(string folderPath, string dbName)
        {
            string dbPath = Path.Combine(folderPath, dbName);

            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }
        }

        public void ExecuteCommand(string sql, params object[] parameters)
        {
            DoWork(connection =>
            {
                using (var command = new SQLiteCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            command.Parameters.AddWithValue($"@p{i + 1}", parameters[i]);
                        }
                    }

                    command.ExecuteNonQuery();
                }
            });
        }

        public DataTable ExecuteSelect(string sql, params object[] parameters)
        {
            DataTable dt = new DataTable();

            DoWork(connection =>
            {
                using (var command = new SQLiteCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            command.Parameters.AddWithValue($"@p{i + 1}", parameters[i]);
                        }
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            });

            return dt;
        }

        //public DataTable ExecuteQuery(string query)
        //{
        //    DataTable result = new DataTable();

        //    DoWork(connection =>
        //    {
        //        using (var command = new SQLiteCommand(query, connection))
        //        {
        //            using (var adapter = new SQLiteDataAdapter(command))
        //            {
        //                adapter.Fill(result);
        //            }
        //        }
        //    });

        //    return result;
        //}

        //public int ExecuteNonQuery(string query)
        //{
        //    int affectedRows = 0;

        //    DoWork(connection =>
        //    {
        //        using (var command = new SQLiteCommand(query, connection))
        //        {
        //            affectedRows = command.ExecuteNonQuery();
        //        }
        //    });

        //    return affectedRows;
        //}

        //public object ExecuteScalar(string query)
        //{
        //    object result = null;

        //    DoWork(connection =>
        //    {
        //        using (var command = new SQLiteCommand(query, connection))
        //        {
        //            result = command.ExecuteScalar();
        //        }
        //    });

        //    return result;
        //}

        public void JsonDataset(JObject json, string datasetName, string sql, params SQLiteParameter[] parameters)
        {
            DoWork(connection =>
            {
                using (var command = new SQLiteCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        var rows = new JArray();
                        while (reader.Read())
                        {
                            var row = new JObject();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = JToken.FromObject(reader.GetValue(i));
                            }
                            rows.Add(row);
                        }
                        json[datasetName] = rows;
                    }
                }
            });
        }

        public void Import(string filename)
        {
            var json = File.ReadAllText(filename);
            var data = JArray.Parse(json);
            var tableName = Path.GetFileNameWithoutExtension(filename);

            ExecuteCommand($"DELETE FROM {tableName};");

            foreach (var item in data)
            {
                var columnNames = item.Children<JProperty>().Select(p => p.Name).ToList();
                var values = item.Children<JProperty>().Select(p => p.Value.ToString()).ToList();
                var insertQuery = $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", values.Select(v => $"'{v}'"))});";
                ExecuteCommand(insertQuery);
            }
        }
    }
}