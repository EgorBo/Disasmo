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
        private string _currentTf;
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
                if (_currentSymbol is IMethodSymbol ms)
                {
                    if (ms.MethodKind == MethodKind.LocalFunction)
                    {
                        // just print them all, I don't know how to get "g__%MethodName|0_0" ugly name out of 
                        // IMethodSymbol in order to pass it to JitDisasm. Ugh, I hate it.
                        target = _currentSymbol.ContainingType.Name + "::*";
                        hostType = _currentSymbol.ContainingType.ToString();
                        methodName = "*";
                    }
                    else
                    {
                        target = _currentSymbol.ContainingType.Name + "::" + _currentSymbol.Name;
                        hostType = _currentSymbol.ContainingType.ToString();
                        methodName = _currentSymbol.Name;

                        if (hostType.EndsWith(">$"))
                        {
                            // A hack for local/global functions
                            target = $"<{_currentSymbol.ContainingSymbol.Name}>g__{methodName}*";
                        }
                    }
                }
                else
                {
                    // the whole class
                    target = _currentSymbol.Name + "::*";
                    hostType = _currentSymbol.ToString();
                    methodName = "*";
                }

                if (!SettingsVm.RunAppMode && !SettingsVm.CrossgenIsSelected)
                {
                    await LoaderAppManager.InitLoaderAndCopyTo(_currentTf, dstFolder, log => { /*TODO: update UI*/ }, UserCt);
                }

                if (SettingsVm.JitDumpInsteadOfDisasm)
                    envVars["DOTNET_JitDump"] = target;
                else if (SettingsVm.PrintInlinees)
                    envVars["DOTNET_JitPrintInlinedMethods"] = target;
                else
                    envVars["DOTNET_JitDisasm"] = target;

                if (!string.IsNullOrWhiteSpace(SettingsVm.SelectedCustomJit) && !SettingsVm.CrossgenIsSelected &&
                    !SettingsVm.SelectedCustomJit.Equals(DefaultJit, StringComparison.InvariantCultureIgnoreCase) && SettingsVm.UseCustomRuntime)
                {
                    envVars["DOTNET_AltJitName"] = SettingsVm.SelectedCustomJit;
                    envVars["DOTNET_AltJit"] = target;
                }

                envVars["DOTNET_TieredPGO"] = SettingsVm.UsePGO ? "1" : "0";

                if (!SettingsVm.UseDotnetPublishForReload && SettingsVm.UseCustomRuntime)
                {
                    var (runtimePackPath, success) = GetPathToRuntimePack();
                    if (!success)
                        return;

                    // tell jit to look for BCL libs in the locally built runtime pack
                    envVars["CORE_LIBRARIES"] = runtimePackPath;
                }

                envVars["DOTNET_TieredCompilation"] = SettingsVm.UseTieredJit ? "1" : "0";

                // User is free to override any of those ^
                SettingsVm.FillWithUserVars(envVars);


                string currentFgFile = null;
                if (SettingsVm.FgEnable)
                {
                    if (methodName == "*")
                    {
                        Output = "Flowgraph for classes (all methods) is not supported yet.";
                        return;
                    }

                    currentFgFile = Path.GetTempFileName();
                    envVars["DOTNET_JitDumpFg"] = target;
                    envVars["DOTNET_JitDumpFgDot"] = "1";
                    envVars["DOTNET_JitDumpFgPhase"] = SettingsVm.FgPhase.Trim();
                    envVars["DOTNET_JitDumpFgFile"] = currentFgFile;
                }

                string command = $"\"{LoaderAppManager.DisasmoLoaderName}.dll\" \"{fileName}.dll\" \"{hostType}\" \"{methodName}\"";
                if (SettingsVm.RunAppMode)
                {
                    command = $"\"{fileName}.dll\"";
                }

                string executable = "dotnet";

                if (SettingsVm.CrossgenIsSelected && SettingsVm.UseCustomRuntime)
                {
                    var (clrCheckedFilesDir, checkedFound) = GetPathToCoreClrChecked();
                    if (!checkedFound)
                        return;

                    var (runtimePackPath, runtimePackFound) = GetPathToRuntimePack();
                    if (!runtimePackFound)
                        return;

                    command = "";
                    executable = Path.Combine(clrCheckedFilesDir, "crossgen2", "crossgen2.exe");

                    command += " --out aot ";

                    foreach (var envVar in envVars)
                    {
                        var keyLower = envVar.Key.ToLowerInvariant();
                        if (keyLower?.StartsWith("dotnet_") == false &&
                            keyLower?.StartsWith("complus_") == false)
                        {
                            continue;
                        }

                        keyLower = keyLower
                            .Replace("dotnet_jitdump", "--codegenopt:jitdump")
                            .Replace("dotnet_jitdisasm", "--codegenopt:jitdisasm")
                            .Replace("dotnet_", "--codegenopt:")
                            .Replace("complus_", "--codegenopt:");
                        command += keyLower + "=\"" + envVar.Value + "\" ";
                    }

                    // These are needed for faster crossgen itself - they're not changing output codegen
                    envVars["DOTNET_TieredPGO"] = "0";
                    envVars["DOTNET_ReadyToRun"] = "1";
                    envVars["DOTNET_TC_QuickJitForLoops"] = "1";
                    envVars["DOTNET_TieredCompilation"] = "1";
                    command += SettingsVm.Crossgen2Args + $" \"{fileName}.dll\" ";

                    if (SettingsVm.UseDotnetPublishForReload)
                    {
                        // Reference everything in the publish dir
                        command += $" -r: \"{dstFolder}\\*.dll\" ";
                    }
                    else
                    {
                        // the runtime pack we use doesn't contain corelib so let's use "checked" corelib
                        // TODO: build proper core_root with release version of corelib
                        var corelib = Path.Combine(clrCheckedFilesDir, "System.Private.CoreLib.dll");
                        command += $" -r: \"{runtimePackPath}\\*.dll\" -r: \"{corelib}\" ";
                    }

                    LoadingStatus = $"Executing crossgen2...";
                }
                else if (SettingsVm.IsNonCustomDotnetAotMode())
                {
                    // TODO:
                    // dotnet publish /p:NativeAot and /p:PublishReadyToRun don't print anything (msbuild hides stdout)
                }
                else
                {
                    LoadingStatus = $"Executing DisasmoLoader...";
                }


                if (!SettingsVm.UseDotnetPublishForReload && !SettingsVm.CrossgenIsSelected && SettingsVm.UseCustomRuntime)
                {
                    var (clrCheckedFilesDir, success) = GetPathToCoreClrChecked();
                    if (!success)
                        return;
                    executable = Path.Combine(clrCheckedFilesDir, "CoreRun.exe");
                }

                ProcessResult result = await ProcessUtils.RunProcess(
                    executable, command, envVars, dstFolder, cancellationToken: UserCt);

                ThrowIfCanceled();

                if (string.IsNullOrEmpty(result.Error))
                {
                    Success = true;
                    Output = PreprocessOutput(result.Output);
                }
                else
                {
                    Output = result.Output + "\nERROR:\n" + result.Error;
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
            if (SettingsVm.JitDumpInsteadOfDisasm || SettingsVm.PrintInlinees)
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

        private (string, bool) GetPathToRuntimePack(string arch = "x64")
        {
            var (_, success) = GetPathToCoreClrChecked(arch);
            if (!success)
                return (null, false);

            string runtimePacksPath = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\runtime");
            string runtimePackPath = null;
            if (Directory.Exists(runtimePacksPath))
            {
                var packs = Directory.GetDirectories(runtimePacksPath, "*-windows-Release-" + arch);
                runtimePackPath = packs.OrderByDescending(i => i).FirstOrDefault();
            }

            if (!Directory.Exists(runtimePackPath))
            {
                Output = "Please, build a runtime-pack in your local repo:\n\n" +
                         $"Run 'build.cmd Clr+Libs -c Release -a {arch}' in the repo root\n" + 
                         "Don't worry, you won't have to re-build it every time you change something in jit, vm or corelib.";
                return (null, false);
            }
            return (runtimePackPath, true);
        }

        private (string, bool) GetPathToCoreClrChecked(string arch = "x64")
        {
            var clrCheckedFilesDir = FindJitDirectory(SettingsVm.PathToLocalCoreClr, arch);
            if (string.IsNullOrWhiteSpace(clrCheckedFilesDir))
            {
                Output = $"Path to a local dotnet/runtime repository is either not set or it's not built for {arch} arch yet" +
                         (SettingsVm.CrossgenIsSelected ? "\n(When you use crossgen and target e.g. arm64 you need coreclr built for that arch)" : "") + 
                         "\nPlease clone it and build it in `Checked` mode, e.g.:\n\n" +
                         "git clone git@github.com:dotnet/runtime.git\n" +
                         "cd runtime\n" +
                         $"build.cmd Clr+Libs -c Release -rc Checked -a {arch}\n\n";
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

                string clrCheckedFilesDir = null;
                if (SettingsVm.UseCustomRuntime)
                {
                    var (dir, success) = GetPathToCoreClrChecked();
                    if (!success)
                        return;
                    clrCheckedFilesDir = dir;
                }

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

                _currentTf = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFramework");
                _currentTf = _currentTf.ToLowerInvariant().Trim();

                ThrowIfCanceled();

                float netVer = 0;
                if (_currentTf.StartsWith("net") &&
                    float.TryParse(_currentTf.Remove(0, "net".Length), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out netVer) && netVer >= 5)
                {
                    if (!SettingsVm.UseCustomRuntime && netVer < 7)
                    {
                        Output =
                            "Only net7.0 (and newer) apps are supported with non-locally built dotnet/runtime.\nMake sure <TargetFramework>net7.0</TargetFramework> is set in your csproj.";
                        return;
                    }
                }
                else
                {
                    Output =
                        "Only net5.0 (and newer) apps are supported.\nMake sure <TargetFramework>net5.0</TargetFramework> is set in your csproj.";
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

                if (SettingsVm.CrossgenIsSelected)
                {
                    if (SettingsVm.UsePGO)
                    {
                        Output = "PGO has no effect on R2R'd code (yet).";
                        return;
                    }

                    if (SettingsVm.RunAppMode)
                    {
                        Output = "Run mode is not supported for crossgen";
                        return;
                    }

                    if (SettingsVm.UseTieredJit)
                    {
                        Output = "TieredJIT has no effect on R2R'd code.";
                        return;
                    }

                    if (SettingsVm.FgEnable)
                    {
                        Output = "Flowgraphs are not tested with crossgen2 yet (in Disasmo)";
                        return;
                    }
                }

                DisasmoOutDir = Path.Combine(await projectProperties.GetEvaluatedPropertyValueAsync("OutputPath"), DisasmoFolder);
                string currentProjectDirPath = Path.GetDirectoryName(_currentProjectPath);

                dte.SaveAllActiveDocuments();

                if (SettingsVm.IsNonCustomDotnetAotMode())
                {
                    ThrowIfCanceled();
                    await RunFinalExe();
                    return;
                }

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
                    if (SettingsVm.UseCustomRuntime)
                    {
                        var (_, rpSuccess) = GetPathToRuntimePack();
                        if (!rpSuccess)
                            return;
                    }

                    LoadingStatus = $"dotnet build -c Release -o ...";

                    string dotnetBuildArgs = $"build -c Release -o {DisasmoOutDir} /p:WarningLevel=0 /p:TreatWarningsAsErrors=false";
                    
                    if (SettingsVm.UseNoRestoreFlag)
                        dotnetBuildArgs += " --no-restore --no-dependencies --nologo";

                    var fasterBuildArgs = new Dictionary<string, string>
                    {
                        ["DOTNET_TC_QuickJitForLoops"] = "1", // slightly speeds up build (not needed for >=.net7.0)
                        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
                        ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
                        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
                    };
                    publishResult = await ProcessUtils.RunProcess("dotnet", dotnetBuildArgs, fasterBuildArgs,
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

                if (SettingsVm.UseDotnetPublishForReload && SettingsVm.UseCustomRuntime)
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

        private static string FindJitDirectory(string basePath, string arch)
        {
            string jitDir = Path.Combine(basePath, $@"artifacts\bin\coreclr\windows.{arch}.Checked");
            if (Directory.Exists(jitDir))
            {
                return jitDir;
            }

            jitDir = Path.Combine(basePath, $@"artifacts\bin\coreclr\windows.{arch}.Debug");
            if (Directory.Exists(jitDir))
            {
                return jitDir;
            }

            return null;
        }
    }
}