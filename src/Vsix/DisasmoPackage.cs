using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Disasmo.ViewModels;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Editor.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Disasmo
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideBindingPath]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(DisasmWindow))]
    public sealed class DisasmoPackage : AsyncPackage
    {
        public const string PackageGuidString = "6d23b8d8-92f1-4f92-947a-b9021f6ab3dc";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            try
            {
                Current = this;
                await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var disasmoCmd = IdeUtils.DTE().Commands.Item("Tools.Disasmo", 0);
                if (disasmoCmd != null)
                {
                    string binding = "";
                    if (disasmoCmd.Bindings is object[] bindingArray)
                    {
                        var hotkeys = bindingArray.Select(b => b.ToString()).ToArray();
                        // prefer Text Editor over Global
                        var bindingPair = hotkeys.FirstOrDefault(h => h.StartsWith("Text Editor::")) ?? hotkeys.FirstOrDefault();
                        if (bindingPair != null && bindingPair.Contains("::"))
                            binding = bindingPair.Substring(bindingPair.IndexOf("::") + 2);
                    }
                    else
                    {
                        if (disasmoCmd.Bindings is string bindingStr)
                        {
                            if (bindingStr.Contains("::"))
                                binding = bindingStr.Substring(bindingStr.IndexOf("::") + 2);
                        }
                    }
                    HotKey = binding;
                }
            }
            catch
            {
            }
        }

        public static string HotKey = "";

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
            return new Version(5, 2, 1);

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

    public class DisasmoCommandArgs : EditorCommandArgs
    {
        public DisasmoCommandArgs(ITextView textView, ITextBuffer textBuffer)
            : base(textView, textBuffer)
        {
        }
    }

    public class DisasmoCommandBinding
    {
        private const int DisasmoCommandId = 0x0100;
        private const string DisasmoCommandSet = "4fd0ea18-9f33-43da-ace0-e387656e584c";

        [Export]
        [CommandBinding(DisasmoCommandSet, DisasmoCommandId, typeof(DisasmoCommandArgs))]
        internal CommandBindingDefinition disasmoCommandBinding;
    }


    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [Name(nameof(DisasmoCommandHandler))]
    public class DisasmoCommandHandler : ICommandHandler<DisasmoCommandArgs>
    {
        public string DisplayName => "Disasmo this";

        [Import]
        private IEditorOperationsFactoryService EditorOperations = null;

        public CommandState GetCommandState(DisasmoCommandArgs args)
        {
            return CommandState.Available;
        }

        public int GetCaretPosition(ITextView view)
        {
            try
            {
                return view?.Caret?.Position.BufferPosition ?? -1;
            }
            catch
            {
                return -1;
            }
        }


        public bool ExecuteCommand(DisasmoCommandArgs args, CommandExecutionContext context)
        {
            var document = args.TextView?.TextBuffer?.GetRelatedDocuments()?.FirstOrDefault();
            if (document != null)
            {
                var pos = GetCaretPosition(args.TextView);
                if (pos != -1)
                {
                    async void CallBack(object _)
                    {
                        try
                        {
                            await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(default);
                            var doc = IdeUtils.DTE().ActiveDocument;
                            var symbol = await DisasmMethodOrClassAction.GetSymbolStatic(document, pos, default, true);
                            var window = await IdeUtils.ShowWindowAsync<DisasmWindow>(true, default);
                            if (window?.ViewModel is {} viewModel)
                            {
                                var settings = viewModel.SettingsVm.ToDisasmoRunnerSettings();
                                viewModel.RunOperationAsync(settings, symbol);
                            }

                            await Task.Delay(300);
                            doc.Activate();
                        }
                        catch
                        {
                        }
                    }

                    ThreadPool.QueueUserWorkItem(CallBack);
                }
            }
            return true;
        }
    }
}