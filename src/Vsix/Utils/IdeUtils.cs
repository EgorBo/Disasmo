using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Disasmo;

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

    public static void SaveAllActiveDocuments(this DTE dte)
    {
        try
        {
            foreach (Document document in dte.Documents)
                document?.Save();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    public static async Task<T> ShowWindowAsync<T>(bool tryTwice, CancellationToken cancellationToken) where T : class
    {
        try
        {
            if (DisasmoPackage.Current == null)
            {
                MessageBox.Show("DisasmoPackage is still loading... (sometimes it takes a while for add-ins to fully load - it makes VS faster to start).");
                return null;
            }
            await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var window = await DisasmoPackage.Current.ShowToolWindowAsync(typeof(DisasmWindow), 0, create: true, cancellationToken: cancellationToken);
            if (tryTwice)
            {
                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                // no idea why I have to call it twice, it doesn't work if I do it only once on the first usage
                window = await DisasmoPackage.Current.ShowToolWindowAsync(typeof(T), 0, create: true,
                    cancellationToken: cancellationToken);
                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            }
            return window as T;
        }
        catch
        {
            return null;
        }
    }

    public static void RunDiffTools(string contentLeft, string contentRight)
    {
        contentLeft = string.IsNullOrEmpty(contentLeft) ? " " : contentLeft;
        contentRight = string.IsNullOrEmpty(contentRight) ? " " : contentRight;

        var tempPath = Path.GetTempPath();
        var diffDir = Path.Combine(tempPath, "Disasmo_diffs_" + Guid.NewGuid().ToString("N").Substring(0, 10));
        Directory.CreateDirectory(diffDir);

        string tmpFileLeft = Path.Combine(diffDir, "previous.asm");
        string tmpFileRight = Path.Combine(diffDir, "current.asm");

        File.WriteAllText(tmpFileLeft, contentLeft.NormalizeLineEndings());
        File.WriteAllText(tmpFileRight, contentRight.NormalizeLineEndings());

        try
        {
            // Copied from https://github.com/madskristensen/FileDiffer/blob/main/src/Commands/DiffFilesCommand.cs#L48-L56 (c) madskristensen
            object args = $"\"{tmpFileLeft}\" \"{tmpFileRight}\"";
            ((DTE)Package.GetGlobalService(typeof(SDTE))).Commands.Raise("5D4C0442-C0A2-4BE8-9B4D-AB1C28450942", 256, ref args, ref args);
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
        finally
        {
            File.Delete(tmpFileLeft);
            File.Delete(tmpFileRight);
        }
    }

    public static async Task<IProjectProperties> GetProjectProperties(UnconfiguredProject unconfiguredProject, string config)
    {
        try
        {
            // it will throw "Release config was not found" to the Output if there is no such config in the project
            ProjectConfiguration releaseConfig = await unconfiguredProject.Services.ProjectConfigurationsService.GetProjectConfigurationAsync("Release");
            ConfiguredProject configuredProject = await unconfiguredProject.LoadConfiguredProjectAsync(releaseConfig);
            return configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
            // VS was not able to find the given config (but it still might exist)
            return null;
        }
    }

    public static async Task<(string, int)> GetTargetFramework(IProjectProperties projectProperties)
    {
        if (projectProperties == null)
        {
            // It is likely hidden somewhere in props, TODO: find a better way
            return ("net7.0", 7);
        }

        try
        {
            string tfms = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFrameworks");
            string tf;
            if (string.IsNullOrEmpty(tfms))
            {
                tf = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFramework");
            }
            else
            {
                var parts = tfms.Split(new[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries);
                tf = parts
                    .Where(p => p.Length == "net7.0".Length && p.StartsWith("net") && char.IsDigit(p[3]))
                    .OrderByDescending(i => i)
                    .FirstOrDefault();
            }
            int majorVersion = tf == null ? 0 : int.Parse(tf.Substring(3, tf.IndexOf('.') - 3));
            return (tf, majorVersion);
        }
        catch (Exception exc)
        {
            UserLogger.Log($"Failed to detect TargetFramework: {exc}");
        }
        return ("", 0);
    }

    public static async void OpenInVSCode(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            string file = Path.GetTempFileName() + ".asm";
            File.WriteAllText(file, output);

            try
            {
                await ProcessUtils.RunProcess("code", file);
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            }
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
    }

    public static void OpenInEditor(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            string file = Path.GetTempFileName() + ".asm";
            File.WriteAllText(file, output);
            
            using (new NewDocumentStateScope(__VSNEWDOCUMENTSTATE.NDS_Provisional, VSConstants.NewDocumentStateReason.SolutionExplorer))
            {
                DTE().ItemOperations.OpenFile(file);
            }
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
    }
}