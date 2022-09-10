using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using IServiceProvider = System.IServiceProvider;

namespace Disasmo
{
    class GuidAndCmdID
    {
        public const string PackageCmdSetGuidString = "6d23b8d8-92f1-4f92-947a-b9021f6ab3dc";

        public static readonly Guid guidCmdSet = new Guid(PackageCmdSetGuidString);

        public const uint cmdidNavigateToGitCommit = 0x0100;
    }


    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(DisasmoPackage.PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideToolWindow(typeof(DisasmWindow))]
    public sealed class DisasmoPackage : AsyncPackage, IOleCommandTarget
    {
        private IOleCommandTarget pkgCommandTarget;
        public const string PackageGuidString = "6d23b8d8-92f1-4f92-947a-b9021f6ab3dc";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Current = this;
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            this.pkgCommandTarget = await this.GetServiceAsync(typeof(IOleCommandTarget)) as IOleCommandTarget;

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
            return new Version(5, 0, 4);

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



        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == GuidAndCmdID.guidCmdSet)
            {
                switch (prgCmds[0].cmdID)
                {
                    case GuidAndCmdID.cmdidNavigateToGitCommit:
                        prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_INVISIBLE);
                        return VSConstants.S_OK;
                }
            }

            return this.pkgCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == GuidAndCmdID.guidCmdSet)
            {
                switch (nCmdID)
                {
                    case GuidAndCmdID.cmdidNavigateToGitCommit:
                        if (IsQueryParameterList(pvaIn, pvaOut, nCmdexecopt))
                        {
                            Marshal.GetNativeVariantForObject("p", pvaOut);
                            return VSConstants.S_OK;
                        }
                        else
                        {
                            // no args
                            if (pvaIn == IntPtr.Zero)
                                return VSConstants.S_FALSE;

                            object vaInObject = Marshal.GetObjectForNativeVariant(pvaIn);
                            if (vaInObject == null || vaInObject.GetType() != typeof(string))
                                return VSConstants.E_INVALIDARG;

                            if ((vaInObject is string commitId) && !string.IsNullOrEmpty(commitId))
                            {
                                NavigateToCommit(commitId, this as IServiceProvider);
                            }
                        }
                        return VSConstants.S_OK;
                }
            }

            return this.pkgCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        internal static void NavigateToCommit(string commitId, IServiceProvider serviceProvider)
        {
            string title = "CodeLends OOP Extension";
            string message = $"Commit Id is: {commitId}";

            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                serviceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private static bool IsQueryParameterList(IntPtr pvaIn, IntPtr pvaOut, uint nCmdexecopt)
        {
            ushort lo = (ushort)(nCmdexecopt & (uint)0xffff);
            ushort hi = (ushort)(nCmdexecopt >> 16);
            if (lo == (ushort)OLECMDEXECOPT.OLECMDEXECOPT_SHOWHELP)
            {
                if (hi == VsMenus.VSCmdOptQueryParameterList)
                {
                    return true;
                }
            }

            return false;
        }
    }
}