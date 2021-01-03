using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Disasmo
{
    public static class IdeUtils
    {
        public static DTE DTE() => Package.GetGlobalService(typeof(SDTE)) as DTE;

        public static Project GetActiveProject(this DTE dte)
        {
            var activeSolutionProjects = dte.ActiveSolutionProjects as Array;
            if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
                return activeSolutionProjects.GetValue(0) as Project;
            return null;
        }

        public static Configuration GetReleaseConfig(this Project project)
        {
            var allReleaseCfgs = project.ConfigurationManager.OfType<Configuration>().Where(c => c.ConfigurationName == "Release").ToList();
            var cfg = allReleaseCfgs.FirstOrDefault(c => c.PlatformName?.Contains("64") == true);
            if (cfg == null)
            {
                cfg = allReleaseCfgs.FirstOrDefault(c => c.PlatformName?.Contains("Any") == true);
                if (cfg == null)
                {
                    return null;
                }
            }

            return cfg;
        }

        public static void SaveAllActiveDocuments(this DTE dte)
        {
            try
            {
                foreach (EnvDTE.Document document in dte.Documents)
                    document?.Save();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public static string ReadStringFromEmbeddedResource(string id)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(id))
            {
                using (var streamReader = new StreamReader(stream))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }

        public static void SaveEmbeddedResourceTo(string resource, string folder)
        {
            string filePath = Path.Combine(folder, resource);
            if (File.Exists(filePath))
                return;

            using (Stream stream = typeof(DisasmWindowControl).Assembly.GetManifestResourceStream("Disasmo.Resources."  + resource))
            {
                using (FileStream file = File.Create(filePath))
                {
                    file.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(file);
                }
            }
        }

        public static string GetPropertyValueSafe(this Configuration c, string key, string defaultValue = "")
        {
            try { return c.Properties?.Item(key)?.Value?.ToString() ?? defaultValue; }
            catch { return defaultValue; }
        }

        public static void RunDiffTools(string contentLeft, string contentRight)
        {
            string tmpFileLeft = Path.GetTempFileName();
            string tmpFileRight = Path.GetTempFileName();

            File.WriteAllText(tmpFileLeft, contentLeft);
            File.WriteAllText(tmpFileRight, contentRight);

            try
            {
                // Copied from https://github.com/madskristensen/FileDiffer/blob/master/src/Commands/DiffFilesCommand.cs#L48-L56 (c) madskristensen
                object args = $"\"{tmpFileLeft}\" \"{tmpFileRight}\"";
                ((DTE)Package.GetGlobalService(typeof(SDTE))).Commands.Raise("5D4C0442-C0A2-4BE8-9B4D-AB1C28450942", 256, ref args, ref args);
            }
            catch
            {
                return;
            }
            finally
            {
                File.Delete(tmpFileLeft);
                File.Delete(tmpFileRight);
            }
        }

        public static async System.Threading.Tasks.Task<T> ShowWindowAsync<T>(CancellationToken cancellationToken) where T : class
        {
            try
            {
                if (DisasmoPackage.Current == null)
                {
                    MessageBox.Show("DisasmoPackage is loading... please try again later.");
                    return null;
                }

                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                await DisasmoPackage.Current.ShowToolWindowAsync(typeof(DisasmWindow), 0, create: true, cancellationToken: cancellationToken);
                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                // no idea why I have to call it twice, it doesn't work if I do it only once on the first usage
                var window = await DisasmoPackage.Current.ShowToolWindowAsync(typeof(T), 0, create: true, cancellationToken: cancellationToken);
                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                return window as T;
            }
            catch
            {
                return null;
            }
        }
    }
}
