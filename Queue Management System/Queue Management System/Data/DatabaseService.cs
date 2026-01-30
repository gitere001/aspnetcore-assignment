using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Queue_Management_System.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            Console.WriteLine(string.IsNullOrEmpty(_connectionString)
       ? "❌ Connection string NOT loaded"
       : "✅ Connection string loaded successfully");
        }

        public async Task<string> TestConnection()
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();
                return "✅ SUCCESS: Connected to Neon PostgreSQL!";
            }
            catch
            {
                return "❌ FAILED: Could not connect to Neon PostgreSQL.";
            }
        }

        private NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }




        // For CREATE / UPDATE / DELETE
        public async Task ExecuteNonQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);

            if (parameters?.Length > 0)
                command.Parameters.AddRange(parameters);

            await command.ExecuteNonQueryAsync();
        }

        // For INSERT ... RETURNING id
        public async Task<T> ExecuteScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);

            if (parameters?.Length > 0)
                command.Parameters.AddRange(parameters);

            var result = await command.ExecuteScalarAsync();
            return (T)result!;
        }

        public async Task<List<T>> ExecuteQueryAsync<T>(string sql, Func<NpgsqlDataReader, T> mapper, params NpgsqlParameter[] parameters)
        {
            var results = new List<T>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);

            if (parameters?.Length > 0)
                command.Parameters.AddRange(parameters);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(mapper(reader));
            }

            return results;
        }

        // For SELECT queries - returns raw data as dictionary
        public async Task<List<Dictionary<string, object>>> ExecuteReaderAsync(string sql, params NpgsqlParameter[] parameters)
        {
            var results = new List<Dictionary<string, object>>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);

            if (parameters?.Length > 0)
                command.Parameters.AddRange(parameters);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }

                results.Add(row);
            }

            return results;
        }

    }
}