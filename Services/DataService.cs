using System.Text.Json;
using StickyNotes.Models;

namespace StickyNotes.Services;

public class DataService
{
    private readonly string _dbPath;
    private readonly string _configPath;
    private readonly string _dataDir;

    private const string ConnectionString = "Data Source={0}";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DataService()
    {
        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StickyNotes");
        Directory.CreateDirectory(_dataDir);

        _dbPath = Path.Combine(_dataDir, "StickyNotes.db");
        _configPath = Path.Combine(_dataDir, "settings.json");
    }

    // ─── Database Initialization ───────────────────────────────

    public async Task InitializeAsync()
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        const string sql = """
            CREATE TABLE IF NOT EXISTS Notes (
                Id TEXT PRIMARY KEY,
                Type INTEGER NOT NULL,
                Content TEXT NOT NULL,
                FileName TEXT,
                TemplateName TEXT NOT NULL DEFAULT 'Yellow',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0
            )
            """;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();

        // Migration: add missing columns
        try { using var c2 = conn.CreateCommand(); c2.CommandText = "ALTER TABLE Notes ADD COLUMN IsPinned INTEGER NOT NULL DEFAULT 0"; await c2.ExecuteNonQueryAsync(); } catch { }
        try { using var c3 = conn.CreateCommand(); c3.CommandText = "ALTER TABLE Notes ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0"; await c3.ExecuteNonQueryAsync(); } catch { }
    }

    // ─── CRUD Operations ────────────────────────────────────────

    public async Task<List<NoteItem>> GetAllNotesAsync()
    {
        var notes = new List<NoteItem>();

        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Notes ORDER BY IsPinned DESC, SortOrder DESC, CreatedAt DESC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notes.Add(MapNote(reader));
        }

        return notes;
    }

    public async Task<NoteItem?> GetNoteByIdAsync(string id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Notes WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return MapNote(reader);

        return null;
    }

    public async Task InsertNoteAsync(NoteItem note)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Notes (Id, Type, Content, FileName, TemplateName, CreatedAt, UpdatedAt, IsPinned, SortOrder)
            VALUES (@id, @type, @content, @fileName, @template, @createdAt, @updatedAt, @isPinned, @sortOrder)
            """;

        cmd.Parameters.AddWithValue("@id", note.Id);
        cmd.Parameters.AddWithValue("@type", (int)note.Type);
        cmd.Parameters.AddWithValue("@content", note.Content);
        cmd.Parameters.AddWithValue("@fileName", (object?)note.FileName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@template", note.TemplateName);
        cmd.Parameters.AddWithValue("@createdAt", note.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", note.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@isPinned", note.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@sortOrder", note.SortOrder);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateNoteAsync(NoteItem note)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Notes
            SET Content = @content, FileName = @fileName, TemplateName = @template,
                UpdatedAt = @updatedAt, IsPinned = @isPinned, SortOrder = @sortOrder
            WHERE Id = @id
            """;

        cmd.Parameters.AddWithValue("@id", note.Id);
        cmd.Parameters.AddWithValue("@content", note.Content);
        cmd.Parameters.AddWithValue("@fileName", (object?)note.FileName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@template", note.TemplateName);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("O"));
        cmd.Parameters.AddWithValue("@isPinned", note.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@sortOrder", note.SortOrder);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteNoteAsync(string id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Notes WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    // ─── History Cleanup ────────────────────────────────────────

    public async Task<int> CleanupOldNotesAsync(int retentionDays = 7)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Notes WHERE CreatedAt < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", DateTime.Now.AddDays(-retentionDays).ToString("O"));

        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetOldNotesCountAsync(int retentionDays = 7)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Notes WHERE CreatedAt < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", DateTime.Now.AddDays(-retentionDays).ToString("O"));

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ─── Settings ───────────────────────────────────────────────

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_configPath))
            return new AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_configPath, json);
    }

    /// <summary>
    /// Synchronous load for critical paths (e.g., window constructor before first render).
    /// </summary>
    public AppSettings LoadSettings()
    {
        if (!File.Exists(_configPath))
            return new AppSettings();
        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Synchronous save for critical paths (e.g., window close) where
    /// fire-and-forget would risk the write not completing before process exit.
    /// </summary>
    public void SaveSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    // ─── Bulk Sort Order Update ────────────────────────────────

    public async Task UpdateSortOrdersAsync(List<NoteItem> notes)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            note.SortOrder = (notes.Count - i) * 1000; // ensure unique descending order
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Notes SET SortOrder = @s WHERE Id = @id";
            cmd.Parameters.AddWithValue("@s", note.SortOrder);
            cmd.Parameters.AddWithValue("@id", note.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }

    // ─── Helpers ────────────────────────────────────────────────

    private Microsoft.Data.Sqlite.SqliteConnection GetConnection()
        => new(string.Format(ConnectionString, _dbPath));

    private static NoteItem MapNote(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new NoteItem
        {
            Id = reader.GetString(0),
            Type = (NoteType)reader.GetInt32(1),
            Content = reader.GetString(2),
            FileName = reader.IsDBNull(3) ? null : reader.GetString(3),
            TemplateName = reader.GetString(4),
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            UpdatedAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
            IsPinned = reader.IsDBNull(7) ? false : reader.GetInt32(7) == 1,
            SortOrder = reader.IsDBNull(8) ? 0 : reader.GetInt64(8)
        };
    }
}