using Queue_Management_System.Data;
using Queue_Management_System.Models;
using Npgsql;

namespace Queue_Management_System.Data.Repositories
{
    public class ServicePointRepository
    {
        private readonly DatabaseService _db;

        public ServicePointRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task CreateTable()
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS service_points (
                    id SERIAL PRIMARY KEY,
                    name VARCHAR(50) NOT NULL,
                    is_active BOOLEAN DEFAULT true,
                    created_at TIMESTAMP DEFAULT NOW()
                );
            ";

            await _db.ExecuteNonQueryAsync(sql);
        }

        public async Task<int> AddServicePoint(ServicePoint servicePoint)
        {
            var sql = @"
                INSERT INTO service_points (name, is_active)
                VALUES (@name, @is_active)
                RETURNING id;
            ";

            return await _db.ExecuteScalarAsync<int>(sql,
                new NpgsqlParameter("@name", servicePoint.Name),
                new NpgsqlParameter("@is_active", servicePoint.IsActive));
        }

        public async Task<List<ServicePoint>> GetAllServicePoints()
        {
            var sql = @"
                SELECT id, name, is_active, created_at
                FROM service_points
                ORDER BY name;
            ";

            return await _db.ExecuteQueryAsync(
                sql,
                reader => new ServicePoint
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    IsActive = reader.GetBoolean(2),
                    CreatedAt = reader.GetDateTime(3)
                }
            );
        }

        public async Task<List<ServicePoint>> GetActiveServicePoints()
        {
            var sql = @"
                SELECT id, name, is_active, created_at
                FROM service_points
                WHERE is_active = true
                ORDER BY name;
            ";

            return await _db.ExecuteQueryAsync(
                sql,
                reader => new ServicePoint
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    IsActive = reader.GetBoolean(2),
                    CreatedAt = reader.GetDateTime(3)
                }
            );
        }

        public async Task UpdateServicePoint(ServicePoint servicePoint)
        {
            var sql = @"
                UPDATE service_points
                SET name = @name,
                    is_active = @is_active
                WHERE id = @id;
            ";

            await _db.ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("@name", servicePoint.Name),
                new NpgsqlParameter("@is_active", servicePoint.IsActive),
                new NpgsqlParameter("@id", servicePoint.Id));
        }
    }
}