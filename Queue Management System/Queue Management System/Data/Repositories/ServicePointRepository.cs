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
                    service_id INTEGER NOT NULL REFERENCES services(id),
                    is_active BOOLEAN DEFAULT true,
                    created_at TIMESTAMP DEFAULT NOW()
                );
            ";

            await _db.ExecuteNonQueryAsync(sql);
        }

        public async Task<int> AddServicePoint(ServicePoint servicePoint)
        {
            var sql = @"
                INSERT INTO service_points (name, service_id, is_active)
                VALUES (@name, @service_id, @is_active)
                RETURNING id;
            ";

            return await _db.ExecuteScalarAsync<int>(sql,
                new NpgsqlParameter("@name", servicePoint.Name),
                new NpgsqlParameter("@service_id", servicePoint.ServiceId),
                new NpgsqlParameter("@is_active", servicePoint.IsActive));
        }

        public async Task<List<ServicePoint>> GetAllServicePoints()
        {
            var sql = @"
        SELECT
            sp.id,
            sp.name,
            sp.service_id,
            sp.is_active,
            sp.created_at,
            s.name as service_name,      -- From JOIN
            s.prefix_code as prefix_code -- From JOIN
        FROM service_points sp
        JOIN services s ON sp.service_id = s.id
        ORDER BY sp.name;
    ";

            return await _db.ExecuteQueryAsync(
                sql,
                reader => new ServicePoint
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),          // Counter name
                    ServiceId = reader.GetInt32(2),      // Foreign key
                    IsActive = reader.GetBoolean(3),
                    CreatedAt = reader.GetDateTime(4),
                    ServiceName = reader.GetString(5),   // Service name from JOIN
                    ServicePrefix = reader.GetString(6)  // Prefix from JOIN
                }
            );
        }

        public async Task<List<ServicePoint>> GetActiveServicePoints()
        {
            var sql = @"
        SELECT
            sp.id,
            sp.name,
            sp.service_id,
            sp.is_active,
            sp.created_at,
            s.name as service_name,
            s.prefix_code as prefix_code
        FROM service_points sp
        JOIN services s ON sp.service_id = s.id
        WHERE sp.is_active = true
        ORDER BY sp.name;
    ";

            return await _db.ExecuteQueryAsync(
                sql,
                reader => new ServicePoint
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ServiceId = reader.GetInt32(2),
                    IsActive = reader.GetBoolean(3),
                    CreatedAt = reader.GetDateTime(4),
                    ServiceName = reader.GetString(5),
                    ServicePrefix = reader.GetString(6)
                }
            );
        }

        public async Task UpdateServicePoint(ServicePoint servicePoint)
        {
            var sql = @"
                        UPDATE service_points
                        SET name = @name,
                            service_id = @service_id,  -- ← ADD THIS!
                            is_active = @is_active
                        WHERE id = @id;
                        ";

            await _db.ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("@name", servicePoint.Name),
                new NpgsqlParameter("@service_id", servicePoint.ServiceId), // ← ADD THIS!
                new NpgsqlParameter("@is_active", servicePoint.IsActive),
                new NpgsqlParameter("@id", servicePoint.Id));
        }
        public async Task<ServicePoint?> GetServicePointById(int id)
        {
            var sql = @"
        SELECT
            sp.id,
            sp.name,
            sp.service_id,
            sp.is_active,
            sp.created_at,
            s.name as service_name,
            s.prefix_code as prefix_code
        FROM service_points sp
        JOIN services s ON sp.service_id = s.id
        WHERE sp.id = @id;
    ";

            var points = await _db.ExecuteQueryAsync(
                sql,
                reader => new ServicePoint
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ServiceId = reader.GetInt32(2),
                    IsActive = reader.GetBoolean(3),
                    CreatedAt = reader.GetDateTime(4),
                    ServiceName = reader.GetString(5),
                    ServicePrefix = reader.GetString(6)
                },
                new NpgsqlParameter("@id", id)
            );

            return points.FirstOrDefault();
        }
        public async Task<List<ServicePointDropdownDto>> GetServicePointsForDropdown()
{
    var sql = @"
        SELECT
            sp.id,
            sp.name,
            sp.is_active,
            CASE WHEN u.id IS NOT NULL THEN true ELSE false END as is_assigned
        FROM service_points sp
        LEFT JOIN users u ON sp.id = u.service_point_id AND u.role = 'Staff'
        ORDER BY sp.name;
    ";

    return await _db.ExecuteQueryAsync(
        sql,
        reader => new ServicePointDropdownDto
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            IsActive = reader.GetBoolean(2),
            IsAssigned = reader.GetBoolean(3)
        }
    );
}
    }
}