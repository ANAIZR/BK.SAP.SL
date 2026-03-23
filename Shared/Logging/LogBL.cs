using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Logging;

public static class LogBL
{
    private static readonly object _sync = new();
    private static string _dateFormat = "yyyy-MM-dd HH:mm:ss.fff";

    // Diccionario: WorkerName => FilePath
    private static readonly ConcurrentDictionary<string, string> _filePaths
        = new ConcurrentDictionary<string, string>();

    private static string? _baseDir;





    public static void Initialize(string? baseDirectory = null)
    {
        _baseDir = baseDirectory;
        _filePaths.Clear();
    }

    // === MÉTODOS PÚBLICOS PARA CADA WORKER ===
    public static void Info(string text, string worker) => Write(LogLevel.INFO, text, worker);
    public static void Debug(string text, string worker) => Write(LogLevel.DEBUG, text, worker);
    public static void Error(string text, string worker) => Write(LogLevel.ERROR, text, worker);
    public static void Warning(string text, string worker) => Write(LogLevel.WARNING, text, worker);

    // También puedes dejar los métodos sin worker si quieres logs generales
    public static void Info(string text) => Write(LogLevel.INFO, text, "GENERAL");

    private static void Write(LogLevel level, string text, string worker)
    {
        var prefix = level switch
        {

            LogLevel.INFO => " [INFO]    ",
            LogLevel.DEBUG => " [DEBUG]   ",
            LogLevel.WARNING => " [WARNING] ",
            LogLevel.ERROR => " [ERROR]   ",

            _ => " "
        };

        var line = $"{DateTime.Now.ToString(_dateFormat)}{prefix}{text}";
        WriteLine(line, worker);
    }

    private static void WriteLine(string text, string worker)
    {
        try
        {
            var path = EnsureFilePath(worker);

            lock (_sync)
            {
                File.AppendAllText(path, text + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Nunca lanzar errores desde el logger

        }
    }

    private static string EnsureFilePath(string worker)
    {
        return _filePaths.GetOrAdd(worker, w =>
        {
            var baseDir = !string.IsNullOrWhiteSpace(_baseDir)
                ? _baseDir!
                : AppContext.BaseDirectory;

            var dir = Path.Combine(
                baseDir,
                "Log",
                DateTime.Now.Year.ToString("0000"),
                DateTime.Now.Month.ToString("00"),
                w // carpeta por worker
            );

            Directory.CreateDirectory(dir);

            var filePath = Path.Combine(
                dir,
                $"{DateTime.Today:MM-dd-yyyy}.log"
            );

            return filePath;
        });
    }

    private enum LogLevel { INFO, DEBUG, WARNING, ERROR }
}
