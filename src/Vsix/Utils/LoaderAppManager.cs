using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Disasmo.Utils
{
    // Builds a module loader app using system dotnet
    // Rebuilds it when the Add-in updates or SDK's version changes.
    public static class LoaderAppManager
    {
        public const string DisasmoLoaderName = "DisasmoLoader3";

        private static async Task<string> GetPathToLoader(CancellationToken ct)
        {
            ProcessResult dotnetVersion = await ProcessUtils.RunProcess("dotnet", "--version", cancellationToken: ct);
            Version addinVersion = DisasmoPackage.Current.GetCurrentVersion();
            return Path.Combine(Path.GetTempPath(), DisasmoLoaderName, $"{addinVersion}_{dotnetVersion.Output}");
        }

        public static async Task InitLoaderAndCopyTo(string dest, Action<string> logger, CancellationToken ct)
        {
            if (!Directory.Exists(dest))
                throw new InvalidOperationException($"ERROR: dest dir was not found: {dest}");

            string dir = null;
            try
            {
                logger("Getting SDK version...");
                dir = await GetPathToLoader(ct);
            }
            catch (Exception exc)
            {
                throw new InvalidOperationException("ERROR in LoaderAppManager.GetPathToLoader: " + exc);
            }

            string csproj = Path.Combine(dir, $"{DisasmoLoaderName}.csproj");
            string csfile = Path.Combine(dir, $"{DisasmoLoaderName}.csproj");
            string outDll = Path.Combine(dir, "out", $"{DisasmoLoaderName}.dll");
            string outJson = Path.Combine(dir, "out", $"{DisasmoLoaderName}.runtimeconfig.json");

            string outDllDest = Path.Combine(dest, DisasmoLoaderName + ".dll");
            string outJsonDest = Path.Combine(dest, DisasmoLoaderName + ".runtimeconfig.json");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            else if (File.Exists(outDll) && File.Exists(outJson))
            {
                File.Copy(outDll, outDllDest, true);
                File.Copy(outJson, outJsonDest, true);
                return;
            }

            logger($"Building '{DisasmoLoaderName}' project...");
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(csfile))
                IdeUtils.SaveEmbeddedResourceTo($"{DisasmoLoaderName}.cs_template", dir);

            if (!File.Exists(csproj))
                IdeUtils.SaveEmbeddedResourceTo($"{DisasmoLoaderName}.csproj_template", dir);

            Debug.Assert(File.Exists(csfile));
            Debug.Assert(File.Exists(csproj));

            ct.ThrowIfCancellationRequested();

            var msg = await ProcessUtils.RunProcess("dotnet", "build -c Release", workingDir: dir, cancellationToken: ct);

            if (!File.Exists(outDll) || !File.Exists(outJson))
            {
                throw new InvalidOperationException($"ERROR: 'dotnet build' did not produce expected binaries ('{outDll}' and '{outJson}'):\n{msg.Output}\n\n{msg.Error}");
            }

            ct.ThrowIfCancellationRequested();
            File.Copy(outDll, outDllDest, true);
            File.Copy(outJson, outJsonDest, true);
        }
    }
}
