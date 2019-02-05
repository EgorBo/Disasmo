using System;
using System.Collections.Generic;
using BenchmarkDotNet.Disassembler;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Xml.Serialization;
using Microsoft.Diagnostics.RuntimeExt;

namespace Disasmo
{
    public class BdnDisassembler
    {
        // An alternative way to disasm C# code is to use ClrMD (BenchmarkDotNet impl):
        public static async Task<DisassemblyResult> Disasm(string path, string type, string method, Dictionary<string, string> envVars, 
            bool showAsm, bool showIl, bool showSource, bool prologueAndEpilogue, int recursionDepth)
        {
            string bdnDisasmer = typeof(ClrSourceExtensions).Assembly.Location;
            string tmpOutput = Path.GetTempFileName();

            try
            {
                var appProcessParams = new ProcessStartInfo(path)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                    };

                foreach (var pair in envVars)
                    appProcessParams.EnvironmentVariables[pair.Key] = pair.Value;

                var appProcess = Process.Start(appProcessParams);

                // wait while everything is being jitted
                await appProcess.StandardOutput.ReadLineAsync();

                var bdnProcess = Process.Start(
                    new ProcessStartInfo(bdnDisasmer)
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            Arguments = $"{appProcess.Id} {type} {method} {showAsm} {showIl} {showSource} {prologueAndEpilogue} {recursionDepth} \"{tmpOutput}\"",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true
                        });

                bdnProcess.WaitForExit(20000);

                appProcess.StandardInput.WriteLine("Attached!");
                appProcess.Kill();

                var output = bdnProcess.StandardOutput.ReadToEnd();
                var error = bdnProcess.StandardError.ReadToEnd();

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
