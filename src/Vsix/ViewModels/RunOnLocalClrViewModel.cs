using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.IO;
using System.Threading;
using System.Windows.Input;

namespace Disasmo.ViewModels
{
    public class RunOnLocalClrViewModel : ViewModelBase
    {
        private readonly SettingsViewModel _settingsVm;
        private string _output;
        private bool _isBusy;

        public RunOnLocalClrViewModel() { }

        public RunOnLocalClrViewModel(SettingsViewModel settingsVm)
        {
            _settingsVm = settingsVm;
        }

        public bool Release
        {
            get => release;
            set => Set(ref release, value);
        }

        public ICommand RunApp => new RelayCommand(() => RunCurrentApp(Release));

        public ICommand RebuildMscorlib => new RelayCommand(() => RebuildMscorlibFor(Release));

        public ICommand RebuildCoreclr => new RelayCommand(() => RebuildCoreclrFor(Release));

        public ICommand RebuildEverything => new RelayCommand(() => 
            {
                RebuildEverythingFor(true);
                RebuildEverythingFor(false);
            });

        public bool SkipPublishStep
        {
            get => skipPublishStep;
            set => Set(ref skipPublishStep, value);
        }

        public string Output
        {
            get => _output;
            set => Set(ref _output, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        private CancellationTokenSource _cts;
        private bool skipPublishStep;
        private bool release;

        private string ValidateParameters(CancellationToken ctoken, bool requireActiveProject)
        {
            Output = "Working...";
            if (string.IsNullOrWhiteSpace(_settingsVm.PathToLocalCoreClr))
            {
                ctoken.ThrowIfCancellationRequested();
                Output += "Path to a local CoreCLR is not set ^.";
                return null;
            }

            if (!requireActiveProject)
                return _settingsVm.PathToLocalCoreClr;

            DTE dte = IdeUtils.DTE();
            ctoken.ThrowIfCancellationRequested();

            // Find Release-x64 configuration:
            Project currentProject = dte.GetActiveProject();
            ctoken.ThrowIfCancellationRequested();

            if (currentProject == null)
            {
                Output = "Active project is null.";
                return null;
            }

            var neededConfig = currentProject.GetReleaseConfig();
            ctoken.ThrowIfCancellationRequested();
            if (neededConfig == null)
            {
                Output = "Couldn't find any 'Release - x64' or 'Release - Any CPU' configuration.";
                return null;
            }

            string currentProjectPath = currentProject.FileName;

            // unfortunately both old VS API and new crashes for me on my vs2019preview2 (see https://github.com/dotnet/project-system/issues/669 and the workaround - both crash)
            // ugly hack for OutputType:
            if (!File.ReadAllText(currentProjectPath).ToLower().Contains("<outputtype>exe<"))
            {
                Output = "At this moment only .NET Core Сonsole Applications (`<OutputType>Exe</OutputType>`) are supported.\nFeel free to contribute multi-project support :-)";
                return null;
            }

            return currentProjectPath;
        }

        private async void RebuildMscorlibFor(bool release)
        {
            IsBusy = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ctoken = _cts.Token;

            try
            {
                string localClr = ValidateParameters(ctoken, false);
                if (localClr == null)
                    return;

                string buildArgs = $"-x64 -{(release ? "release" : "debug")} -skipnative -skipcrossarchnative -skiptests -skipbuildpackages -skipmanagedtools -skiprestore -skiprestoreoptdata";
                string buildcmd = Path.Combine(localClr, "build.cmd");
                ProcessResult result = await ProcessUtils.RunProcess(buildcmd, buildArgs, workingDir: localClr, outputLogger: (isError, logLine) => Output += logLine, cancellationToken: ctoken);
                ctoken.ThrowIfCancellationRequested();
                Output += "\n\nDONE!";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exc)
            {
                Output += $"\n{exc.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void RebuildCoreclrFor(bool release)
        {
            IsBusy = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ctoken = _cts.Token;

            try
            {
                string localClr = ValidateParameters(ctoken, false);
                if (localClr == null)
                    return;

                ctoken.ThrowIfCancellationRequested();
                string buildArgs = $"-x64 -{(release ? "release" : "debug")} -skipmscorlib -skipcrossarchnative -skiptests -skipbuildpackages -skipmanagedtools -skiprestore -skiprestoreoptdata";
                string buildcmd = Path.Combine(localClr, "build.cmd");
                ProcessResult result = await ProcessUtils.RunProcess(buildcmd, buildArgs, workingDir: localClr, outputLogger: (isError, logLine) => Output += logLine, cancellationToken: ctoken);
                ctoken.ThrowIfCancellationRequested();
                Output += "\n\nDONE!";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exc)
            {
                Output += $"\n{exc.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void RebuildEverythingFor(bool release)
        {
            IsBusy = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ctoken = _cts.Token;

            try
            {
                string localClr = ValidateParameters(ctoken, false);
                if (localClr == null)
                    return;

                ctoken.ThrowIfCancellationRequested();
                string buildArgs = $"-x64 -{(release ? "release" : "debug")} -skiptests";
                string buildcmd = Path.Combine(localClr, "build.cmd");
                ProcessResult result = await ProcessUtils.RunProcess(buildcmd, buildArgs, workingDir: localClr, outputLogger: (isError, logLine) => Output += logLine, cancellationToken: ctoken);
                ctoken.ThrowIfCancellationRequested();
                Output += "\n\nDONE!";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exc)
            {
                Output += $"\n{exc.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void RunCurrentApp(bool release)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ctoken = _cts.Token;
            try
            {
                string currentProjectPath = ValidateParameters(ctoken, true);
                if (currentProjectPath == null)
                    return;

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
