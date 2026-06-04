using System.IO;
using Microsoft.Data.Sqlite;

namespace etch_ui.Services;

/// <summary>WPF 로컬 SQLite 텔레메트리 샘플 (Flask 미연결 시에도 이력 보존).</summary>
public sealed class HmiTelemetryStore
{
    private readonly string _connectionString;

    public HmiTelemetryStore(string dbPath)
    {
        string dir = Path.GetDirectoryName(dbPath)!;
        Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using SqliteConnection connection = new(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS telemetry_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT NOT NULL,
                data_source TEXT NOT NULL,
                equipment_state TEXT,
                alarm_code TEXT,
                temperature REAL,
                humidity REAL,
                pressure_mtorr REAL,
                vibration_g REAL,
                interlock_ok INTEGER,
                maintenance_mode INTEGER,
                username TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_telemetry_created ON telemetry_samples(created_at);
            """;
        command.ExecuteNonQuery();
    }

    public void InsertSample(
        string dataSource,
        string equipmentState,
        string? alarmCode,
        double temperature,
        double humidity,
        double pressureMtorr,
        double vibrationG,
        bool interlockOk,
        bool maintenanceMode,
        string? username)
    {
        using SqliteConnection connection = new(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO telemetry_samples (
                created_at, data_source, equipment_state, alarm_code,
                temperature, humidity, pressure_mtorr, vibration_g,
                interlock_ok, maintenance_mode, username
            ) VALUES ($created_at, $data_source, $equipment_state, $alarm_code,
                $temperature, $humidity, $pressure, $vibration,
                $interlock_ok, $maintenance_mode, $username)
            """;
        command.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o"));
        command.Parameters.AddWithValue("$data_source", dataSource);
        command.Parameters.AddWithValue("$equipment_state", equipmentState);
        command.Parameters.AddWithValue("$alarm_code", (object?)alarmCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$temperature", temperature);
        command.Parameters.AddWithValue("$humidity", humidity);
        command.Parameters.AddWithValue("$pressure", pressureMtorr);
        command.Parameters.AddWithValue("$vibration", vibrationG);
        command.Parameters.AddWithValue("$interlock_ok", interlockOk ? 1 : 0);
        command.Parameters.AddWithValue("$maintenance_mode", maintenanceMode ? 1 : 0);
        command.Parameters.AddWithValue("$username", (object?)username ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
