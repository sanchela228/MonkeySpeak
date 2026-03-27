using System.Diagnostics;

namespace Core;

public static class Logger
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4
    }

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;
    public static Action<LogLevel, string, Exception?> Sink { get; set; } = DefaultSink;

    public static void Trace(string message) => Log(LogLevel.Trace, message);
    public static void Debug(string message) => Log(LogLevel.Debug, message);
    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warn(string message) => Log(LogLevel.Warn, message);
    public static void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);
    public static void Error(Exception ex) => Log(LogLevel.Error, ex.Message, ex);
    
    public static void Log(LogLevel level, string message, Exception? ex = null)
    {
        if (level < MinimumLevel)
            return;

        try
        {
            Sink(level, message, ex);
        }
        catch
        {
        }
    }

    private static void DefaultSink(LogLevel level, string message, Exception? ex)
    {
        var ts = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
        var tid = Environment.CurrentManagedThreadId;

        var line = $"[{ts}] [T{tid}] [{level}] {message}";
        if (ex is not null)
            line += $" | {ex.GetType().Name}: {ex.Message}";

        System.Diagnostics.Debug.WriteLine(line);
    }
}