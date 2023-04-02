using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Disasmo;

// Builds a module loader app using system dotnet
// Rebuilds it when the Add-in updates or SDK's version changes.
public static class LoaderAppManager
{
    public static readonly string DisasmoLoaderName = "DisasmoLoader4";

    private static async Task<string> GetPathToLoader(string tf, Version addinVersion, CancellationToken ct)
    {
        ProcessResult dotnetVersion = await ProcessUtils.RunProcess("dotnet", "--version", cancellationToken: ct);
        UserLogger.Log($"dotnet --version: {dotnetVersion.Output} ({dotnetVersion.Error})");
        string version = dotnetVersion.Output.Trim();
        if (!char.IsDigit(version[0]))
        {
            // Something went wrong, use a random to proceed
            version = Guid.NewGuid().ToString("N");
        }
        string folderName = $"{addinVersion}_{tf}_{version}";
        UserLogger.Log($"LoaderAppManager.GetPathToLoader: {folderName}");
        return Path.Combine(Path.GetTempPath(), DisasmoLoaderName, folderName);
    }

    public static async Task InitLoaderAndCopyTo(string tf, string dest, Action<string> logger, Version addinVersion, CancellationToken ct)
    {
        if (!Directory.Exists(dest))
            throw new InvalidOperationException($"ERROR: dest dir was not found: {dest}");

        string dir;
        try
        {
            logger("Getting SDK version...");
            dir = await GetPathToLoader(tf, addinVersion, ct);
        }
        catch (Exception exc)
        {
            throw new InvalidOperationException("ERROR in LoaderAppManager.GetPathToLoader: " + exc);
        }

        string csproj = Path.Combine(dir, $"{DisasmoLoaderName}.csproj");
        string csfile = Path.Combine(dir, $"{DisasmoLoaderName}.cs");
        string outDll = Path.Combine(dir, "out", $"{DisasmoLoaderName}.dll");
        string outJson = Path.Combine(dir, "out", $"{DisasmoLoaderName}.runtimeconfig.json");
        string outDllDest = Path.Combine(dest, DisasmoLoaderName + ".dll");
        string outJsonDest = Path.Combine(dest, DisasmoLoaderName + ".runtimeconfig.json");

        if (File.Exists(outDllDest) && File.Exists(outJsonDest))
        {
            return;
        }

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
            TextUtils.SaveEmbeddedResourceTo($"{DisasmoLoaderName}.cs_template", dir);

        if (!File.Exists(csproj))
            TextUtils.SaveEmbeddedResourceTo($"{DisasmoLoaderName}.csproj_template", dir, content => content.Replace("%tfm%", tf));

        Debug.Assert(File.Exists(csfile));
        Debug.Assert(File.Exists(csproj));

        ct.ThrowIfCancellationRequested();

        var msg = await ProcessUtils.RunProcess("dotnet", "build -c Release", workingDir: dir, cancellationToken: ct);

        if (!File.Exists(outDll) || !File.Exists(outJson))
            throw new InvalidOperationException($"ERROR: 'dotnet build' did not produce expected binaries ('{outDll}'" +
                                                $" and '{outJson}'):\n{msg.Output}\n\n{msg.Error}");

        ct.ThrowIfCancellationRequested();
        File.Copy(outDll, outDllDest, true);
        File.Copy(outJson, outJsonDest, true);
    }
}