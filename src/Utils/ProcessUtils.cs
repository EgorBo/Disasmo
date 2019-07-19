using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Disasmo
{
    public static class ProcessUtils
    {
        public static async Task<ProcessResult> RunProcess(string path, string args = "", Dictionary<string, string> envVars = null, string workingDir = null, Action<bool, string> outputLogger = null, CancellationToken cancellationToken = default)
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

                cancellationToken.ThrowIfCancellationRequested();
                var process = Process.Start(processStartInfo);
                cancellationToken.ThrowIfCancellationRequested();

                process.ErrorDataReceived += (sender, e) =>
                {
                    outputLogger?.Invoke(true, e.Data + "\n");
                    logger.AppendLine(e.Data);
                    loggerForErrors.AppendLine(e.Data);
                };
                process.OutputDataReceived += (sender, e) =>
                {
                    outputLogger?.Invoke(false, e.Data + "\n");
                    logger.AppendLine(e.Data);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                return new ProcessResult { Error = loggerForErrors.ToString().Trim('\r', '\n'), Output = logger.ToString().Trim('\r', '\n') };
            }
            catch (Exception e)
            {
                return new ProcessResult { Error = $"RunProcess failed:{e.Message}.\npath={path}\nargs={args}\nworkingdir={workingDir ?? Environment.CurrentDirectory}\n{loggerForErrors}" };
            }
        }
    }

    public class ProcessResult
    {
        public string Output { get; set; }
        public string Error { get; set; }
    }
}