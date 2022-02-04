using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
