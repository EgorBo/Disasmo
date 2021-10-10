using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Disasmo.Utils
{
    public static class RemoteCheckedJitManager
    {
        public static async Task DownloadCheckedJitAndCopyTo(string dest, CancellationToken ct)
        {
            ProcessResult info = await ProcessUtils.RunProcess("dotnet", "--info");

            // dotnet/installer commit hash from 'dotnet --info' command:
            string dotnetInstallerHash = Regex.Match(info.Output, "Commit: (.*)").Groups[1].Value.Trim('\r', '\n', ' ');
            string versionXml = await new HttpClient()
                .GetStringAsync($"https://raw.githubusercontent.com/dotnet/installer/{dotnetInstallerHash}/eng/Version.Details.xml");

            // Corresponding dotnet/runtime commit hash for our SDK:
            string dotnetRuntimeHash =
                XDocument.Parse(versionXml)
                    .Descendants("Dependency")
                    .First(d => d.Attribute("Name").Value == "Microsoft.NETCore.App.Runtime.win-x64")
                    .Element("Sha").Value;
        }
    }
}
