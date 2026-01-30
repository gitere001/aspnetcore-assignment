using Queue_Management_System.Data;
using Queue_Management_System.Models;
using Npgsql;

namespace Queue_Management_System.Data.Repositories
{
	public class TicketRepository
	{
		private readonly DatabaseService _db;

		public TicketRepository(DatabaseService db)
		{
			_db = db;
		}

		public async Task CreateTable()
		{
			var sql = @"
                CREATE TABLE IF NOT EXISTS tickets (
                    id SERIAL PRIMARY KEY,
                    ticket_number VARCHAR(10) NOT NULL,
                    service_id INTEGER REFERENCES services(id),
                    service_point_id INTEGER,
                    status VARCHAR(20) DEFAULT 'Waiting',
                    created_at TIMESTAMP DEFAULT NOW(),
                    called_at TIMESTAMP,
                    finished_at TIMESTAMP,
                    waiting_time_seconds INTEGER,
                    service_time_seconds INTEGER
                );
            ";

			await _db.ExecuteNonQueryAsync(sql);
		}

		public async Task<int> AddTicket(Ticket ticket)
		{
			var sql = @"
                INSERT INTO tickets (ticket_number, service_id, status)
                VALUES (@ticket_number, @service_id, @status)
                RETURNING id;
            ";

			return await _db.ExecuteScalarAsync<int>(sql,
				new NpgsqlParameter("@ticket_number", ticket.TicketNumber),
				new NpgsqlParameter("@service_id", ticket.ServiceId),
				new NpgsqlParameter("@status", ticket.Status));
		}

		public async Task<Ticket?> GetLastTicketByPrefix(string prefix)
		{
			var sql = @"
        SELECT id, ticket_number, service_id, service_point_id, status,
               created_at, called_at, finished_at, waiting_time_seconds, service_time_seconds
        FROM tickets
        WHERE ticket_number LIKE @prefix || '%'
        ORDER BY id DESC
        LIMIT 1;
    ";

			var result = await _db.ExecuteReaderAsync(sql,
				new NpgsqlParameter("@prefix", prefix));

			if (result.Count == 0) return null;

			var row = result[0];
			return new Ticket
			{
				Id = (int)row["id"],
				TicketNumber = row["ticket_number"].ToString()!,
				ServiceId = (int)row["service_id"],
				ServicePointId = row["service_point_id"] as int?,
				Status = row["status"].ToString()!,
				CreatedAt = (DateTime)row["created_at"],
				CalledAt = row["called_at"] as DateTime?,
				FinishedAt = row["finished_at"] as DateTime?,
				WaitingTimeSeconds = row["waiting_time_seconds"] as int?,
				ServiceTimeSeconds = row["service_time_seconds"] as int?
			};
		}

		public async Task<Ticket?> GetById(int id)
		{
			var sql = @"
        SELECT id, ticket_number, service_id, service_point_id, status,
               created_at, called_at, finished_at, waiting_time_seconds, service_time_seconds
        FROM tickets
        WHERE id = @id;
    ";

			var result = await _db.ExecuteReaderAsync(sql,
				new NpgsqlParameter("@id", id));

			if (result.Count == 0) return null;

			var row = result[0];
			return new Ticket
			{
				Id = (int)row["id"],
				TicketNumber = row["ticket_number"].ToString()!,
				ServiceId = (int)row["service_id"],
				ServicePointId = row["service_point_id"] as int?,
				Status = row["status"].ToString()!,
				CreatedAt = (DateTime)row["created_at"],
				CalledAt = row["called_at"] as DateTime?,
				FinishedAt = row["finished_at"] as DateTime?,
				WaitingTimeSeconds = row["waiting_time_seconds"] as int?,
				ServiceTimeSeconds = row["service_time_seconds"] as int?
			};
		}
	}
}