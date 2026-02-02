using Queue_Management_System.Data;
using Queue_Management_System.Models;
using Npgsql;

namespace Queue_Management_System.Data.Repositories
{
	public class UserRepository
	{
		private readonly DatabaseService _db;
		private readonly ILogger<UserRepository> _logger;

		public UserRepository(DatabaseService db, ILogger<UserRepository> logger)
		{
			_db = db;
			_logger = logger;
		}

		public async Task CreateTable()
		{
			var sql = @"
                CREATE TABLE IF NOT EXISTS users (
                    id SERIAL PRIMARY KEY,
                    username VARCHAR(50) UNIQUE NOT NULL,
					email VARCHAR(100) UNIQUE NOT NULL,
                    password_hash VARCHAR(255) NOT NULL,
                    role VARCHAR(20) NOT NULL,
                    service_point_id INTEGER REFERENCES service_points(id) ON DELETE SET NULL,
                    created_at TIMESTAMP DEFAULT NOW()
                );
            ";

			await _db.ExecuteNonQueryAsync(sql);
		}

		public async Task<User?> GetUserByUsernameOrEmail(string usernameOrEmail)
		{
			var sql = @"
        SELECT id, username, email, password_hash, role
        FROM users
        WHERE username = @usernameOrEmail OR email = @usernameOrEmail
        LIMIT 1;
    ";

			// Execute query using your existing DatabaseService
			var results = await _db.ExecuteReaderAsync(sql,
				new NpgsqlParameter("@usernameOrEmail", usernameOrEmail));

			if (results.Count == 0)
				return null;

			var row = results[0];

			return new User
			{
				Id = (int)row["id"]!,
				Username = row["username"]!.ToString()!,
				Email = row["email"]!.ToString()!,
				PasswordHash = row["password_hash"]!.ToString()!,
				Role = row["role"]!.ToString()!
			};
		}
		public async Task<int> AddUser(User user)
		{
			var sql = @"
        INSERT INTO users (username, email, password_hash, role, created_at)
        VALUES (@username, @email, @password_hash, @role, NOW())
        RETURNING id;
    ";

			return await _db.ExecuteScalarAsync<int>(sql,
				new NpgsqlParameter("@username", user.Username),
				new NpgsqlParameter("@email", user.Email),
				new NpgsqlParameter("@password_hash", user.PasswordHash),
				new NpgsqlParameter("@role", user.Role));
		}
		public async Task<List<User>> GetAllUsers()
		{
			var sql = @"
        SELECT
            u.id,
            u.username,
            u.email,
            u.role,
            u.service_point_id,
            sp.name as service_point_name,
            u.created_at
        FROM users u
        LEFT JOIN service_points sp ON u.service_point_id = sp.id
        ORDER BY u.created_at DESC;
    ";

			return await _db.ExecuteQueryAsync(sql, reader => new User
			{
				Id = reader.GetInt32(0),
				Username = reader.GetString(1),
				Email = reader.GetString(2),
				Role = reader.GetString(3),
				ServicePointId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
				ServicePointName = reader.IsDBNull(5) ? null : reader.GetString(5),
				// Note: CreatedAt property doesn't exist in your User model
			});
		}

		public async Task UpdateUserServicePoint(int userId, int? servicePointId)
		{
			var sql = @"
        UPDATE users
        SET service_point_id = @servicePointId
        WHERE id = @userId;
    ";

			await _db.ExecuteNonQueryAsync(sql,
				new NpgsqlParameter("@userId", userId),
				new NpgsqlParameter("@servicePointId",
					servicePointId.HasValue ? (object)servicePointId.Value : DBNull.Value));
		}

		public async Task<User?> GetUserById(int userId)
		{
			var sql = @"
        SELECT id, username, email, password_hash, role, service_point_id
        FROM users
        WHERE id = @userId;
    ";

			var results = await _db.ExecuteReaderAsync(sql,
				new NpgsqlParameter("@userId", userId));

			if (results.Count == 0)
				return null;

			var row = results[0];

			return new User
			{
				Id = (int)row["id"]!,
				Username = row["username"]!.ToString()!,
				Email = row["email"]!.ToString()!,
				PasswordHash = row["password_hash"]!.ToString()!,
				Role = row["role"]!.ToString()!,
				ServicePointId = row["service_point_id"] != DBNull.Value ? (int?)row["service_point_id"] : null
			};
		}
		public async Task<User?> GetStaffWithServicePoint(int userId)
		{
			var sql = @"
        SELECT
            u.id,
            u.username,
            u.service_point_id,
            sp.name as service_point_name,
			sp.is_active as service_point_is_active
        FROM users u
        LEFT JOIN service_points sp ON u.service_point_id = sp.id
        WHERE u.id = @userId AND u.role = 'Staff';
    ";

			var results = await _db.ExecuteReaderAsync(sql,
				new NpgsqlParameter("@userId", userId));
			

			if (results.Count == 0)
			{
				_logger.LogWarning($"No staff found for ID {userId}");
				return null;
			}

			var row = results[0];

			return new User
			{
				Id = (int)row["id"]!,
				Username = row["username"]!.ToString()!,
				ServicePointId = row["service_point_id"] != null && row["service_point_id"] != DBNull.Value ? (int?)row["service_point_id"] : null,
				ServicePointName = row["service_point_name"] != null && row["service_point_name"] != DBNull.Value ? row["service_point_name"]!.ToString()! : null,
				ServicePointIsActive = row["service_point_is_active"] != null && row["service_point_is_active"] != DBNull.Value ? (bool)row["service_point_is_active"] : false
			};
		}




	}
}