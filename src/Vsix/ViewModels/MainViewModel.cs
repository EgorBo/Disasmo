using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using Document = Microsoft.CodeAnalysis.Document;
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
        private string _customFuncName;
        private bool _isLoading;
        private bool _showCustomFuncInput;
        private ISymbol _currentSymbol;
        private Document _codeDocument;
        private bool _success;
        private bool _tieredJitEnabled;
        private string _currentProjectPath;

        private string DisasmoOutDir = "";

        public SettingsViewModel SettingsVm { get; } = new SettingsViewModel();
        public IntrinsicsViewModel IntrinsicsVm { get; } = new IntrinsicsViewModel();

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

        public ICommand RefreshCommand => new RelayCommand(() => RunOperationAsync(_currentSymbol, _codeDocument));

        public ICommand ShowCustomFuncInputCommand => new RelayCommand(() => ShowCustomFuncInput = true);

        public ICommand HideCustomFuncInputCommand => new RelayCommand(() => ShowCustomFuncInput = false);

        public ICommand RunForCustomFunCommand => new RelayCommand(() => { });

        public ICommand RunDiffWithPrevious => new RelayCommand(() => IdeUtils.RunDiffTools(PreviousOutput, Output));

        public async Task RunFinalExe()
        {
            try
            {
                Success = false;
                IsLoading = true;
                LoadingStatus = "Loading...";

                string dstFolder = DisasmoOutDir;
                if (!Path.IsPathRooted(dstFolder))
                    dstFolder = Path.Combine(Path.GetDirectoryName(_currentProjectPath), DisasmoOutDir);

                // TODO: respect AssemblyName property (if it doesn't match csproj name)
                string fileName = Path.GetFileNameWithoutExtension(_currentProjectPath);

                var envVars = new Dictionary<string, string>();

                string hostType;
                string target;
                if (_currentSymbol is IMethodSymbol)
                {
                    target = _currentSymbol.ContainingType.Name + "::" + _currentSymbol.Name;
                    hostType = _currentSymbol.ContainingType.ToString();
                }
                else
                {
                    target = _currentSymbol.Name + "::*";
                    hostType = _currentSymbol.ToString();
                }

                IdeUtils.SaveEmbeddedResourceTo("Disasmo.Loader.dll", dstFolder);
                IdeUtils.SaveEmbeddedResourceTo("Disasmo.Loader.deps.json", dstFolder);

                if (SettingsVm.JitDumpInsteadOfDisasm)
                    envVars["COMPlus_JitDump"] = target;
                else
                    envVars["COMPlus_JitDisasm"] = target;

                if (!string.IsNullOrWhiteSpace(SettingsVm.SelectedCustomJit) &&
                    SettingsVm.SelectedCustomJit != "clrjit.dll")
                {
                    envVars["COMPlus_AltJitName"] = SettingsVm.SelectedCustomJit;
                    envVars["COMPlus_AltJit"] = target;
                }
                SettingsVm.FillWithUserVars(envVars);

                // TODO: it'll fail if the project has a custom assembly name (AssemblyName)
                LoadingStatus = $"Executing: CoreRun.exe Disasmo.Loader.dll {fileName}.dll \"{hostType}\"";
                var result = await ProcessUtils.RunProcess(
                    Path.Combine(dstFolder, "CoreRun.exe"), $"\"Disasmo.Loader.dll\" \"{fileName}.dll\" \"{hostType}\"", envVars, dstFolder);
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
            return ComPlusDisassemblyPrettifier.Prettify(output, !SettingsVm.ShowAsmComments);
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

        public async void RunOperationAsync(ISymbol symbol, Document codeDoc)
        {
            DTE dte = IdeUtils.DTE();

            try
            {
                MainPageRequested?.Invoke();
                Success = false;
                IsLoading = true;
                _currentSymbol = symbol;
                _codeDocument = codeDoc;
                Output = "";

                if (symbol == null || codeDoc == null)
                    return;

                var clrCheckedFilesDir = Path.Combine(SettingsVm.PathToLocalCoreClr, @"artifacts\bin\coreclr\windows.x64.Checked");
                if (string.IsNullOrWhiteSpace(SettingsVm.PathToLocalCoreClr) ||
                    !Directory.Exists(clrCheckedFilesDir))
                {
                    Output = "Path to a local dotnet/runtime repository is not set yet ^. (e.g. C:/prj/runtime)\nPlease clone it and build it in `Checked` mode, e.g.:\n\n" +
                        "git clone git@github.com:dotnet/runtime.git\n" +
                        "cd runtime\n" +
                        "build.cmd Clr -c Checked\n\n" +
                        "See https://github.com/dotnet/runtime/blob/master/docs/workflow/requirements/windows-requirements.md";
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

                //await JoinableTaskFactory.MainThreadAwaitable();
                _currentProjectPath = currentProject.FileName;

                string targetFramework = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFramework");
                targetFramework = targetFramework.ToLowerInvariant().Trim();

                if (targetFramework.StartsWith("net") && 
                    float.TryParse(targetFramework.Remove(0, "net".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float netVer) && 
                    netVer >= 5)
                {
                    // the project either netcoreapp3.x or net5 (or newer)
                }
                else
                {
                    Output = "Only net5.0 apps are supported. Make sure TargetFramework is set in your csproj.";
                    return;
                }

                DisasmoOutDir = Path.Combine(await projectProperties.GetEvaluatedPropertyValueAsync("OutputPath"), "Disasmo");
                string currentProjectDirPath = Path.GetDirectoryName(_currentProjectPath);

                dte.SaveAllActiveDocuments();

                LoadingStatus = $"dotnet build -r win-x64 -f {targetFramework} -c Release -o ...";
                var publishResult = await ProcessUtils.RunProcess(GetDotnetCliPath(), $"build -r win-x64 -c Release -f {targetFramework} -o {DisasmoOutDir} /p:PublishReadyToRun=false /p:PublishTrimmed=false /p:PublishSingleFile=false", null, currentProjectDirPath);
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

                LoadingStatus = "Copying files from locally built CoreCLR";

                string dstFolder = DisasmoOutDir;
                if (!Path.IsPathRooted(dstFolder))
                    dstFolder = Path.Combine(currentProjectDirPath, DisasmoOutDir);
                if (!Directory.Exists(dstFolder))
                {
                    Output = $"Something went wrong, {dstFolder} doesn't exist after 'dotnet build -r win-x64 -c Release' step";
                    return;
                }

                var copyClrFilesResult = await ProcessUtils.RunProcess("robocopy", $"/e \"{clrCheckedFilesDir}\" \"{dstFolder}", null);
                if (!string.IsNullOrEmpty(copyClrFilesResult.Error))
                {
                    Output = copyClrFilesResult.Error;
                    return;
                }
                await RunFinalExe();
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
    }
}