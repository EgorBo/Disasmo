using System;
using System.Runtime.InteropServices;
using System.Threading;
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
    }
}