using System;
using BenchmarkDotNet.Disassembler;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Disasmo
{
    public class BdnDisassembler
    {
        // An alternative way to disasm C# code is to use ClrMD (BenchmarkDotNet impl):
        public static async Task<DisassemblyResult> Disasm(string path, string type, string method)
        {
            string bdnDisasmer = "TODO:";//@"C:\prj\Disasmo\BenchmarkDotNet.Disassembler.x64\bin\Debug\net46\BenchmarkDotNet.Disassembler.x64.exe";
            string tmpOutput = Path.GetTempFileName();

            try
            {
                var appProcess = Process.Start(
                    new ProcessStartInfo(path) {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                var bdnProcess = Process.Start(
                    new ProcessStartInfo(bdnDisasmer) {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments = $"{appProcess.Id} {type} {method} True False False True 1 \"{tmpOutput}\"",
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    });

                bdnProcess.WaitForExit(10000);

                var output = bdnProcess.StandardOutput.ReadToEnd();
                var error = bdnProcess.StandardError.ReadToEnd();

                appProcess.Kill();

                XmlSerializer xmlSerializer = new XmlSerializer(typeof(DisassemblyResult));
                using (var stream = File.OpenRead(tmpOutput))
                {
                    return (DisassemblyResult) xmlSerializer.Deserialize(stream);
                }
            }
            finally
            {
                File.Delete(tmpOutput);
            }
        }
    }
}
