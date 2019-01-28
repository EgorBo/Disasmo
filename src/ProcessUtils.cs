using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Disasmo
{
    public static class ProcessUtils
    {
        public static async Task<ProcessResult> RunProcess(string path, string args = "", Dictionary<string, string> envVars = null, string workingDir = null)
        {
            var logger = new StringBuilder();
            var loggerForErrors = new StringBuilder();
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Arguments = args,
                };

                if (workingDir != null)
                    processStartInfo.WorkingDirectory = workingDir;

                if (envVars != null)
                {
                    foreach (var envVar in envVars)
                        processStartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
                }

                var process = Process.Start(processStartInfo);

                process.ErrorDataReceived += (sender, e) =>
                {
                    logger.AppendLine(e.Data);
                    loggerForErrors.AppendLine(e.Data);
                };
                process.OutputDataReceived += (sender, e) => logger.AppendLine(e.Data);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                return new ProcessResult { Error = loggerForErrors.ToString().Trim('\r', '\n'), Output = logger.ToString().Trim('\r', '\n') };
            }
            catch
            {
                return new ProcessResult { Error = $"RunProcess failed.\npath={path}\nargs={args}\nworkingdir={workingDir ?? Environment.CurrentDirectory}\n{loggerForErrors}" };
            }
        }
    }

    public class ProcessResult
    {
        public string Output { get; set; }
        public string Error { get; set; }
    }
}