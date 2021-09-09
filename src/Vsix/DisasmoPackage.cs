using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Disasmo
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(DisasmoPackage.PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideToolWindow(typeof(DisasmWindow))]
    public sealed class DisasmoPackage : AsyncPackage
    {
        public const string PackageGuidString = "6d23b8d8-92f1-4f92-947a-b9021f6ab3dc";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Current = this;
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        }

        public static DisasmoPackage Current { get; set; }

        public static async Task<Version> GetLatestVersionOnline()
        {
            try
            {
                await Task.Delay(3000);
                // is there an API to do it? I don't care - let's parse html :D
                var client = new HttpClient();
                string str = await client.GetStringAsync("https://marketplace.visualstudio.com/items?itemName=EgorBogatov.Disasmo");
                string marker = "extensions/egorbogatov/disasmo/";
                int index = str.IndexOf(marker);
                return Version.Parse(str.Substring(index + marker.Length, str.IndexOf('/', index + marker.Length) - index - marker.Length));
            }
            catch { return new Version(0, 0); }
        }

        public Version GetCurrentVersion()
        {
            //TODO: fix
            return new Version(3, 0, 2);

            //try
            //{
            //    // get ExtensionManager
            //    IVsExtensionManager manager = GetService(typeof(SVsExtensionManager)) as IVsExtensionManager;
            //    // get your extension by Product Id
            //    IInstalledExtension myExtension = manager.GetInstalledExtension("Disasmo.39513ef5-c3ee-4547-b7be-f29c752b591d");
            //    // get current version
            //    Version currentVersion = myExtension.Header.Version;
            //    return currentVersion;
            //}
            //catch {return new Version(0, 0); }
        }
    }
}