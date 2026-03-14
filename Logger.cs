using System.Text;

namespace SenfCli;

public static class Logger
{
    private static readonly Lock Sync = new();
    private const int MaxLogFiles = 10;
    private static string? _logDir;
    private static string? _logFilePath;
    private static bool _initialized;

    public static bool IsActive { get; private set; }

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
                return;

            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".senf");
            _logDir = Path.Combine(configDir, "logs");
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);

            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            _logFilePath = Path.Combine(_logDir, $"senf-{stamp}.log");
            CleanupOldLogs();
            _initialized = true;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        lock (Sync)
        {
            IsActive = enabled;
            if (IsActive && !_initialized)
                Initialize();
        }
    }

    public static void EnableFromEnvironment(string variableName = "SENF_LOG")
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (IsTruthy(value))
            SetEnabled(true);
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Debug(string message) => Write("DEBUG", message);

    public static void Error(Exception ex, string message)
    {
        var details = new StringBuilder();
        details.AppendLine(message);
        details.AppendLine(ex.ToString());
        Write("ERROR", details.ToString().TrimEnd());
    }

    private static void Write(string level, string message)
    {
        try
        {
            if (!IsActive)
                return;
            EnsureInitialized();
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            var line = $"{timestamp} [{level}] {message}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(_logFilePath!, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Swallow logging failures to avoid breaking CLI execution.
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;
        Initialize();
    }

    private static void CleanupOldLogs()
    {
        if (_logDir == null)
            return;

        var logFiles = Directory.GetFiles(_logDir, "senf-*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ToList();

        for (var i = MaxLogFiles; i < logFiles.Count; i++)
        {
            try
            {
                logFiles[i].Delete();
            }
            catch
            {
                // Ignore deletion failures to keep CLI execution safe.
            }
        }
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
