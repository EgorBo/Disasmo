using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Disasmo.Properties;
using Microsoft.VisualStudio.Shell;
using Project = EnvDTE.Project;

namespace Disasmo
{
    public class MainViewModel : ViewModelBase
    {
        private string _output;
        private string _loadingStatus;
        private string _pathToLocalCoreClr;
        private bool _isLoading;
        private ISymbol _currentMethodSymbol;
        private bool _success;

        public MainViewModel()
        {
            PathToLocalCoreClr = Settings.Default.PathToCoreCLR;
        }

        public string Output
        {
            get => _output;
            set => Set(ref _output, value);
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

        public string PathToLocalCoreClr
        {
            get => _pathToLocalCoreClr;
            set
            {
                Set(ref _pathToLocalCoreClr, value);
                Settings.Default.PathToCoreCLR = value;
                Settings.Default.Save();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public ICommand RefreshCommand => new RelayCommand(() => DisasmAsync(_currentMethodSymbol));

        public async void DisasmAsync(ISymbol symbol)
        {
            try
            {
                Success = false;
                IsLoading = true;
                _currentMethodSymbol = symbol;
                Output = "";

                if (symbol == null)
                    return;

                if (string.IsNullOrWhiteSpace(PathToLocalCoreClr))
                {
                    Output = "Path to a local CoreCLR is not set yet ^. (e.g. C:/prj/coreclr-master)\nPlease clone it and build it in both Release and Debug modes:\n\ncd coreclr-master\nbuild release skiptests\nbuild debug skiptests\n\nFor more details visit https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md#setting-up-our-environment";
                    return;
                }

                if (symbol is IMethodSymbol method && method.IsGenericMethod)
                {
                    // TODO: ask user to specify type parameters
                    Output = "Generic methods are not supported yet.";
                    return;
                }

                // Find Release-x64 configuration:
                var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
                Project currentProject = dte.GetActiveProject();

                var allReleaseCfgs = currentProject.ConfigurationManager.OfType<Configuration>().Where(c => c.ConfigurationName == "Release").ToList();
                var neededConfig = allReleaseCfgs.FirstOrDefault(c => c.PlatformName?.Contains("64") == true);
                if (neededConfig == null)
                {
                    neededConfig = allReleaseCfgs.FirstOrDefault(c => c.PlatformName?.Contains("Any") == true);
                    if (neededConfig == null)
                    {
                        Output = "Couldn't find any 'Release - x64' or 'Release - Any CPU' configuration.";
                        return;
                    }
                }

                string currentProjectOutputPath = neededConfig.GetPropertyValueSafe("OutputPath");
                string currentProjectPath = currentProject.FileName;

                // TODO: validate TargetFramework and OutputType
                // unfortunately both old VS API and new crashes for me on my vs2019preview2 (see https://github.com/dotnet/project-system/issues/669 and the workaround - both crash)
                // ugly hack for OutputType:
                if (!File.ReadAllText(currentProjectPath).ToLower().Contains("<outputtype>exe<"))
                {
                    Output = "At this moment only .NET Core Сonsole Applications (`<OutputType>Exe</OutputType>`) are supported.\nFeel free to contribute multi-project support :-)";
                    return;
                }

                // TODO: At this step we need to modify app's EntryPoint and add the following line:
                // global::System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(...);

                string projectPath = Path.GetDirectoryName(currentProjectPath);

                // first of all we need to restore packages if they are not restored
                // and do 'dotnet publish'
                // Basically, it follows https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md
                // TODO: incremental update

                LoadingStatus = "dotnet restore -r win-x64";
                var restoreResult = await ProcessUtils.RunProcess("dotnet", "restore -r win-x64", null, projectPath);
                if (!string.IsNullOrEmpty(restoreResult.Error))
                {
                    Output = restoreResult.Error;
                    return;
                }

                LoadingStatus = "dotnet publish -r win-x64 -c Release";
                var publishResult = await ProcessUtils.RunProcess("dotnet", "publish -r win-x64 -c Release", null, projectPath);
                if (!string.IsNullOrEmpty(publishResult.Error))
                {
                    Output = publishResult.Error;
                    return;
                }

                LoadingStatus = "Copying files from locally built CoreCLR";
                var dst = Path.Combine(projectPath, currentProjectOutputPath, @"win-x64\publish");
                if (!Directory.Exists(dst))
                {
                    Output = $"Something went wrong, {dst} doesn't exist after 'dotnet publish'";
                    return;
                }

                var clrReleaseFiles = Path.Combine(PathToLocalCoreClr, @"bin\Product\Windows_NT.x64.Release");

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

                var clrJitFile = Path.Combine(PathToLocalCoreClr, @"bin\Product\Windows_NT.x64.Debug\clrjit.dll");
                if (!File.Exists(clrJitFile))
                {
                    Output = $"File + {clrJitFile} does not exist. Please follow instructions at\n https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md";
                    return;
                }

                File.Copy(clrJitFile, Path.Combine(dst, "clrjit.dll"), true);

                // TODO: use VS API?
                var prjFile = Path.GetFileNameWithoutExtension(Directory.GetFiles(projectPath, "*.csproj").FirstOrDefault());

                // see https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md#specifying-method-names
                string target;

                if (symbol is IMethodSymbol)
                    target = symbol.ContainingType.Name + "::" + symbol.Name;
                else
                    target = symbol.Name + "::*";

                string finalExe = Path.Combine(projectPath, currentProjectOutputPath, $@"win-x64\publish\{prjFile}.exe");
                LoadingStatus = "Executing: " + finalExe;

                var result = await ProcessUtils.RunProcess(finalExe, "",
                    new Dictionary<string, string>
                    {
                        {"COMPlus_TieredCompilation", "0"}, // TODO: make optional and mark methods with AggressiveOptimization attribute
                        {"COMPlus_JitDiffableDasm", "1"},
                        {"COMPlus_JitDisasm", target},
                    });

                if (string.IsNullOrEmpty(result.Error))
                {
                    Success = true;
                    Output = result.Output;
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
    }
}