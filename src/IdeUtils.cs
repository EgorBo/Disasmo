using System;
using EnvDTE;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace Disasmo
{
    public static class IdeUtils
    {
        public static Project GetActiveProject(this DTE dte)
        {
            var activeSolutionProjects = dte.ActiveSolutionProjects as Array;
            if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
                return activeSolutionProjects.GetValue(0) as Project;
            return null;
        }

        public static string GetPropertyValueSafe(this Configuration c, string key, string defaultValue = "")
        {
            try { return c.Properties?.Item(key)?.Value?.ToString() ?? defaultValue; }
            catch { return defaultValue; }
        }

        public static UnconfiguredProject GetUnconfiguredProject(this Project project)
        {
            var context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
                context = project.Object as IVsBrowseObjectContext;
            return context?.UnconfiguredProject;
        }

        public static WritableSettingsStore GetWritableSettingsStore(this IServiceProvider vsServiceProvider)
        {
            var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
            return shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }
    }
}
