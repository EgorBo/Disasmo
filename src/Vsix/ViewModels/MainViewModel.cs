using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Document = Microsoft.CodeAnalysis.Document;
using Project = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;
using Disasmo.Utils;
using Disasmo.ViewModels;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Disasmo.Analyzers;

namespace Disasmo
{
    public class MainViewModel : ViewModelBase
    {
        private string _output;
        private string _previousOutput;
        private string _loadingStatus;
        private string _customFuncName;
        private bool _isLoading;
        private bool _showCustomFuncInput;
        private ISymbol _currentSymbol;
        private Document _codeDocument;
        private bool _success;
        private bool _tieredJitEnabled;
        private OperationType _operationType;
        private string _currentProjectPath;

        private string DisasmoOutDir = "";
        private const string DisasmoBeginMarker = "/*disasmo{*/";
        private const string DisasmoEndMarker = "/*}disasmo*/";

        public SettingsViewModel SettingsVm { get; } = new SettingsViewModel();

        public IntrinsicsViewModel IntrinsicsVm { get; } = new IntrinsicsViewModel();

        public RunOnLocalClrViewModel RunVm { get; }

        public MainViewModel()
        {
            RunVm = new RunOnLocalClrViewModel(SettingsVm);
        }

        public event Action MainPageRequested;

