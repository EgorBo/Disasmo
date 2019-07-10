using Disasmo.Utils;
using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Disasmo.ViewModels
{
    public class RunOnLocalClrViewModel : ViewModelBase
    {
        private readonly SettingsViewModel _settingsVm;
        private string output;

        public RunOnLocalClrViewModel() { }

        public RunOnLocalClrViewModel(SettingsViewModel settingsVm)
        {
            _settingsVm = settingsVm;
        }

        public ICommand RunDebug => new RelayCommand(() => Run(false));

        public ICommand RunRelease => new RelayCommand(() => Run(true));

        public string Output
        {
            get => output;
            set => Set(ref output, value);
        }

        private CancellationTokenSource _cts;

        private async void Run(bool release)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ctoken = _cts.Token;
            try
            {
                Output = "Working...";
                if (string.IsNullOrWhiteSpace(_settingsVm.PathToLocalCoreClr))
                {
                    ctoken.ThrowIfCancellationRequested();
                    Output += "Path to a local CoreCLR is not set ^.";
                    return;
                }

                DTE dte = IdeUtils.DTE();

                // Find Release-x64 configuration:
                Project currentProject = dte.GetActiveProject();

                var neededConfig = currentProject.GetReleaseConfig();
                if (neededConfig == null)
                {
                    Output = "Couldn't find any 'Release - x64' or 'Release - Any CPU' configuration.";
                    return;
                }

                var currentProjectPath = currentProject.FileName;

                // unfortunately both old VS API and new crashes for me on my vs2019preview2 (see https://github.com/dotnet/project-system/issues/669 and the workaround - both crash)
                // ugly hack for OutputType:
                if (!File.ReadAllText(currentProjectPath).ToLower().Contains("<outputtype>exe<"))
                {
                    Output = "At this moment only .NET Core Сonsole Applications (`<OutputType>Exe</OutputType>`) are supported.\nFeel free to contribute multi-project support :-)";
                    return;
                }

                string currentProjectDirPath = Path.GetDirectoryName(currentProjectPath);

                const string outFolder = "DisasmoLocalRun";
                string dotnetCli = DotnetCliUtils.GetDotnetCliPath(_settingsVm.PathToLocalCoreClr);
                string dotnetCliPublishArgs = $"publish -c {(release ? "Release" : "Debug")} -r win10-x64 -o {outFolder}"; // NOTE: Disasmo is Windows only

                Output += "\n" + dotnetCli + " " + dotnetCliPublishArgs;
                ctoken.ThrowIfCancellationRequested();
                ProcessResult result = await ProcessUtils.RunProcess(dotnetCli, dotnetCliPublishArgs, workingDir: currentProjectDirPath, cancellationToken: ctoken); 
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    ctoken.ThrowIfCancellationRequested();
                    Output += result.Error;
                    return;
                }

                ctoken.ThrowIfCancellationRequested();
                Output = result.Output;
            }
            catch (OperationCanceledException)
            {
                Output += "Cancelled.";
            }
        }
    }
}
