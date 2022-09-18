using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disasmo.Utils;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Serialization;

namespace Disasmo
{
    public static class ProcessUtils
    {
        public static async Task<ProcessResult> RunProcess(
            string path, 
            string args = "", 
            Dictionary<string, string> envVars = null, 
            string workingDir = null, 
            Action<bool, string> outputLogger = null, 
            CancellationToken cancellationToken = default)
        {
            UserLogger.AppendText($"\nExecuting a command in directory \"{workingDir}\":\n\t{path} {args}\nEnv.vars:\n{DumpEnvVars(envVars)}");

            var logger = new StringBuilder();
            var loggerForErrors = new StringBuilder();
            Process process = null;
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
                process = Process.Start(processStartInfo);
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

                cancellationToken.ThrowIfCancellationRequested();
                await process.WaitForExitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                return new ProcessResult { Error = loggerForErrors.ToString().Trim('\r', '\n'), Output = logger.ToString().Trim('\r', '\n') };
            }
            catch (Exception e)
            {
                return new ProcessResult { Error = $"RunProcess failed:{e.Message}.\npath={path}\nargs={args}\nworkingdir={workingDir ?? Environment.CurrentDirectory}\n{loggerForErrors}" };
            }
            finally
            {
                // Just to make sure the process is killed
                process.KillProccessSafe();
            }
        }

        public static Task WaitForExitAsync(this Process process,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (process.HasExited) 
                return Task.CompletedTask;
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if (cancellationToken != default(CancellationToken))
                cancellationToken.Register(() => tcs.TrySetCanceled());
            return process.HasExited ? Task.CompletedTask : tcs.Task;
        }

        private static void KillProccessSafe(this Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
            }
        }

        private static string DumpEnvVars(Dictionary<string, string> envVars)
        {
            if (envVars == null)
                return "";

            string envVar = "";
            foreach (var ev in envVars)
                envVar += ev.Key + "=" + ev.Value + "\n";
            return envVar;
        }
    }

    public class ProcessResult
    {
        public string Output { get; set; }
        public string Error { get; set; }
    }
}