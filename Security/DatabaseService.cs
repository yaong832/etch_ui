using Microsoft.Data.Sqlite;
using System.IO;

namespace etch_ui.Security;

public sealed class DatabaseService
{
    public string DbPath { get; }
    private string ConnectionString => $"Data Source={DbPath}";

    public DatabaseService(string dbPath)
    {
        DbPath = dbPath;
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        string sql = """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                role TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                last_login_at TEXT
            );

            CREATE TABLE IF NOT EXISTS event_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT NOT NULL,
                username TEXT,
                state TEXT,
                code TEXT,
                message TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS alarm_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                alarm_code TEXT NOT NULL,
                occurred_at TEXT NOT NULL,
                resolved_at TEXT,
                resolved_by TEXT,
                note TEXT
            );
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void EnsureDefaultAdmin(string username, string password)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(1) FROM users WHERE username = $username";
        checkCommand.Parameters.AddWithValue("$username", username);
        long exists = (long)(checkCommand.ExecuteScalar() ?? 0);
        if (exists > 0)
        {
            return;
        }

        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO users (username, password_hash, role, is_active, created_at)
            VALUES ($username, $password_hash, $role, 1, $created_at)
            """;
        insertCommand.Parameters.AddWithValue("$username", username);
        insertCommand.Parameters.AddWithValue("$password_hash", PasswordHasher.HashPassword(password));
        insertCommand.Parameters.AddWithValue("$role", UserRole.Admin.ToString());
        insertCommand.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o"));
        insertCommand.ExecuteNonQuery();
    }

    /// <summary>데모용 작업자 계정(없을 때만 생성).</summary>
    public void EnsureDefaultWorker(string username, string password)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(1) FROM users WHERE username = $username";
        checkCommand.Parameters.AddWithValue("$username", username);
        long exists = (long)(checkCommand.ExecuteScalar() ?? 0);
        if (exists > 0)
        {
            return;
        }

        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO users (username, password_hash, role, is_active, created_at)
            VALUES ($username, $password_hash, $role, 1, $created_at)
            """;
        insertCommand.Parameters.AddWithValue("$username", username);
        insertCommand.Parameters.AddWithValue("$password_hash", PasswordHasher.HashPassword(password));
        insertCommand.Parameters.AddWithValue("$role", UserRole.Worker.ToString());
        insertCommand.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o"));
        insertCommand.ExecuteNonQuery();
    }

    public UserAccount? Authenticate(string username, string password)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, username, password_hash, role, is_active
            FROM users
            WHERE username = $username
            """;
        command.Parameters.AddWithValue("$username", username);

