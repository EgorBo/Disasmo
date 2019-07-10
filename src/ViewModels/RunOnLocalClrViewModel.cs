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

        public bool SkipPublishStep
        {
            get => skipPublishStep;
            set => Set(ref skipPublishStep, value);
        }

        public string Output
        {
            get => output;
            set => Set(ref output, value);
        }

        private CancellationTokenSource _cts;
        private bool skipPublishStep;

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

                if (currentProject == null)
                {
                    Output = "Active project is null.";
                    return;
                }

                var neededConfig = currentProject.GetReleaseConfig();
                if (neededConfig == null)
                {
                    Output = "Couldn't find any 'Release - x64' or 'Release - Any CPU' configuration.";
                    return;
                }

                string currentProjectPath = currentProject.FileName;

                // unfortunately both old VS API and new crashes for me on my vs2019preview2 (see https://github.com/dotnet/project-system/issues/669 and the workaround - both crash)
                // ugly hack for OutputType:
                if (!File.ReadAllText(currentProjectPath).ToLower().Contains("<outputtype>exe<"))
                {
                    Output = "At this moment only .NET Core Сonsole Applications (`<OutputType>Exe</OutputType>`) are supported.\nFeel free to contribute multi-project support :-)";
                    return;
                }

                string currentProjectDirPath = Path.GetDirectoryName(currentProjectPath);

                const string outFolder = "DisasmoLocalRun";
                string dotnetCli = "dotnet"; // from PATH
                string dotnetCliPublishArgs = $"publish -c {(release ? "Release" : "Debug")} -r win10-x64 -o {outFolder}"; // NOTE: Disasmo is Windows only

                ctoken.ThrowIfCancellationRequested();

                string dst = Path.Combine(currentProjectDirPath, outFolder);

                ProcessResult result;
                if (!Directory.Exists(dst) || !SkipPublishStep)
                {
                    Output += $"\n{dotnetCli} {dotnetCliPublishArgs}";
                    result = await ProcessUtils.RunProcess(dotnetCli, dotnetCliPublishArgs, workingDir: currentProjectDirPath, cancellationToken: ctoken);
                    ctoken.ThrowIfCancellationRequested();
                    if (!string.IsNullOrWhiteSpace(result.Error))
                    {
                        Output += $"\n{result.Error}";
                        return;
                    }
                }

                if (!Directory.Exists(dst))
                {
                    Output = $"Something went wrong, {dst} doesn't exist after 'dotnet publish'";
                    return;
                }

                var clrReleaseFiles = Path.Combine(_settingsVm.PathToLocalCoreClr, @"bin\Product\Windows_NT.x64.Release");

                Output += $"\nCopying files from local CoreCLR...";

                if (!Directory.Exists(clrReleaseFiles))
                {
                    Output = $"Folder + {clrReleaseFiles} does not exist. Please follow instructions at\n https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md";
                    return;
                }

                var copyClrReleaseResult = await ProcessUtils.RunProcess("robocopy", $"/e \"{clrReleaseFiles}\" \"{dst}", null);
                if (!string.IsNullOrEmpty(copyClrReleaseResult.Error))
                {
                    Output = copyClrReleaseResult.Error;
                    return;
                }

                var clrJitFile = Path.Combine(_settingsVm.PathToLocalCoreClr, @"bin\Product\Windows_NT.x64.Debug\clrjit.dll");
                if (!File.Exists(clrJitFile))
                {
                    Output = $"File + {clrJitFile} does not exist. Please follow instructions at\n https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md";
                    return;
                }

                File.Copy(clrJitFile, Path.Combine(dst, "clrjit.dll"), true);


                string exeName = $@"{Path.GetFileNameWithoutExtension(currentProjectPath)}.exe";
                string finalExe = Path.Combine(Path.GetDirectoryName(currentProjectPath), outFolder, exeName);

                result = await ProcessUtils.RunProcess(finalExe, cancellationToken: ctoken);
                ctoken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    Output += result.Error;
                    return;
                }

                Output = result.Output;
            }
            catch (OperationCanceledException)
            {
                Output += "\nCancelled.";
            }
            catch (Exception exc)
            {
                Output = exc.ToString();
            }
        }
    }
}
