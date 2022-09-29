using System.IO;

namespace Disasmo.Utils
{
    public static class UserLogger
    {
        public static void AppendText(string text)
        {
            File.AppendAllText(LogFile, text + "\n");
        }

        public static string LogFile { get; } = Path.GetTempFileName();
    }
}
