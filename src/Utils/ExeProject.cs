using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disasmo
{

    // It's not finished yet
    // the idea is to generate a temp ConsoleApp in order to be able to disasm class libraries
    public static class ExeProject
    {
        public static void GenerateForObjectLayoutInspector(string projectPath, string workingDir, string type, string targetFramework = "netcoreapp3.0")
        {
            ConfigureDisasmoHiddenLauncher(workingDir, 
                new Dictionary<string, string>
                {
                    { "%targetFramework%", targetFramework },
                    { "%defineConstants%", "DISASMO_OBJECT_LAYOUT_INSPECTOR" },
                    { "%csprojFile%", projectPath },
                }, 
                new Dictionary<string, string>
                {
                    { "%typename%", type }
                });
        }

        public static void GenerateForDisasmClass(string projectPath, string workingDir, string type, bool waitForAttach, string targetFramework = "netcoreapp3.0")
        {
            ConfigureDisasmoHiddenLauncher(workingDir,
                new Dictionary<string, string>
                {
                    { "%targetFramework%", targetFramework },
                    { "%defineConstants%", "DISASMO_PREPARE_CLASS;" + (waitForAttach ? "DISASMO_WAIT_FOR_ATTACH" : "") },
                    { "%csprojFile%", projectPath },
                },
                new Dictionary<string, string>
                {
                    { "%typename%", type }
                });
        }

        public static void GenerateForDisasmGenericMethod(string projectPath, string workingDir, string type, string targetFramework = "netcoreapp3.0")
        {
            ConfigureDisasmoHiddenLauncher(workingDir,
                new Dictionary<string, string>
                {
                    { "%targetFramework%", targetFramework },
                    { "%defineConstants%", "DISASMO_PREPARE_GENERIC_METHOD" },
                    { "%csprojFile%", projectPath },
                },
                new Dictionary<string, string>
                {
                    { "%typename%", type }
                });
        }

        private static void ConfigureDisasmoHiddenLauncher(string workingDir,
            Dictionary<string, string> csprojVariables,
            Dictionary<string, string> csVariables)
        {
            if (!Directory.Exists(workingDir))
                Directory.CreateDirectory(workingDir);

            try
            {
                var csFileContent = IdeUtils.ReadStringFromEmbeddedResource("Disasmo.Resources.LauncherTemplate.Main.cs");
                var csprojFileContent = IdeUtils.ReadStringFromEmbeddedResource("Disasmo.Resources.LauncherTemplate.DisasmoHiddenLauncher.csproj");
                string csFileName = Path.Combine(workingDir, "DisasmoHiddenLauncher.cs");
                string csprojFileName = Path.Combine(workingDir, "DisasmoHiddenLauncher.csproj");

                File.Delete(csprojFileName);
                File.Delete(csFileName);

                foreach (var item in csprojVariables)
                    csprojFileContent = csprojFileContent.Replace(item.Key, item.Value);

                foreach (var item in csVariables)
                    csFileContent = csFileContent.Replace(item.Key, item.Value);

                File.WriteAllText(csFileName, csFileContent);
                File.WriteAllText(csprojFileName, csprojFileContent);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to setup DisasmoHiddenLauncher.", e);
            }
        }
    }
}
