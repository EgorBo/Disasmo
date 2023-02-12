using System.IO;

namespace Disasmo;

public static class UserLogger
{
    public static void Log(string text) => File.AppendAllText(LogFile, text?.NormalizeLineEndings());

    public static string LogFile { get; } = Path.GetTempFileName();
}