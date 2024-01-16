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
 */

using System.Data.SQLite;
using System.Data;
using System;

using Newtonsoft.Json.Linq;

namespace ESBLog.Database
{
    public class DbAccess
    {
        private SQLiteConnection _connection;
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

        public void DoWork(Action<SQLiteConnection> work)
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

        public void Open(string connectionString)
        {
            if (_connection != null)
            {
                throw new InvalidOperationException("Connection is already open.");
            }

            _connection = new SQLiteConnection(connectionString);
            _connection.Open();
        }

        public void CloseConnection()
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }
        }

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
                                row[reader.GetName(i)] = JToken.FromObject(reader.GetValue(i)); // = reader.GetValue(i).ToString();
                            }
                            rows.Add(row);
                        }
                        json[datasetName] = rows;
                    }
                }
            });
        }
    }
}