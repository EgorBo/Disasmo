using System;
using System.Collections.Generic;
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
            string bdnDisasmer = "BenchmarkDotNet.Disassembler.x64.exe";//@"C:\prj\Disasmo\BenchmarkDotNet.Disassembler.x64\bin\Debug\net46\BenchmarkDotNet.Disassembler.x64.exe";
            string tmpOutput = Path.GetTempFileName();

            try
            {
                var appProcess = Process.Start(
                    new ProcessStartInfo(path){
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                await Task.Delay(2000);

                var bdnProcess = Process.Start(
                    new ProcessStartInfo(bdnDisasmer){
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments = $"{appProcess.Id} {type} {method} True False False False 0 \"{tmpOutput}\"",
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    });

                bdnProcess.WaitForExit(10000);

                var output = bdnProcess.StandardOutput.ReadToEnd();
                var error = bdnProcess.StandardError.ReadToEnd();

                appProcess.Kill();

                if (!string.IsNullOrWhiteSpace(output) || !string.IsNullOrWhiteSpace(error))
                    return new DisassemblyResult {Errors = new[] {output, error, "Output: " + tmpOutput}};

                var xmlSerializer = new XmlSerializer(typeof(DisassemblyResult));
                DisassemblyResult result;
                using (var stream = File.OpenRead(tmpOutput))
                    result = (DisassemblyResult) xmlSerializer.Deserialize(stream);

                File.Delete(tmpOutput);
                return result;
            }
            catch (Exception exc)
            {
                return new DisassemblyResult {Errors = new[] {exc.ToString()}};
            }
        }
    }
}
