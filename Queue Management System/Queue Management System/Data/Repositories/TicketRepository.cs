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
                    service_time_seconds INTEGER,
                    served_by_user_id INTEGER REFERENCES users(id) ON DELETE SET NULL
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

        public async Task<List<Ticket>> GetTicketsByServicePoint(int servicePointId, int currentUserId)
        {
            var sql = @"
        SELECT
            t.id,
            t.ticket_number,
            t.status,
            t.created_at,
			t.service_id
        FROM tickets t
        JOIN service_points sp ON sp.id = @servicePointId
        WHERE t.service_id = sp.service_id
           AND (
      t.status = 'Waiting'  -- Shared queue for all staff
      OR
      (
          t.status IN ('Called', 'Serving')
          AND t.served_by_user_id = @currentUserId  -- Only MY tickets
      )
  )
       ORDER BY t.created_at ASC;
    ";

            return await _db.ExecuteQueryAsync(sql,
                reader => new Ticket
                {
                    Id = reader.GetInt32(0),
                    TicketNumber = reader.GetString(1),
                    Status = reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3),
                    ServiceId = reader.GetInt32(4)
                },
                new NpgsqlParameter("@servicePointId", servicePointId),
                new NpgsqlParameter("@currentUserId", currentUserId)
            );
        }

        public async Task<Dictionary<string, object>?> CallNextTicket(int servicePointId, int staffUserId)
        {
            try
            {
                var sql = @"
            WITH next_ticket AS (
                SELECT t.id
                FROM tickets t
                JOIN service_points sp ON sp.service_id = t.service_id
                WHERE sp.id = @servicePointId
                  AND t.status = 'Waiting'
                  AND t.service_point_id IS NULL
                ORDER BY t.created_at ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            UPDATE tickets t
            SET status = 'Called',
                service_point_id = @servicePointId,
                called_at = NOW(),
                served_by_user_id = @staffUserId,
                waiting_time_seconds = EXTRACT(EPOCH FROM (NOW() - t.created_at))
            FROM next_ticket nt
            WHERE t.id = nt.id
            RETURNING t.ticket_number;
        ";

                var result = await _db.ExecuteReaderAsync(sql,
                    new NpgsqlParameter("@servicePointId", servicePointId),
                    new NpgsqlParameter("@staffUserId", staffUserId));

                if (result.Count == 0) return null;

                var row = result[0];
                return new Dictionary<string, object>
                {
                    ["ticketNumber"] = row["ticket_number"].ToString()!
                };
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> UpdateTicketStatus(int servicePointId, int staffUserId, string ticketNumber, string newStatus)
        {
            try
            {
                var sql = @"
            UPDATE tickets
            SET status = @newStatus,
                finished_at = CASE WHEN @newStatus IN ('Finished', 'NoShow') THEN NOW() ELSE finished_at END,
                service_time_seconds = CASE
                    WHEN @newStatus = 'Finished' THEN EXTRACT(EPOCH FROM (NOW() - called_at))
                    ELSE service_time_seconds
                END
            WHERE ticket_number = @ticketNumber
              AND service_point_id = @servicePointId
              AND served_by_user_id = @staffUserId
              AND status IN ('Called', 'Serving')
              AND (
                  (@newStatus = 'Serving' AND status = 'Called') OR
                  (@newStatus = 'Finished' AND status = 'Serving') OR
                  (@newStatus = 'NoShow' AND status IN ('Called', 'Serving'))
              );
        ";

                var rowsAffected = await _db.ExecuteNonQueryWithResultAsync(sql,
                    new NpgsqlParameter("@ticketNumber", ticketNumber),
                    new NpgsqlParameter("@servicePointId", servicePointId),
                    new NpgsqlParameter("@staffUserId", staffUserId),
                    new NpgsqlParameter("@newStatus", newStatus));

                return rowsAffected > 0;
            }
            catch
            {
                throw;
            }
        }

        public class CalledTicketDto
        {
            public string TicketNumber { get; set; } = string.Empty;
            public int? ServicePointId { get; set; }
        }

        public async Task<List<CalledTicketDto>> GetCalledTickets()
        {
            var sql = @"
        SELECT
            t.ticket_number,
            t.service_point_id
        FROM tickets t
        WHERE t.status = 'Called'
        ORDER BY t.called_at ASC;
    ";

            return await _db.ExecuteQueryAsync(sql,
                reader => new CalledTicketDto
                {
                    TicketNumber = reader.GetString(0),
                    ServicePointId = reader.IsDBNull(1) ? null : reader.GetInt32(1)
                });
        }
    }
}