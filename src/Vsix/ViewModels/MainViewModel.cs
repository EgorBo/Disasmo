﻿using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Project = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;
using Disasmo.ViewModels;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System.Collections.ObjectModel;
using CAProject = Microsoft.CodeAnalysis.Project;

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
        private CAProject _currentProject;
        private bool _success;
        private string _currentProjectPath;
        private string _currentTf;
        private string _fgPngPath;
        private string DisasmoOutDir = "";
        private ObservableCollection<FlowgraphItemViewModel> _fgPhases = new();
        private FlowgraphItemViewModel _selectedPhase;

        // let's use new name for the temp folder each version to avoid possible issues (e.g. changes in the Disasmo.Loader)
        private string DisasmoFolder => "Disasmo-v" + DisasmoPackage.Current?.GetCurrentVersion();

        public SettingsViewModel SettingsVm { get; } = new();
        public IntrinsicsViewModel IntrinsicsVm { get; } = new();

        public event Action MainPageRequested;

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

        public string DefaultHotKey => DisasmoPackage.HotKey;

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

        public ICommand RefreshCommand => new RelayCommand(() => RunOperationAsync(_currentSymbol, _currentProject));

        public ICommand RunDiffWithPrevious => new RelayCommand(() => IdeUtils.RunDiffTools(PreviousOutput, Output));

        public ICommand OpenInVSCode => new RelayCommand(() => IdeUtils.OpenInVSCode(Output));

        public ICommand OpenInVS => new RelayCommand(() => IdeUtils.OpenInVS(Output));

        public ObservableCollection<FlowgraphItemViewModel> FgPhases
        {
            get => _fgPhases;
            set => Set(ref _fgPhases, value);
        }

        public FlowgraphItemViewModel SelectedPhase
        {
            get => _selectedPhase;
            set
            {
                Set(ref _selectedPhase, value);
                _selectedPhase?.LoadImageAsync(UserCt);
            }
        }

        public async Task RunFinalExe(DisasmoSymbolInfo symbolInfo, IProjectProperties projectProperties)
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

                try
                {
                    if (projectProperties != null)
                    {
                        string customAsmName = await projectProperties.GetEvaluatedPropertyValueAsync("AssemblyName");
                        if (!string.IsNullOrWhiteSpace(customAsmName))
                        {
                            fileName = customAsmName;
                        }
                    }
                }
                catch { }

                var envVars = new Dictionary<string, string>();

                if (!SettingsVm.RunAppMode && !SettingsVm.CrossgenIsSelected && !SettingsVm.NativeAotIsSelected)
                {
                    var addinVersion = DisasmoPackage.Current.GetCurrentVersion();
                    await LoaderAppManager.InitLoaderAndCopyTo(_currentTf, dstFolder, log => { /*TODO: update UI*/ }, addinVersion, UserCt);
                }

                if (SettingsVm.JitDumpInsteadOfDisasm)
                    envVars["DOTNET_JitDump"] = symbolInfo.Target;
                else if (SettingsVm.PrintInlinees)
                    envVars["DOTNET_JitPrintInlinedMethods"] = symbolInfo.Target;
                else
                    envVars["DOTNET_JitDisasm"] = symbolInfo.Target;

                if (!string.IsNullOrWhiteSpace(SettingsVm.SelectedCustomJit) && !SettingsVm.CrossgenIsSelected && !SettingsVm.NativeAotIsSelected &&
                    !SettingsVm.SelectedCustomJit.Equals(Constants.DefaultJit, StringComparison.InvariantCultureIgnoreCase) && SettingsVm.UseCustomRuntime)
                {
                    envVars["DOTNET_AltJitName"] = SettingsVm.SelectedCustomJit;
                    envVars["DOTNET_AltJit"] = symbolInfo.Target;
                }

                envVars["DOTNET_TieredPGO"] = SettingsVm.UsePGO ? "1" : "0";
                envVars["DOTNET_JitDisasmDiffable"] = SettingsVm.Diffable ? "1" : "0";

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
                    if (symbolInfo.MethodName == "*")
                    {
                        Output = "Flowgraph for classes (all methods) is not supported yet.";
                        return;
                    }

                    currentFgFile = Path.GetTempFileName();
                    envVars["DOTNET_JitDumpFg"] = symbolInfo.Target;
                    envVars["DOTNET_JitDumpFgDot"] = "1";
                    envVars["DOTNET_JitDumpFgPhase"] = "*";
                    envVars["DOTNET_JitDumpFgFile"] = currentFgFile;
                }

                string command = $"\"{LoaderAppManager.DisasmoLoaderName}.dll\" \"{fileName}.dll\" \"{symbolInfo.ClassName}\" \"{symbolInfo.MethodName}\" {SettingsVm.UseUnloadableContext}";
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

                    executable = Path.Combine(SettingsVm.PathToLocalCoreClr, "dotnet.cmd");
                    command = $"{Path.Combine(clrCheckedFilesDir, "crossgen2", "crossgen2.dll")} --out aot ";

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
                    envVars.Clear();

                    // These are needed for faster crossgen itself - they're not changing output codegen
                    envVars["DOTNET_TieredPGO"] = "0";
                    envVars["DOTNET_ReadyToRun"] = "1";
                    envVars["DOTNET_TC_QuickJitForLoops"] = "1";
                    envVars["DOTNET_TC_CallCountingDelayMs"] = "0";
                    envVars["DOTNET_TieredCompilation"] = "1";
                    command += SettingsVm.Crossgen2Args.Replace("\r\n", " ").Replace("\n", " ") + $" \"{fileName}.dll\" ";

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
                else if (SettingsVm.NativeAotIsSelected && SettingsVm.UseCustomRuntime)
                {
                    var (clrReleaseFolder, clrFound) = GetPathToCoreClrCheckedForNativeAot();
                    if (!clrFound)
                        return;

                    command = "";
                    executable = Path.Combine(clrReleaseFolder, "ilc", "ilc.exe");

                    command += $" \"{fileName}.dll\" ";

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
                    envVars.Clear();
                    command += SettingsVm.IlcArgs.Replace("%DOTNET_REPO%", SettingsVm.PathToLocalCoreClr.TrimEnd('\\', '/')).Replace("\r\n", " ").Replace("\n", " ");

                    if (SettingsVm.UseDotnetPublishForReload)
                    {
                        // Reference everything in the publish dir
                        command += $" -r: \"{dstFolder}\\*.dll\" ";
                    }
                    else
                    {
                        // the runtime pack we use doesn't contain corelib so let's use "checked" corelib
                        // TODO: build proper core_root with release version of corelib
                        //var corelib = Path.Combine(clrCheckedFilesDir, "System.Private.CoreLib.dll");
                        //command += $" -r: \"{runtimePackPath}\\*.dll\" -r: \"{corelib}\" ";
                    }

                    LoadingStatus = "Executing ILC... Make sure your method is not inlined and is reachable as NativeAOT runs IL Link. It might take some time...";
                }
                else if (SettingsVm.IsNonCustomNativeAOTMode())
                {
                    LoadingStatus = "Compiling for NativeAOT (.NET 8.0+ is required) ...";

                    // For non-custom NativeAOT we need to use dotnet publish + with custom IlcArgs
                    // namely, we need to re-direct jit's output to a file (JitStdOutFile).

                    var tmpJitStdout = Path.GetTempFileName() + ".asm";

                    envVars["DOTNET_JitStdOutFile"] = tmpJitStdout;

                    string customIlcArgs = "";
                    foreach (var envVar in envVars)
                    {
                        var keyLower = envVar.Key.ToLowerInvariant();
                        if (keyLower?.StartsWith("dotnet_") == false &&
                            keyLower?.StartsWith("complus_") == false)
                        {
                            continue;
                        }

                        keyLower = keyLower
                            .Replace("dotnet_", "--codegenopt:")
                            .Replace("complus_", "--codegenopt:");
                        customIlcArgs += $"\t\t<IlcArg Include=\"{keyLower}=&quot;{envVar.Value}&quot;\" />\n";
                    }
                    envVars.Clear();

                    var tmpProps = Path.GetTempFileName() + ".props";
                    File.WriteAllText(tmpProps, $"""
                                                <?xml version="1.0" encoding="utf-8"?>
                                                <Project>
                                                    <PropertyGroup>
                                                        <DefineConstants>$(DefineConstants);DISASMO</DefineConstants>
                                                    </PropertyGroup>
                                                	<ItemGroup>
                                                {customIlcArgs}
                                                	</ItemGroup>
                                                </Project>
                                                """);

                    string tfmPart = SettingsVm.DontGuessTFM && string.IsNullOrWhiteSpace(SettingsVm.OverridenTFM) ? "" : $"-f {_currentTf}";

                    // NOTE: CustomBeforeDirectoryBuildProps is probably not a good idea to overwrite, but we need to pass IlcArgs somehow
                    string dotnetPublishArgs =
                        $"publish {tfmPart} -r win-{SettingsViewModel.Arch} -c Release" +
                        $" /p:PublishAot=true /p:CustomBeforeDirectoryBuildProps=\"{tmpProps}\"" +
                        $" /p:WarningLevel=0 /p:TreatWarningsAsErrors=false -v:q";

                    var publishResult = await ProcessUtils.RunProcess("dotnet", dotnetPublishArgs, null, Path.GetDirectoryName(_currentProjectPath), cancellationToken: UserCt);

                    ThrowIfCanceled();

                    if (string.IsNullOrEmpty(publishResult.Error))
                    {
                        if (!File.Exists(tmpJitStdout))
                        {
                            Output = $"""
                                      JitDisasm didn't produce any output :(. Make sure your method is not inlined by the code generator
                                      (it's a good idea to mark it as [MethodImpl(MethodImplOptions.NoInlining)]) and is reachable from Main() as
                                      NativeAOT may delete unused methods. Also, JitDisasm doesn't work well for Main() in NativeAOT mode."


                                      {publishResult.Output}
                                      """;
                        }
                        else
                        {
                            Success = true;
                            Output = File.ReadAllText(tmpJitStdout);

                            // Keep the temp files around for debugging if it failed.
                            // and delete them if it succeeded.
                            File.Delete(tmpProps);
                            File.Delete(tmpJitStdout);
                        }
                    }
                    else
                    {
                        Output = publishResult.Error;
                    }
                    return;
                }
                else
                {
                    LoadingStatus = $"Executing DisasmoLoader...";
                }


                if (!SettingsVm.UseDotnetPublishForReload && !SettingsVm.CrossgenIsSelected && !SettingsVm.NativeAotIsSelected && SettingsVm.UseCustomRuntime)
                {
                    var (clrCheckedFilesDir, success) = GetPathToCoreClrChecked();
                    if (!success)
                        return;
                    executable = Path.Combine(clrCheckedFilesDir, "CoreRun.exe");
                }

                if ((SettingsVm.RunAppMode) && 
                    !string.IsNullOrWhiteSpace(SettingsVm.OverridenJitDisasm))
                {
                    envVars["DOTNET_JitDisasm"] = SettingsVm.OverridenJitDisasm;
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

                    string fgLines = File.ReadAllText(currentFgFile);

                    FgPhases.Clear();
                    var graphs = fgLines.Split(new [] {"digraph FlowGraph {"}, StringSplitOptions.RemoveEmptyEntries);
                    int graphIndex = 0;

                    var fgBaseDir = Path.Combine(Path.GetTempPath(), "Disasmo", "Flowgraphs", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(fgBaseDir);
                    foreach (var graph in graphs)
                    {
                        try
                        {
                            var name = graph.Substring(graph.IndexOf("graph [label = ") + "graph [label = ".Length);
                            name = name.Substring(0, name.IndexOf("\"];"));
                            name = name.Replace("\\n", " ");
                            name = name.Substring(name.IndexOf(" after ") + " after ".Length).Trim();

                            // Reset counter if tier0 and tier1 are merged together
                            if (name == "Pre-import")
                            {
                                graphIndex = 0;
                            }

                            name = (++graphIndex) + ". " + name;

                            // Ignore invalid path chars
                            name = Path.GetInvalidFileNameChars().Aggregate(name, (current, ic) => current.Replace(ic, '_'));

                            var dotPath = Path.Combine(fgBaseDir, $"{name}.dot");
                            File.WriteAllText(dotPath, "digraph FlowGraph {\n" + graph);

                            FgPhases.Add(new FlowgraphItemViewModel(SettingsVm) { Name = name, DotFileUrl = dotPath, ImageUrl = "" });
                        }
                        catch (Exception exc)
                        {
                            Debug.WriteLine(exc);
                        }
                    }
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
            return DisassemblyPrettifier.Prettify(output, !SettingsVm.ShowAsmComments && !SettingsVm.RunAppMode);
        }

        private UnconfiguredProject GetUnconfiguredProject(Project project)
        {
            var context = project as IVsBrowseObjectContext;
            if (context == null && project != null) 
                context = project.Object as IVsBrowseObjectContext;

            return context?.UnconfiguredProject;
        }

        private (string, bool) GetPathToRuntimePack()
        {
            var arch = SettingsViewModel.Arch;
            var (_, success) = GetPathToCoreClrChecked();
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
                         $"Run 'build.cmd Clr+Clr.Aot+Libs -c Release -a {arch}' in the repo root\n" + 
                         "Don't worry, you won't have to re-build it every time you change something in jit, vm or corelib.";
                return (null, false);
            }
            return (runtimePackPath, true);
        }

        private (string, bool) GetPathToCoreClrChecked()
        {
            var arch = SettingsViewModel.Arch;
            var clrCheckedFilesDir = FindJitDirectory(SettingsVm.PathToLocalCoreClr, arch);
            if (string.IsNullOrWhiteSpace(clrCheckedFilesDir))
            {
                Output = $"Path to a local dotnet/runtime repository is either not set or it's not built for {arch} arch yet" +
                         (SettingsVm.CrossgenIsSelected ? "\n(When you use crossgen and target e.g. arm64 you need coreclr built for that arch)" : "") +
                         "\nPlease clone it and build it in `Checked` mode, e.g.:\n\n" +
                         "git clone git@github.com:dotnet/runtime.git\n" +
                         "cd runtime\n" +
                         $"build.cmd Clr+Clr.Aot+Libs -c Release -rc Checked -a {arch}\n\n";
                return (null, false);
            }
            return (clrCheckedFilesDir, true);
        }


        private (string, bool) GetPathToCoreClrCheckedForNativeAot()
        {
            var arch = SettingsViewModel.Arch;
            var releaseFolder = Path.Combine(SettingsVm.PathToLocalCoreClr, "artifacts", "bin", "coreclr", $"windows.{arch}.Checked");
            if (!Directory.Exists(releaseFolder) || !Directory.Exists(Path.Combine(releaseFolder, "aotsdk")) || !Directory.Exists(Path.Combine(releaseFolder, "ilc")))
            {
                Output = $"Path to a local dotnet/runtime repository is either not set or it's not correctly built for {arch} arch yet for NativeAOT" +
                         "\nPlease clone it and build it using the following steps.:\n\n" +
                         "git clone git@github.com:dotnet/runtime.git\n" +
                         "cd runtime\n" +
                         $"build.cmd Clr+Clr.Aot+Libs -c Release -rc Checked -a {arch}\n\n";
                return (null, false);
            }
            return (releaseFolder, true);
        }

        public async void RunOperationAsync(ISymbol symbol, CAProject project)
        {
            var stopwatch = Stopwatch.StartNew();
            DTE dte = IdeUtils.DTE();

            // It's possible that the last modified C# document is not active (e.g. Disasmo itself is in the focus)
            // so we have no choice but to run Save() for all the opened documents
            dte.SaveAllDocuments();

            try
            {
                IsLoading = true;
                FgPngPath = null;
                MainPageRequested?.Invoke();
                Success = false;
                _currentSymbol = symbol;
                _currentProject = project;
                Output = "";

                if (symbol == null)
                {
                    Output = "Symbol is not recognized, put cursor on a function/class name";
                    return;
                }    
                
                string clrCheckedFilesDir = null;
                if (SettingsVm.UseCustomRuntime)
                {
                    var (dir, success) = GetPathToCoreClrChecked();
                    if (!success)
                        return;
                    clrCheckedFilesDir = dir;
                }

                if (symbol is IMethodSymbol { IsGenericMethod: true } && !SettingsVm.RunAppMode)
                {
                    // TODO: ask user to specify type parameters
                    Output = "Generic methods are only supported in 'Run' mode";
                    return;
                }

                ThrowIfCanceled();

                // Find Release-{SettingsViewModel.Arch} configuration:
                Project currentProject = dte.GetActiveProject(project.FilePath);
                if (currentProject is null)
                {
                    Output = "There no active project. Please re-open solution.";
                    return;
                }

                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync();
                _currentProjectPath = currentProject.FileName;

                UnconfiguredProject unconfiguredProject = GetUnconfiguredProject(currentProject);
                // find all configurations, ordered by version descending
                var projectConfigurations = (await IdeUtils.GetProjectConfigurations(unconfiguredProject))
                    .OrderByDescending(IdeUtils.GetTargetFrameworkVersionDimension)
                    .ToList();
                // filter Release configurations
                var releaseConfigurations = projectConfigurations
                    .Where(cfg => string.Equals(IdeUtils.GetConfigurationDimension(cfg), "Release", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                // Use Release configurations only if we have any
                if (releaseConfigurations.Any())
                {
                    projectConfigurations = releaseConfigurations;
                }

                ProjectConfiguration projectConfiguration;
                if (string.IsNullOrWhiteSpace(SettingsVm.OverridenTFM))
                {
                    // choose first (highest)
                    projectConfiguration = projectConfigurations.FirstOrDefault();
                    // resolve later
                    _currentTf = null;
                }
                else
                {
                    // No validation in this case
                    _currentTf = SettingsVm.OverridenTFM.Trim();
                    var currentTfmVersion = TfmVersion.Parse(_currentTf);
                    // find the best suitable project configuration
                    projectConfiguration = projectConfigurations
                        .FirstOrDefault(cfg => currentTfmVersion != null && currentTfmVersion.CompareTo(IdeUtils.GetTargetFrameworkVersionDimension(cfg)) >= 0)
                        ?? projectConfigurations.FirstOrDefault();
                }

                IProjectProperties projectProperties = await IdeUtils.GetProjectProperties(unconfiguredProject, projectConfiguration);
                ThrowIfCanceled();

                // resolve target framework
                if (_currentTf == null)
                {
                    int? major;
                    if (projectProperties != null)
                    {
                        _currentTf = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFramework");
                        major = TfmVersion.Parse(_currentTf)?.Major;
                    }
                    else
                    {
                        // fallback to net 7.0
                        _currentTf = "net7.0";
                        major = 7;
                    }

                    ThrowIfCanceled();

                    if (major >= 6)
                    {
                        if (!SettingsVm.UseCustomRuntime && major < 7)
                        {
                            Output =
                                "Only net7.0 (and newer) apps are supported with non-locally built dotnet/runtime.\nMake sure <TargetFramework>net7.0</TargetFramework> is set in your csproj.";
                            return;
                        }
                    }
                    else
                    {
                        Output =
                            "Only net6.0 (and newer) apps are supported.\nMake sure <TargetFramework>net6.0</TargetFramework> is set in your csproj.";
                        return;
                    }
                }

                ThrowIfCanceled();

                if (SettingsVm.RunAppMode && SettingsVm.UseDotnetPublishForReload)
                {
                    // TODO: fix this
                    Output = "\"Run current app\" mode only works with \"dotnet build\" reload strategy, see Options tab.";
                    return;
                }

                // Validation for Flowgraph tab
                if (SettingsVm.FgEnable)
                {
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

                if (SettingsVm.CrossgenIsSelected || SettingsVm.NativeAotIsSelected)
                {
                    if (SettingsVm.UsePGO)
                    {
                        Output = "PGO has no effect on R2R'd/NativeAOT code.";
                        return;
                    }

                    if (SettingsVm.RunAppMode)
                    {
                        Output = "Run mode is not supported for crossgen/NativeAOT";
                        return;
                    }

                    if (SettingsVm.UseTieredJit)
                    {
                        Output = "TieredJIT has no effect on R2R'd/NativeAOT code.";
                        return;
                    }

                    if (SettingsVm.FgEnable)
                    {
                        Output = "Flowgraphs are not tested with crossgen2/NativeAOT yet (in Disasmo)";
                        return;
                    }
                }

                string outputDir = projectProperties == null ? "bin" : await projectProperties.GetEvaluatedPropertyValueAsync("OutputPath");
                DisasmoOutDir = Path.Combine(outputDir, DisasmoFolder + (SettingsVm.UseDotnetPublishForReload ? "_published" : ""));
                string currentProjectDirPath = Path.GetDirectoryName(_currentProjectPath);

                if (SettingsVm.IsNonCustomDotnetAotMode())
                {
                    ThrowIfCanceled();
                    var symbolInfo = SymbolUtils.FromSymbol(_currentSymbol);
                    await RunFinalExe(symbolInfo, projectProperties);
                    return;
                }

                string tfmPart = SettingsVm.DontGuessTFM && string.IsNullOrWhiteSpace(SettingsVm.OverridenTFM) ? "" : $"-f {_currentTf}";

                // Some things can't be set in CLI e.g. appending to DefineConstants
                var tmpProps = Path.GetTempFileName() + ".props";
                File.WriteAllText(tmpProps, $"""
                                             <?xml version="1.0" encoding="utf-8"?>
                                             <Project>
                                                 <PropertyGroup>
                                                     <DefineConstants>$(DefineConstants);DISASMO</DefineConstants>
                                                 </PropertyGroup>
                                             </Project>
                                             """);

                ProcessResult publishResult;
                if (SettingsVm.UseDotnetPublishForReload)
                {
                    LoadingStatus = $"dotnet publish -r win-{SettingsViewModel.Arch} -c Release -o ...";

                    string dotnetPublishArgs =
                        $"publish {tfmPart} -r win-{SettingsViewModel.Arch} -c Release -o {DisasmoOutDir} --self-contained true /p:PublishTrimmed=false /p:PublishSingleFile=false /p:CustomBeforeDirectoryBuildProps=\"{tmpProps}\" /p:WarningLevel=0 /p:TreatWarningsAsErrors=false -v:q";

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

                    LoadingStatus = "dotnet build -c Release -o ...";

                    string dotnetBuildArgs = $"build {tfmPart} -c Release -o {DisasmoOutDir} --no-self-contained " +
                                             "/p:RuntimeIdentifier=\"\" " +
                                             "/p:RuntimeIdentifiers=\"\" " +
                                             "/p:WarningLevel=0 " +
                                             $"/p:CustomBeforeDirectoryBuildProps=\"{tmpProps}\" " +
                                             $"/p:TreatWarningsAsErrors=false \"{_currentProjectPath}\"";

                    Dictionary<string, string> fasterBuildEnvVars = new Dictionary<string, string>
                    {
                        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
                        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
                    };

                    if (SettingsVm.UseNoRestoreFlag)
                    {
                        dotnetBuildArgs += " --no-restore --no-dependencies --nologo";
                        fasterBuildEnvVars["DOTNET_MULTILEVEL_LOOKUP"] = "0";
                    }

                    publishResult = await ProcessUtils.RunProcess("dotnet", dotnetBuildArgs, fasterBuildEnvVars,
                        currentProjectDirPath,
                        cancellationToken: UserCt);
                }

                File.Delete(tmpProps);
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
                            $"Something went wrong, {dstFolder} doesn't exist after 'dotnet publish -r win-{SettingsViewModel.Arch} -c Release' step";
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
                var finalSymbolInfo = SymbolUtils.FromSymbol(_currentSymbol);
                await RunFinalExe(finalSymbolInfo, projectProperties);
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