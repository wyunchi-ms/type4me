using System.IO;
using Microsoft.Data.Sqlite;
using Type4Me.Services;

namespace Type4Me.Database;

/// <summary>
/// SQLite-based recognition history store.
/// Thread-safe via SemaphoreSlim.
/// </summary>
public sealed class HistoryStore : IDisposable
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SqliteConnection? _connection;

    public HistoryStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Type4Me");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "history.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS recognition_history (
                id TEXT PRIMARY KEY,
                created_at TEXT NOT NULL,
                duration_seconds REAL,
                raw_text TEXT NOT NULL,
                processing_mode TEXT,
                processed_text TEXT,
                final_text TEXT NOT NULL,
                status TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    // ── Insert ─────────────────────────────────────────────

    public async Task InsertAsync(HistoryRecord record)
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection == null) return;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO recognition_history
                    (id, created_at, duration_seconds, raw_text, processing_mode, processed_text, final_text, status)
                VALUES
                    (@id, @created_at, @duration, @raw_text, @mode, @processed_text, @final_text, @status)
                """;
            cmd.Parameters.AddWithValue("@id", record.Id);
            cmd.Parameters.AddWithValue("@created_at", record.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@duration", record.DurationSeconds);
            cmd.Parameters.AddWithValue("@raw_text", record.RawText);
            cmd.Parameters.AddWithValue("@mode", (object?)record.ProcessingMode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@processed_text", (object?)record.ProcessedText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@final_text", record.FinalText);
            cmd.Parameters.AddWithValue("@status", record.Status);
            cmd.ExecuteNonQuery();
        }
        finally { _lock.Release(); }
    }

    // ── Fetch ──────────────────────────────────────────────

    public async Task<HistoryRecord[]> FetchAllAsync(int limit = 50, int offset = 0, string? search = null)
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection == null) return [];
            using var cmd = _connection.CreateCommand();

            if (!string.IsNullOrEmpty(search))
            {
                cmd.CommandText = """
                    SELECT * FROM recognition_history
                    WHERE final_text LIKE @search OR raw_text LIKE @search
                    ORDER BY created_at DESC LIMIT @limit OFFSET @offset
                    """;
                cmd.Parameters.AddWithValue("@search", $"%{search}%");
            }
            else
            {
                cmd.CommandText = """
                    SELECT * FROM recognition_history
                    ORDER BY created_at DESC LIMIT @limit OFFSET @offset
                    """;
            }

            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            using var reader = cmd.ExecuteReader();
            var records = new List<HistoryRecord>();

            while (reader.Read())
            {
                records.Add(ReadRecord(reader));
            }

            return records.ToArray();
        }
        finally { _lock.Release(); }
    }

    public async Task<int> CountAsync(DateTime? from = null, DateTime? to = null)
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection == null) return 0;
            using var cmd = _connection.CreateCommand();

            if (from.HasValue && to.HasValue)
            {
                cmd.CommandText = "SELECT COUNT(*) FROM recognition_history WHERE created_at >= @from AND created_at <= @to";
                cmd.Parameters.AddWithValue("@from", from.Value.ToString("O"));
                cmd.Parameters.AddWithValue("@to", to.Value.ToString("O"));
            }
            else
            {
                cmd.CommandText = "SELECT COUNT(*) FROM recognition_history";
            }

            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        finally { _lock.Release(); }
    }

    // ── Delete ─────────────────────────────────────────────

    public async Task DeleteAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection == null) return;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM recognition_history WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        finally { _lock.Release(); }
    }

    public async Task DeleteAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection == null) return;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM recognition_history";
            cmd.ExecuteNonQuery();
        }
        finally { _lock.Release(); }
    }

    // ── Helpers ─────────────────────────────────────────────

    private static HistoryRecord ReadRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        CreatedAt = DateTime.TryParse(reader.GetString(1), out var dt) ? dt : DateTime.Now,
        DurationSeconds = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
        RawText = reader.GetString(3),
        ProcessingMode = reader.IsDBNull(4) ? null : reader.GetString(4),
        ProcessedText = reader.IsDBNull(5) ? null : reader.GetString(5),
        FinalText = reader.GetString(6),
        Status = reader.GetString(7),
    };

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
