using Queue_Management_System.Data;
using Queue_Management_System.Models;
using Npgsql;

namespace Queue_Management_System.Data.Repositories
{
	public class UserRepository
	{
		private readonly DatabaseService _db;

		public UserRepository(DatabaseService db)
		{
			_db = db;
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
                    service_point_id INTEGER,
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
		



	}
}