        public string Output
        {
            get => _output;
            set
            {
                if (!string.IsNullOrWhiteSpace(_output))
                    PreviousOutput = _output;
                Set(ref _output, value);
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

        public bool Success
        {
            get => _success;
            set => Set(ref _success, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public bool ShowCustomFuncInput
        {
            get => _showCustomFuncInput;
            set => Set(ref _showCustomFuncInput, value);
        }

        public string CustomFuncName
        {
            get => _customFuncName;
            set => Set(ref _customFuncName, value);
        }

        // tier0, see https://github.com/dotnet/coreclr/issues/22123#issuecomment-458661007
        public bool TieredJitEnabled
        {
            get => _tieredJitEnabled;
            set
            {
                Set(ref _tieredJitEnabled, value);
                if (Success) RunFinalExe();
            }
        }

        public ICommand RefreshCommand => new RelayCommand(() => RunOperationAsync(_currentSymbol, _codeDocument, _operationType));

        public ICommand ShowCustomFuncInputCommand => new RelayCommand(() => ShowCustomFuncInput = true);

        public ICommand HideCustomFuncInputCommand => new RelayCommand(() => ShowCustomFuncInput = false);

        public ICommand RunForCustomFunCommand => new RelayCommand(() => { });

        public ICommand RunDiffWithPrevious => new RelayCommand(() => IdeUtils.RunDiffTools(PreviousOutput, Output));

        private static async Task<(Location, bool)> GetEntryPointLocation(Document codeDoc, ISymbol currentSymbol)
        {
            try
            {
                Compilation compilation = await codeDoc.Project.GetCompilationAsync();
                IMethodSymbol entryPoint = compilation.GetEntryPoint(default(CancellationToken));
                if (entryPoint.Equals(currentSymbol))
                    return (null, true);
                Location location = entryPoint.Locations.FirstOrDefault();
                return (location, false);
            }
            catch
            {
                return (null, false);
            }
        }

        public async Task RunFinalExe()
        {
            try
            {
                Success = false;
                IsLoading = true;
                LoadingStatus = "Loading...";

                var exeName = $@"{Path.GetFileNameWithoutExtension(_currentProjectPath)}.exe";
                string finalExe = Path.Combine(Path.GetDirectoryName(_currentProjectPath), DisasmoOutDir, exeName);

                var envVars = new Dictionary<string, string>();
                envVars["COMPlus_TieredCompilation"] = TieredJitEnabled ? "1" : "0";
                SettingsVm.FillWithUserVars(envVars);

                string target = _currentSymbol.GetJitDisasmTarget();
                if (SettingsVm.JitDumpInsteadOfDisasm)
                    envVars["COMPlus_JitDump"] = target;
                else
                    envVars["COMPlus_JitDisasm"] = target;

                // TODO: it'll fail if the project has a custom assembly name (AssemblyName)
                LoadingStatus = "Executing: " + exeName;
                var result = await ProcessUtils.RunProcess(finalExe, "", envVars);
                if (string.IsNullOrEmpty(result.Error))
                {
                    Success = true;
                    Output = PreprocessOutput(result.Output);
                }
                else
                {
                    Output = result.Error;
                }
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
            return ComPlusDisassemblyPrettifier.Prettify(output, !SettingsVm.ShowPrologueEpilogue, !SettingsVm.ShowAsmComments);
        }

        private string GetDotnetCliPath()
        {
            return "dotnet"; // from PATH
        }

        private UnconfiguredProject GetUnconfiguredProject(EnvDTE.Project project)
        {
            var context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
                context = project.Object as IVsBrowseObjectContext;

            return context?.UnconfiguredProject;
        }

        public async void RunOperationAsync(ISymbol symbol, Document codeDoc, OperationType operationType)
        {
            string entryPointFilePath = "";
            DTE dte = IdeUtils.DTE();

            try
            {
                MainPageRequested?.Invoke();
                Success = false;
                IsLoading = true;
                _currentSymbol = symbol;
                _codeDocument = codeDoc;
                _operationType = operationType;
                Output = "";

                if (symbol == null || codeDoc == null)
                    return;

                if (string.IsNullOrWhiteSpace(SettingsVm.PathToLocalCoreClr) && operationType == OperationType.Disasm)
                {
                    Output = "Path to a local dotnet/runtime repository is not set yet ^. (e.g. C:/prj/runtime)\nPlease clone it and build it in `Checked` mode, e.g.:\n\n" +
                        "git clone git@github.com:dotnet/runtime.git\n" +
                        "cd runtime\\src\\coreclr\n" +
                        "build.cmd -checked";
                    return;
                }

                if (symbol is IMethodSymbol method && method.IsGenericMethod)
                {
                    // TODO: ask user to specify type parameters
                    Output = "Generic methods are not supported yet.";
                    return;
                }

                // Find Release-x64 configuration:
                Project currentProject = dte.GetActiveProject();
                UnconfiguredProject unconfiguredProject = GetUnconfiguredProject(currentProject);

                // it will throw "Release config was not found" to the Output if there is no such config in the project
                ProjectConfiguration releaseConfig = await unconfiguredProject.Services.ProjectConfigurationsService.GetProjectConfigurationAsync("Release");
                ConfiguredProject configuredProject = await unconfiguredProject.LoadConfiguredProjectAsync(releaseConfig);
                IProjectProperties projectProperties = configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();

                _currentProjectPath = currentProject.FileName;

                if (await projectProperties.GetEvaluatedPropertyValueAsync("OutputType") != "Exe")
                {
                    Output = "At this moment only .NET Core Сonsole Applications (`<OutputType>Exe</OutputType>`) are supported.\nFeel free to contribute multi-project support :-)";
                    return;
                }

                string targetFramework = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFramework");

                // ugly temp workaround:
                if (!targetFramework.StartsWith("netcoreapp") || float.Parse(targetFramework.Remove(0, 10), CultureInfo.InvariantCulture) < 3)
                {
                    Output = "Only netcoreapp3.0 (and newer) Console Applications are supported.";
                    return;
                }

                DisasmoOutDir = Path.Combine(await projectProperties.GetEvaluatedPropertyValueAsync("OutputPath"), "Disasmo");
                string currentProjectDirPath = Path.GetDirectoryName(_currentProjectPath);

                if (operationType == OperationType.ObjectLayout)
                {
                    LoadingStatus = "dotnet add package ObjectLayoutInspector -v 0.1.1";
                    var restoreResult = await ProcessUtils.RunProcess(GetDotnetCliPath(), "add package ObjectLayoutInspector -v 0.1.1", null, currentProjectDirPath);
                    if (!string.IsNullOrEmpty(restoreResult.Error))
                    {
                        Output = restoreResult.Error;
                        return;
                    }
                }

                // first of all we need to restore packages if they are not restored
                // and do 'dotnet publish'
                var (location, isMain) = await GetEntryPointLocation(_codeDocument, symbol);

                if (isMain)
                {
                    Output = "Disasm for Main() is not supported (we are going to inject RuntimeHelpers.PrepareMethod there)";
                    return;
                }

                entryPointFilePath = location?.SourceTree?.FilePath;
                if (string.IsNullOrEmpty(entryPointFilePath))
                {
                    Output = "Can't find Main() method in the project. (in order to inject 'RuntimeHelpers.PrepareMethod')";
                    return;
                }

                dte.SaveAllActiveDocuments();

                bool skipDotnetRestore = SettingsVm.SkipDotnetRestoreStep;
                if (skipDotnetRestore && !Directory.Exists(Path.Combine(currentProjectDirPath, DisasmoOutDir)))
                    skipDotnetRestore = false;

                if (!skipDotnetRestore)
                {
                    LoadingStatus = "dotnet restore -r win-x64\nSometimes it takes a while (up to few minutes)...";
                    var restoreResult = await ProcessUtils.RunProcess(GetDotnetCliPath(), "restore -r win-x64", null, currentProjectDirPath);
                    if (!string.IsNullOrEmpty(restoreResult.Error))
                    {
                        Output = restoreResult.Error;
                        return;
                    }
                }
                InjectCodeToMain(entryPointFilePath, location.SourceSpan.Start, symbol, false, operationType);

                LoadingStatus = $"dotnet publish -r win-x64 -f {targetFramework} -c Release - o ...";
                var publishResult = await ProcessUtils.RunProcess(GetDotnetCliPath(), $"publish -r win-x64 -c Release -f {targetFramework} -o {DisasmoOutDir}", null, currentProjectDirPath);
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

                if (operationType == OperationType.Disasm)
                {
                    LoadingStatus = "Copying files from locally built CoreCLR";
                    var dst = Path.Combine(currentProjectDirPath, DisasmoOutDir);
                    if (!Directory.Exists(dst))
                    {
                        Output = $"Something went wrong, {dst} doesn't exist after 'dotnet publish'";
                        return;
                    }

                    var clrReleaseFiles = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\coreclr\Windows_NT.x64.Release");


                    if (SettingsVm.PreferCheckedBuild || !Directory.Exists(clrReleaseFiles))
                        clrReleaseFiles = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\coreclr\Windows_NT.x64.Checked");

                    if (!Directory.Exists(clrReleaseFiles))
                    {
                        Output = $"Folder + {clrReleaseFiles} does not exist. Make sure you did:\n\n" +
                                 "git clone git@github.com:dotnet/runtime.git\n" +
                                 "cd runtime\\src\\coreclr\n" +
                                 "build.cmd -checked -skiptests"; ;
                        return;
                    }

                    var copyClrReleaseResult = await ProcessUtils.RunProcess("robocopy", $"/e \"{clrReleaseFiles}\" \"{dst}", null);
                    if (!string.IsNullOrEmpty(copyClrReleaseResult.Error))
                    {
                        Output = copyClrReleaseResult.Error;
                        return;
                    }

                    var clrJitFile = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\coreclr\Windows_NT.x64.Debug\clrjit.dll");

                    if (SettingsVm.PreferCheckedBuild || !File.Exists(clrJitFile))
                        clrJitFile = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\coreclr\Windows_NT.x64.Checked\clrjit.dll");

                    if (!File.Exists(clrJitFile))
                    {
                        Output = $"File + {clrJitFile} does not exist. Please follow instructions at\n https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md";
                        return;
                    }

                    File.Copy(clrJitFile, Path.Combine(dst, "clrjit.dll"), true);
                }
                await RunFinalExe();
            }
            catch (Exception e)
            {
                Output = e.ToString();
            }
            finally
            {
                RemoveInjectedCodeFromMain(entryPointFilePath);
                IsLoading = false;
            }
        }

        private static void InjectCodeToMain(string mainPath, int mainStartIndex, ISymbol symbol, bool waitForAttach, OperationType operationType)
        {
            // Did you expect to see some Roslyn magic here? :)
            // TODO: replace with Mono.Cecil

            string code = File.ReadAllText(mainPath);
            int indexOfMain = code.IndexOf('{', mainStartIndex) + 1;

            string disasmTemplate =
                DisasmoBeginMarker +
                "System.Linq.Enumerable.ToList(" +
                "System.Linq.Enumerable.Where(" +
                    "typeof(%typename%).GetMethods((System.Reflection.BindingFlags)60), " +
                    "w => w.DeclaringType == typeof(%typename%) && !w.IsGenericMethod))" +
                    ".ForEach(m => System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle));" +
                "System.Console.WriteLine(\" \");" +
                (waitForAttach ? "System.Console.ReadLine();" : "") +
                "System.Environment.Exit(0);" +
                DisasmoEndMarker;

            string objectLayoutTemplate =
                DisasmoBeginMarker +
                "ObjectLayoutInspector.TypeLayout.PrintLayout<%typename%>(recursively: true);" +
                "System.Environment.Exit(0);" +
                DisasmoEndMarker;

            string hostType = symbol.GetContainingTypeNameOrSelf();

            if (operationType == OperationType.ObjectLayout)
                code = code.Insert(indexOfMain, objectLayoutTemplate);
            else if (operationType == OperationType.Disasm)
                code = code.Insert(indexOfMain, disasmTemplate);

            code = code.Replace("%typename%", hostType);

            File.WriteAllText(mainPath, code);
        }

        private void RemoveInjectedCodeFromMain(string mainPath)
        {
            try
            {
                if (string.IsNullOrEmpty(mainPath))
                    return;

                bool changed = false;
                var source = File.ReadAllText(mainPath);
                while (true)
                {
                    if (source.Contains(DisasmoBeginMarker))
                    {
                        var indexBegin = source.IndexOf(DisasmoBeginMarker, StringComparison.InvariantCulture);
                        var indexEnd = source.IndexOf(DisasmoEndMarker, indexBegin, StringComparison.InvariantCulture);
                        if (indexBegin >= 0 && indexEnd > indexBegin)
                        {
                            source = source.Remove(indexBegin, indexEnd - indexBegin + DisasmoEndMarker.Length);
                            changed = true;
                        }
                        else break;
                    }
                    else break;
                }

                if (changed)
                {
                    File.WriteAllText(mainPath, source);
                    DTE dte = IdeUtils.DTE();
                }

            }
            catch (Exception e)
            {
                Output = e.ToString();
            }
        }
    }

    public enum OperationType
    {
        Disasm,
        ObjectLayout
    }
}