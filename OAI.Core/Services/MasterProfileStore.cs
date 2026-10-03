using System.Text.Json;
using Microsoft.Data.Sqlite;
using OAI.Core.Models;

namespace OAI.Core.Services;

public class MasterProfileStore
{
    private readonly string _dbPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MasterProfileStore(string? dbPath = null)
    {
        _dbPath = dbPath ?? DefaultPath();
        EnsureSchema();
    }

    public static string DefaultPath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OAI");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "oai_master.db");
    }

    public void Save(MasterUserProfile profile)
    {
        profile.LastUpdated = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DELETE FROM OaiMasterProfile;
INSERT INTO OaiMasterProfile (Id, ProfileJson, LastUpdated) VALUES (1, $json, $updated);";
        cmd.Parameters.AddWithValue("$json", json);
        cmd.Parameters.AddWithValue("$updated", profile.LastUpdated.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public MasterUserProfile? Load()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ProfileJson FROM OaiMasterProfile WHERE Id = 1 LIMIT 1;";
        var value = cmd.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return JsonSerializer.Deserialize<MasterUserProfile>(value, JsonOptions);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS OaiMasterProfile (
    Id INTEGER PRIMARY KEY,
    ProfileJson TEXT NOT NULL,
    LastUpdated TEXT NOT NULL
);";
        cmd.ExecuteNonQuery();
    }
}
