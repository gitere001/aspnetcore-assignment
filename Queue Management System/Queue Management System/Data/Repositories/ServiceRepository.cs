using Queue_Management_System.Data;
using Queue_Management_System.Models;
using Npgsql;

namespace Queue_Management_System.Data.Repositories
{
    public class ServiceRepository
    {
        private readonly DatabaseService _db;
        private Dictionary<string, int>? _serviceIdCache;
        private object _cacheLock = new object();

        public ServiceRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task CreateTable()
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS services (
                    id SERIAL PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    prefix_code VARCHAR(2) NOT NULL,
                    created_at TIMESTAMP DEFAULT NOW()
                );
            ";

            await _db.ExecuteNonQueryAsync(sql);
        }

        public async Task<int> AddService(Service service)
        {
            var sql = @"
                INSERT INTO services (name, prefix_code)
                VALUES (@name, @prefix_code)
                RETURNING id;
            ";

            return await _db.ExecuteScalarAsync<int>(sql,
                new NpgsqlParameter("@name", service.Name),
                new NpgsqlParameter("@prefix_code", service.PrefixCode));
        }

        public async Task<List<Service>> GetAllServices()
        {
            var sql = @"
                SELECT id, name, prefix_code, created_at
                FROM services
                ORDER BY name;
            ";

            return await _db.ExecuteQueryAsync(
                sql,
                reader => new Service
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    PrefixCode = reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3)
                }
            );
        }

        public async Task<Service?> GetServiceByPrefix(string prefix)
        {
            var sql = @"
                SELECT id, name, prefix_code, created_at
                FROM services
                WHERE prefix_code = @prefix
                LIMIT 1;
            ";

            var result = await _db.ExecuteReaderAsync(sql,
                new NpgsqlParameter("@prefix", prefix));

            if (result.Count == 0) return null;

            var row = result[0];
            return new Service
            {
                Id = (int)row["id"],
                Name = row["name"].ToString()!,
                PrefixCode = row["prefix_code"].ToString()!,
                CreatedAt = (DateTime)row["created_at"]
            };
        }

        // NEW: Optimized method with caching
        public async Task<int> GetServiceIdByPrefixCached(string prefix)
        {
            // Double-check locking for thread safety
            if (_serviceIdCache == null)
            {
                lock (_cacheLock)
                {
                    if (_serviceIdCache == null)
                    {
                        _serviceIdCache = new Dictionary<string, int>();
                    }
                }
            }

            // Check cache first (FAST PATH)
            lock (_cacheLock)
            {
                if (_serviceIdCache.ContainsKey(prefix))
                {
                    return _serviceIdCache[prefix];
                }
            }

            // Cache miss: get from database
            var service = await GetServiceByPrefix(prefix);
            if (service == null) return -1;

            // Update cache
            lock (_cacheLock)
            {
                _serviceIdCache[prefix] = service.Id;
            }

            return service.Id;
        }
    }
}