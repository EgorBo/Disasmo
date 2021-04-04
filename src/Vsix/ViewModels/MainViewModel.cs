using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Project = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;
using Disasmo.Utils;
using Disasmo.ViewModels;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Disasmo
{
    public class MainViewModel : ViewModelBase
    {
        private string _output;
        private string _previousOutput;
        private string _loadingStatus;
        private string _stopwatchStatus;
        private string[] _jitDumpPhases;
        private bool _isLoading;
        private ISymbol _currentSymbol;
        private bool _success;
        private string _currentProjectPath;
        private string _fgPngPath;
        private string DisasmoOutDir = "";
        private const string DefaultJit = "clrjit.dll";

        // let's use new name for the temp folder each version to avoid possible issues (e.g. changes in the Disasmo.Loader)
        private string DisasmoFolder => "Disasmo-v" + DisasmoPackage.Current?.GetCurrentVersion();

        public SettingsViewModel SettingsVm { get; } = new SettingsViewModel();
        public IntrinsicsViewModel IntrinsicsVm { get; } = new IntrinsicsViewModel();

        public event Action MainPageRequested;

        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                // Some design-time data for development
                JitDumpPhases = new []
                    {
                        "Pre-import",
                        "Profile incorporation",
                        "Importation",
                        "Morph - Add internal blocks",
                        "Compute edge weights (1, false)",
                        "Build SSA representation",
                    };
            }
        }

        public string[] JitDumpPhases
        {
            get => _jitDumpPhases;
            set => Set(ref _jitDumpPhases, value);
        }

        public string Output
        {
            get => _output;
            set
            {
                if (!string.IsNullOrWhiteSpace(_output))
                    PreviousOutput = _output;
                Set(ref _output, value);

                const string phasePrefix = "*************** Starting PHASE ";
                JitDumpPhases = (Output ?? "")
                        .Split('\n')
                        .Where(l => l.StartsWith(phasePrefix))
                        .Select(i => i.Replace(phasePrefix, ""))
                        .ToArray();
            }
        }

        public string PreviousOutput
        {
            get => _previousOutput;
            set => Set(ref _previousOutput, value);
        }

        public string LoadingStatus
        {
            get => _loadingStatus;
            set => Set(ref _loadingStatus, value);
        }

        public CancellationTokenSource UserCts { get; set; }

        public CancellationToken UserCt => UserCts?.Token ?? default;

        public void ThrowIfCanceled()
        {
            if (UserCts?.IsCancellationRequested == true)
                throw new OperationCanceledException();
        }

        public ICommand CancelCommand => new RelayCommand(() =>
        {
            try { UserCts?.Cancel(); } catch { }
        });

        public bool Success
        {
            get => _success;
            set => Set(ref _success, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (!_isLoading && value)
                {
                    UserCts = new CancellationTokenSource();
                }
                Set(ref _isLoading, value);
            }
        }

        public string StopwatchStatus
        {
            get => _stopwatchStatus;
            set => Set(ref _stopwatchStatus, value);
        }

        public string FgPngPath
        {
            get => _fgPngPath;
            set => Set(ref _fgPngPath, value);
        }
        

        public ICommand RefreshCommand => new RelayCommand(() => RunOperationAsync(_currentSymbol));

        public ICommand RunDiffWithPrevious => new RelayCommand(() => IdeUtils.RunDiffTools(PreviousOutput, Output));

        public async Task RunFinalExe()
        {
            try
            {
                if (_currentSymbol == null || string.IsNullOrWhiteSpace(_currentProjectPath))
                    return;

                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync();

                Success = false;
                IsLoading = true;
                FgPngPath = null;
                LoadingStatus = "Loading...";

                string dstFolder = DisasmoOutDir;
                if (!Path.IsPathRooted(dstFolder))
                    dstFolder = Path.Combine(Path.GetDirectoryName(_currentProjectPath), DisasmoOutDir);

                // TODO: respect AssemblyName property (if it doesn't match csproj name)
                string fileName = Path.GetFileNameWithoutExtension(_currentProjectPath);

                var envVars = new Dictionary<string, string>();

                string hostType;
                string methodName;
                string target;
                if (_currentSymbol is IMethodSymbol)
                {
                    target = _currentSymbol.ContainingType.Name + "::" + _currentSymbol.Name;
                    hostType = _currentSymbol.ContainingType.ToString();
                    methodName = _currentSymbol.Name;
                }
                else
                {
                    // the whole class
                    target = _currentSymbol.Name + "::*";
                    hostType = _currentSymbol.ToString();
                    methodName = "*";
                }

                if (!SettingsVm.RunAppMode)
                {
                    IdeUtils.SaveEmbeddedResourceTo("Disasmo.Loader.dll", dstFolder);
                    IdeUtils.SaveEmbeddedResourceTo("Disasmo.Loader.deps.json", dstFolder);
                }

                if (SettingsVm.JitDumpInsteadOfDisasm)
                    envVars["COMPlus_JitDump"] = target;
                else
                    envVars["COMPlus_JitDisasm"] = target;

                if (!string.IsNullOrWhiteSpace(SettingsVm.SelectedCustomJit) &&
                    !SettingsVm.SelectedCustomJit.Equals(DefaultJit, StringComparison.InvariantCultureIgnoreCase))
                {
                    envVars["COMPlus_AltJitName"] = SettingsVm.SelectedCustomJit;
                    envVars["COMPlus_AltJit"] = target;
                }

                if (!SettingsVm.UseDotnetPublishForReload)
                {
                    var (runtimePackPath, success) = GetPathToRuntimePack();
                    if (!success)
                        return;

                    // tell jit to look for BCL libs in the locally built runtime pack
                    envVars["CORE_LIBRARIES"] = runtimePackPath;
                }

                envVars["COMPlus_TieredCompilation"] = SettingsVm.UseTieredJit ? "1" : "0";

                // User is free to override any of those ^
                SettingsVm.FillWithUserVars(envVars);


                string currentFgFile = null;
                if (SettingsVm.FgEnable)
                {
                    if (envVars.Keys.Any(k => k.Contains("_JitDumpFg")))
                    {
                        Output = "Please, remove all *_JitDumpFg*=.. variables in 'Settings' tab";
                        return;
                    }

                    if (methodName == "*")
                    {
                        Output = "Flowgraph for classes (all methods) is not supported yet.";
                        return;
                    }

                    currentFgFile = Path.GetTempFileName();
                    envVars["COMPlus_JitDumpFg"] = "*";
                    envVars["COMPlus_JitDumpFgDot"] = "1";
                    envVars["COMPlus_JitDumpFgPhase"] = SettingsVm.FgPhase.Trim();
                    envVars["COMPlus_JitDumpFgFile"] = currentFgFile;
                }

                string command = $"\"Disasmo.Loader.dll\" \"{fileName}.dll\" \"{hostType}\" \"{methodName}\" \"false\"";
                if (SettingsVm.RunAppMode)
                {
                    command = $"\"{fileName}.dll\"";
                }

                // TODO: it'll fail if the project has a custom assembly name (AssemblyName)
                LoadingStatus = $"Executing: " + command;

                string coreRunPath = "dotnet";
                if (!SettingsVm.UseDotnetPublishForReload)
                {
                    var (clrCheckedFilesDir, success) = GetPathToCoreClrChecked();
                    if (!success)
                        return;
                    coreRunPath = Path.Combine(clrCheckedFilesDir, "CoreRun.exe");
                }

                ProcessResult result = await ProcessUtils.RunProcess(
                    coreRunPath, command, envVars, dstFolder, cancellationToken: UserCt);

                ThrowIfCanceled();

                if (string.IsNullOrEmpty(result.Error))
                {
                    Success = true;
                    Output = PreprocessOutput(result.Output);
                }
                else
                {
                    Output = result.Error;
                }

                if (SettingsVm.FgEnable && SettingsVm.JitDumpInsteadOfDisasm)
                {
                    currentFgFile += ".dot";
                    if (!File.Exists(currentFgFile))
                    {
                        Output = $"Oops, JitDumpFgFile ('{currentFgFile}') doesn't exist :(\nInvalid Phase name?";
                        return;
                    }

                    if (new FileInfo(currentFgFile).Length == 0)
                    {
                        Output = $"Oops, JitDumpFgFile ('{currentFgFile}') file is empty :(\nInvalid Phase name?";
                        return;
                    }

                    var fgLines = File.ReadAllLines(currentFgFile);
                    if (fgLines.Count(l => l.StartsWith("digraph FlowGraph")) > 1)
                    {
                        int removeTo = fgLines.Select((l, i) => new {line = l, index = i}).Last(i => i.line.StartsWith("digraph FlowGraph")).index;
                        File.WriteAllLines(currentFgFile, fgLines.Skip(removeTo).ToArray());
                    }

                    ThrowIfCanceled();

                    var pngPath = Path.GetTempFileName();
                    string dotExeArgs = $"-Tpng -o\"{pngPath}\" -Kdot \"{currentFgFile}\"";
                    ProcessResult dotResult = await ProcessUtils.RunProcess(SettingsVm.GraphvisDotPath, dotExeArgs, cancellationToken: UserCt);

                    ThrowIfCanceled();

                    if (!File.Exists(pngPath) || new FileInfo(pngPath).Length == 0)
                    {
                        Output = "Graphvis failed:\n" + dotResult.Output + "\n\n" + dotResult.Error;
                        return;
                    }

                    FgPngPath = pngPath;
                }

            }
            catch (OperationCanceledException e)
            {
                Output = e.Message;
            }
            catch (Exception e)
            {
                Output = e.ToString();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string PreprocessOutput(string output)
        {
            if (SettingsVm.JitDumpInsteadOfDisasm)
                return output;
            return ComPlusDisassemblyPrettifier.Prettify(output, !SettingsVm.ShowAsmComments && !SettingsVm.RunAppMode);
        }

        private UnconfiguredProject GetUnconfiguredProject(EnvDTE.Project project)
        {
            var context = project as IVsBrowseObjectContext;
            if (context == null && project != null) 
                context = project.Object as IVsBrowseObjectContext;

            return context?.UnconfiguredProject;
        }

        private (string, bool) GetPathToRuntimePack()
        {
            var (_, success) = GetPathToCoreClrChecked();
            if (!success)
                return (null, false);

            var runtimePackPath = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\runtime\net6.0-windows-Release-x64");
            if (!Directory.Exists(runtimePackPath))
            {
                Output = "'dotnet build' reload strategy requires a win-x64 runtimepack to be built for Release config\n\n" +
                         "Run 'build.cmd Clr+Libs -c Release' in order to build it\nYou won't have to re-build every time you change something in the jit/vm/corelib.";
                return (null, false);
            }
            return (runtimePackPath, true);
        }

        private (string, bool) GetPathToCoreClrChecked()
        {
            var clrCheckedFilesDir = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\coreclr\windows.x64.Checked");
            if (string.IsNullOrWhiteSpace(SettingsVm.PathToLocalCoreClr) ||
                !Directory.Exists(clrCheckedFilesDir))
            {
                Output = "Path to a local dotnet/runtime repository is either not set or it's not built yet\nPlease clone it and build it in `Checked` mode, e.g.:\n\n" +
                         "git clone git@github.com:dotnet/runtime.git\n" +
                         "cd runtime\n" +
                         "build.cmd Clr -c Checked\n\n" +
                         "" +
                         "Also, consider running 'build.cmd Clr+Libs -c Release' additionally if you want to use 'Run Method in a loop' experimental feature." +
                         "See https://github.com/dotnet/runtime/blob/master/docs/workflow/requirements/windows-requirements.md";
                return (null, false);
            }
            return (clrCheckedFilesDir, true);
        }

        public async void RunOperationAsync(ISymbol symbol)
        {
            var stopwatch = Stopwatch.StartNew();
            DTE dte = IdeUtils.DTE();

            try
            {
                IsLoading = true;
                FgPngPath = null;
                await Task.Delay(50);
                MainPageRequested?.Invoke();
                Success = false;
                _currentSymbol = symbol;
                Output = "";

                if (symbol == null)
                    return;

                var (clrCheckedFilesDir, success) = GetPathToCoreClrChecked();
                if (!success)
                    return;

                if (symbol is IMethodSymbol method && method.IsGenericMethod)
                {
                    // TODO: ask user to specify type parameters
                    Output = "Generic methods are not supported yet.";
                    return;
                }

                ThrowIfCanceled();

                // Find Release-x64 configuration:
                Project currentProject = dte.GetActiveProject();
                UnconfiguredProject unconfiguredProject = GetUnconfiguredProject(currentProject);

                // it will throw "Release config was not found" to the Output if there is no such config in the project
                ProjectConfiguration releaseConfig = await unconfiguredProject.Services.ProjectConfigurationsService.GetProjectConfigurationAsync("Release");
                ConfiguredProject configuredProject = await unconfiguredProject.LoadConfiguredProjectAsync(releaseConfig);
                IProjectProperties projectProperties = configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();

                ThrowIfCanceled();

                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync();
                _currentProjectPath = currentProject.FileName;

                string targetFramework = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFramework");
                targetFramework = targetFramework.ToLowerInvariant().Trim();

                ThrowIfCanceled();

                if (targetFramework.StartsWith("net") &&
                    float.TryParse(targetFramework.Remove(0, "net".Length), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float netVer) &&
                    netVer >= 5)
                {
                    // the project is net5 or newer
                }
                else
                {
                    Output =
                        "Only net5.0 (and later) apps are supported.\nMake sure <TargetFramework>net5.0</TargetFramework> is set in your csproj.";
                    return;
                }

                if (SettingsVm.RunAppMode && SettingsVm.UseDotnetPublishForReload)
                {
                    // TODO: fix this
                    Output = "\"Run current app\" mode only works with \"dotnet build\" reload strategy, see Options tab.";
                    return;
                }

                // Validation for Flowgraph tab
                if (SettingsVm.FgEnable)
                {
                    var phase = SettingsVm.FgPhase.Trim();
                    if (phase == "*")
                    {
                        Output = "* as a phase name is not supported yet."; // TODO: implement
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(SettingsVm.GraphvisDotPath) ||
                        !File.Exists(SettingsVm.GraphvisDotPath))
                    {
                        Output = "Graphvis is not installed or path to dot.exe is incorrect, see 'Settings' tab.\nGraphvis can be installed from https://graphviz.org/download/";
                        return;
                    }

                    if (!SettingsVm.JitDumpInsteadOfDisasm)
                    {
                        Output = "Either disable flowgraphs in the 'Flowgraph' tab or enable JitDump.";
                        return;
                    }
                }

                DisasmoOutDir = Path.Combine(await projectProperties.GetEvaluatedPropertyValueAsync("OutputPath"), DisasmoFolder);
                string currentProjectDirPath = Path.GetDirectoryName(_currentProjectPath);

                dte.SaveAllActiveDocuments();

                ProcessResult publishResult;
                if (SettingsVm.UseDotnetPublishForReload)
                {
                    LoadingStatus = $"dotnet publish -r win-x64 -c Release -o ...";

                    string dotnetPublishArgs =
                        $"publish -r win-x64 -c Release -o {DisasmoOutDir} --self-contained true /p:PublishTrimmed=false /p:PublishSingleFile=false /p:WarningLevel=0 /p:TreatWarningsAsErrors=false";

                    publishResult = await ProcessUtils.RunProcess("dotnet", dotnetPublishArgs, null, currentProjectDirPath, cancellationToken: UserCt);
                }
                else
                {
                    var (_, rpSuccess) = GetPathToRuntimePack();
                    if (!rpSuccess)
                        return;
                    LoadingStatus = $"dotnet build -c Release -o ...";

                    string dotnetBuildArgs = $"build -c Release -o {DisasmoOutDir} /p:WarningLevel=0 /p:TreatWarningsAsErrors=false";
                    
                    if (SettingsVm.UseNoRestoreFlag)
                        dotnetBuildArgs += " --no-restore";

                    publishResult = await ProcessUtils.RunProcess("dotnet", dotnetBuildArgs, null,
                        currentProjectDirPath,
                        cancellationToken: UserCt);
                }
                ThrowIfCanceled();

                if (!string.IsNullOrEmpty(publishResult.Error))
                {
                    Output = publishResult.Error;
                    return;
                }

                // in case if there are compilation errors:
                if (publishResult.Output.Contains(": error"))
                {
                    Output = publishResult.Output;
                    return;
                }

                if (SettingsVm.UseDotnetPublishForReload)
                {
                    LoadingStatus = "Copying files from locally built CoreCLR";

                    string dstFolder = DisasmoOutDir;
                    if (!Path.IsPathRooted(dstFolder))
                        dstFolder = Path.Combine(currentProjectDirPath, DisasmoOutDir);
                    if (!Directory.Exists(dstFolder))
                    {
                        Output =
                            $"Something went wrong, {dstFolder} doesn't exist after 'dotnet publish -r win-x64 -c Release' step";
                        return;
                    }

                    var copyClrFilesResult = await ProcessUtils.RunProcess("robocopy",
                        $"/e \"{clrCheckedFilesDir}\" \"{dstFolder}", null, cancellationToken: UserCt);

                    if (!string.IsNullOrEmpty(copyClrFilesResult.Error))
                    {
                        Output = copyClrFilesResult.Error;
                        return;
                    }
                }

                ThrowIfCanceled();
                await RunFinalExe();
            }
            catch (OperationCanceledException e)
            {
                Output = e.Message;
            }
            catch (Exception e)
            {
                Output = e.ToString();
            }
            finally
            {
                IsLoading = false;
                stopwatch.Stop();
                StopwatchStatus = $"Disasm took {stopwatch.Elapsed.TotalSeconds:F1} s.";
            }
        }
    }
}