        int userId;
        string userName;
        UserRole role;

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                return null;
            }

            bool isActive = reader.GetInt64(4) == 1;
            if (!isActive)
            {
                return null;
            }

            string storedHash = reader.GetString(2);
            if (!PasswordHasher.VerifyPassword(password, storedHash))
            {
                return null;
            }

            string roleText = reader.GetString(3);
            role = UserRoleExtensions.ParseFromDatabase(roleText);

            userId = reader.GetInt32(0);
            userName = reader.GetString(1);
        }

        UpdateLastLogin(connection, userId);

        return new UserAccount
        {
            Id = userId,
            Username = userName,
            Role = role
        };
    }

    private static void UpdateLastLogin(SqliteConnection connection, int id)
    {
        using SqliteCommand updateCommand = connection.CreateCommand();
        updateCommand.CommandText = "UPDATE users SET last_login_at = $last_login_at WHERE id = $id";
        updateCommand.Parameters.AddWithValue("$last_login_at", DateTime.UtcNow.ToString("o"));
        updateCommand.Parameters.AddWithValue("$id", id);
        updateCommand.ExecuteNonQuery();
    }

    public void AppendEventLog(string? username, string? state, string? code, string message)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO event_logs (created_at, username, state, code, message)
            VALUES ($created_at, $username, $state, $code, $message)
            """;
        insertCommand.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o"));
        insertCommand.Parameters.AddWithValue("$username", (object?)username ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("$state", (object?)state ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("$code", (object?)code ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("$message", message);
        insertCommand.ExecuteNonQuery();
    }

    public List<UserListRow> GetAllUsers()
    {
        List<UserListRow> list = [];
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, username, role, is_active, created_at
            FROM users
            ORDER BY username COLLATE NOCASE
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new UserListRow
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Role = UserRoleExtensions.ParseFromDatabase(reader.GetString(2)),
                IsActive = reader.GetInt64(3) == 1,
                CreatedAt = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return list;
    }

    public bool TryAddUser(string username, string password, UserRole role, out string errorMessage)
    {
        errorMessage = string.Empty;
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessage = "아이디를 입력하세요.";
            return false;
        }

        if (!PasswordPolicy.IsValid(password, out string policyMessage))
        {
            errorMessage = policyMessage;
            return false;
        }

        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand dupCommand = connection.CreateCommand();
        dupCommand.CommandText = "SELECT COUNT(1) FROM users WHERE LOWER(username) = LOWER($username)";
        dupCommand.Parameters.AddWithValue("$username", username);
        long exists = (long)(dupCommand.ExecuteScalar() ?? 0);
        if (exists > 0)
        {
            errorMessage = "이미 같은 아이디가 있습니다.";
            return false;
        }

        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO users (username, password_hash, role, is_active, created_at)
            VALUES ($username, $password_hash, $role, 1, $created_at)
            """;
        insertCommand.Parameters.AddWithValue("$username", username);
        insertCommand.Parameters.AddWithValue("$password_hash", PasswordHasher.HashPassword(password));
        insertCommand.Parameters.AddWithValue("$role", role.ToString());
        insertCommand.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o"));
        insertCommand.ExecuteNonQuery();
        return true;
    }

    public bool TrySetUserActive(int targetUserId, bool active, int actingUserId, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (targetUserId == actingUserId && !active)
        {
            errorMessage = "본인 계정은 비활성화할 수 없습니다.";
            return false;
        }

        if (!active && IsUserAdmin(targetUserId) && CountActiveAdmins() <= 1)
        {
            errorMessage = "시스템에 남은 마지막 관리자는 비활성화할 수 없습니다.";
            return false;
        }

        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand updateCommand = connection.CreateCommand();
        updateCommand.CommandText = "UPDATE users SET is_active = $active WHERE id = $id";
        updateCommand.Parameters.AddWithValue("$active", active ? 1 : 0);
        updateCommand.Parameters.AddWithValue("$id", targetUserId);
        int n = updateCommand.ExecuteNonQuery();
        if (n == 0)
        {
            errorMessage = "대상 사용자를 찾을 수 없습니다.";
            return false;
        }

        return true;
    }

    private bool IsUserAdmin(int userId)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT role FROM users WHERE id = $id";
        command.Parameters.AddWithValue("$id", userId);
        object? scalar = command.ExecuteScalar();
        if (scalar is null || scalar is DBNull)
        {
            return false;
        }

        return UserRoleExtensions.ParseFromDatabase(scalar.ToString()) == UserRole.Admin;
    }

    private int CountActiveAdmins()
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT role FROM users WHERE is_active = 1";
        int count = 0;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (UserRoleExtensions.ParseFromDatabase(reader.GetString(0)) == UserRole.Admin)
            {
                count++;
            }
        }

        return count;
    }

    public List<EventLogRow> GetRecentEventLogs(int limit = 200)
    {
        int n = Math.Clamp(limit, 1, 2000);
        List<EventLogRow> list = [];
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, created_at, username, state, code, message
            FROM event_logs
            ORDER BY id DESC
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$limit", n);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string createdRaw = reader.GetString(1);
            string display = createdRaw;
            if (DateTime.TryParse(createdRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime utc))
            {
                display = utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }

            list.Add(new EventLogRow
            {
                Id = reader.GetInt64(0),
                CreatedAtDisplay = display,
                Username = reader.IsDBNull(2) ? null : reader.GetString(2),
                State = reader.IsDBNull(3) ? null : reader.GetString(3),
                Code = reader.IsDBNull(4) ? null : reader.GetString(4),
                Message = reader.GetString(5),
            });
        }

        return list;
    }
}
