using System.Collections;
using System.IO;
using System.Text;

namespace WarehouseTimesheetApp;

public static class ErrorLogger
{
    private static readonly object Sync = new();
    private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

    public static string LogDirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WarehouseTimesheetApp",
        "logs");

    public static string LogFilePath => Path.Combine(LogDirectoryPath, $"errors_{SessionId}.log");

    public static void LogMessage(string source, string message)
    {
        var payload = new StringBuilder()
            .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO")
            .AppendLine($"Source: {source}")
            .AppendLine($"Message: {message}")
            .AppendLine(new string('-', 80))
            .ToString();

        Write(payload);
    }

    public static void LogException(string source, Exception exception, IDictionary? context = null)
    {
        var payload = new StringBuilder()
            .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR")
            .AppendLine($"Source: {source}")
            .AppendLine($"Type: {exception.GetType().FullName}")
            .AppendLine($"Message: {exception.Message}")
            .AppendLine("StackTrace:")
            .AppendLine(exception.StackTrace ?? "<empty>");

        if (context is not null && context.Count > 0)
        {
            payload.AppendLine("Context:");
            foreach (DictionaryEntry item in context)
            {
                payload.AppendLine($"  - {item.Key}: {item.Value}");
            }
        }

        if (exception.InnerException is not null)
        {
            payload.AppendLine("InnerException:")
                .AppendLine(exception.InnerException.ToString());
        }

        payload.AppendLine(new string('-', 80));
        Write(payload.ToString());
    }

    private static void Write(string payload)
    {
        lock (Sync)
        {
            Directory.CreateDirectory(LogDirectoryPath);
            File.AppendAllText(LogFilePath, payload, Encoding.UTF8);
        }
    }
}
