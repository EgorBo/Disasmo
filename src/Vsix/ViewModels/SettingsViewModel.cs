using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using Disasmo.Properties;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace Disasmo
{
    public class SettingsViewModel : ViewModelBase
    {
        private string _pathToLocalCoreClr;
        private bool _jitDumpInsteadOfDisasm;
        private string _customEnvVars;
        private bool _showAsmComments;
        private bool _updateIsAvailable;
        private Version _currentVersion;
        private Version _availableVersion;
        private bool _allowDisasmInvocations;
        private bool _useDotnetPublishForReload;
        private bool _useDotnetBuildForReload;
        private bool _runAppMode;
        private bool _printInlinees;
        private bool _presenterMode;
        private bool _useNoRestoreFlag;
        private bool _useTieredJit;
        private bool _useCustomRuntime;
        private ObservableCollection<string> _customJits;
        private string _selectedCustomJit;
        private string _graphvisDot;
        private bool _fgEnable;
        private string _fgPhase;

        public SettingsViewModel()
        {
            PathToLocalCoreClr = Settings.Default.PathToCoreCLR_V7;
            ShowAsmComments = Settings.Default.ShowAsmComments_V7;
            CustomEnvVars = Settings.Default.CustomEnvVars3_V9.Replace(";;", Environment.NewLine);
            JitDumpInsteadOfDisasm = Settings.Default.JitDumpInsteadOfDisasm_V7;
            AllowDisasmInvocations = Settings.Default.AllowDisasmInvocations_V7;
            UseDotnetBuildForReload = Settings.Default.UseDotnetBuildForReload_V7;
            RunAppMode = Settings.Default.RunAppMode_V7;
            UseNoRestoreFlag = Settings.Default.UseNoRestoreFlag_V7;
            PresenterMode = Settings.Default.PresenterMode;
            UpdateIsAvailable = false;
            UseTieredJit = Settings.Default.UseTieredJit_V2;
            UseCustomRuntime = Settings.Default.UseCustomRuntime_V2;
            GraphvisDotPath = Settings.Default.GraphvisDotPath;
            FgPhase = Settings.Default.FgPhase;
            FgEnable = Settings.Default.FgEnable;
            PrintInlinees = Settings.Default.PrintInlinees;
            CheckUpdates();
        }

        private async void CheckUpdates()
        {
            CurrentVersion = DisasmoPackage.Current?.GetCurrentVersion();
            AvailableVersion = await DisasmoPackage.GetLatestVersionOnline();
            if (CurrentVersion != null && AvailableVersion > CurrentVersion)
                UpdateIsAvailable = true;
        }

        public bool FgEnable
        {
            get => _fgEnable;
            set
            {
                Set(ref _fgEnable, value);
                Settings.Default.FgEnable = value;
                Settings.Default.Save();
                if (value)
                {
                    JitDumpInsteadOfDisasm = true;
                }
            }
        }

        public string FgPhase
        {
            get => _fgPhase;
            set
            {
                Set(ref _fgPhase, value);
                Settings.Default.FgPhase = value;
                Settings.Default.Save();
            }
        }

        public string GraphvisDotPath
        {
            get => _graphvisDot;
            set
            {
                Set(ref _graphvisDot, value);
                Settings.Default.GraphvisDotPath = value;
                Settings.Default.Save();
            }
        }

        public string PathToLocalCoreClr
        {
            get => _pathToLocalCoreClr;
            set
            {
                Set(ref _pathToLocalCoreClr, value);
                Settings.Default.PathToCoreCLR_V7 = value;
                Settings.Default.Save();

                if (!string.IsNullOrWhiteSpace(_pathToLocalCoreClr))
                {
                    string jitDir = FindJitDirectory(_pathToLocalCoreClr);
                    if (jitDir != null)
                    {
                        string[] jits = Directory.GetFiles(jitDir, "clrjit*.dll");
                        CustomJits = new ObservableCollection<string>(jits.Select(Path.GetFileName));
                        SelectedCustomJit = CustomJits.FirstOrDefault(j => j == "clrjit.dll");
                        return;
                    }
                }

                SelectedCustomJit = null;
                CustomJits?.Clear();
            }
        }

        public ObservableCollection<string> CustomJits
        {
            get => _customJits;
            set => Set(ref _customJits, value);
        }

        public string SelectedCustomJit
        {
            get => _selectedCustomJit;
            set => Set(ref _selectedCustomJit, value);
        }

        public bool RunAppMode
        {
            get => _runAppMode;
            set
            {
                Set(ref _runAppMode, value);
                Settings.Default.RunAppMode_V7 = value;
                Settings.Default.Save();
            }
        }

        public bool PrintInlinees
        {
            get => _printInlinees;
            set
            {
                Set(ref _printInlinees, value);
                if (value)
                {
                    // Reset "Use JitDump" flag which should also reset FlowGraph flag
                    JitDumpInsteadOfDisasm = false;
                }

                Settings.Default.PrintInlinees = value;
                Settings.Default.Save();
            }
        }

        public bool UseNoRestoreFlag
        {
            get => _useNoRestoreFlag;
            set
            {
                Set(ref _useNoRestoreFlag, value);
                Settings.Default.UseNoRestoreFlag_V7 = value;
                Settings.Default.Save();
            }
        }

        public bool PresenterMode
        {
            get => _presenterMode;
            set
            {
                Set(ref _presenterMode, value);
                Settings.Default.PresenterMode = value;
                Settings.Default.Save();
            }
        }

        public bool UseCustomRuntime
        {
            get => _useCustomRuntime;
            set
            {
                Set(ref _useCustomRuntime, value);
                Settings.Default.UseCustomRuntime_V2 = value;
                Settings.Default.Save();
                if (!value)
                {
                    UseDotnetPublishForReload = true;
                }
            }
        }

        public bool UseDotnetPublishForReload
        {
            get => _useDotnetPublishForReload;
            set
            {
                Set(ref _useDotnetPublishForReload, value);
                Set(ref _useDotnetBuildForReload, !value);
                Settings.Default.UseDotnetBuildForReload_V7 = !value;
                Settings.Default.Save();
            }
        }

        public bool UseDotnetBuildForReload
        {
            get => _useDotnetBuildForReload;
            set
            {
                Set(ref _useDotnetBuildForReload, value);
                Set(ref _useDotnetPublishForReload, !value);
                Settings.Default.UseDotnetBuildForReload_V7 = value;
                Settings.Default.Save();
            }
        }

        public bool JitDumpInsteadOfDisasm
        {
            get => _jitDumpInsteadOfDisasm;
            set
            {
                Set(ref _jitDumpInsteadOfDisasm, value);
                Settings.Default.JitDumpInsteadOfDisasm_V7 = value;
                Settings.Default.Save();
                if (!value)
                {
                    FgEnable = false;
                }
                else
                {
                    PrintInlinees = false;
                }
            }
        }

        public bool UseTieredJit
        {
            get => _useTieredJit;
            set
            {
                Set(ref _useTieredJit, value);
                Settings.Default.UseTieredJit_V2 = value;
                Settings.Default.Save();
            }
        }

        public bool ShowAsmComments
        {
            get => _showAsmComments;
            set
            {
                Set(ref _showAsmComments, value);
                Settings.Default.ShowAsmComments_V7 = value;
                Settings.Default.Save();
            }
        }

        public string CustomEnvVars
        {
            get => _customEnvVars;
            set
            {
                Set(ref _customEnvVars, value);
                Settings.Default.CustomEnvVars3_V9 = value;
                Settings.Default.Save();
            }
        }

        public bool UpdateIsAvailable
        {
            get => _updateIsAvailable;
            set { Set(ref _updateIsAvailable, value); }
        }

        public Version CurrentVersion
        {
            get => _currentVersion;
            set => Set(ref _currentVersion, value);
        }

        public Version AvailableVersion
        {
            get => _availableVersion;
            set => Set(ref _availableVersion, value);
        }

        public ICommand BrowseCommand => new RelayCommand(() =>
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
                PathToLocalCoreClr = dialog.SelectedPath;
        });

        public bool AllowDisasmInvocations
        {
            get => _allowDisasmInvocations;
            set
            {
                Set(ref _allowDisasmInvocations, value);
                Settings.Default.AllowDisasmInvocations_V7 = value;
                Settings.Default.Save();
            }
        }

        public void FillWithUserVars(Dictionary<string, string> dictionary)
        {
            if (string.IsNullOrWhiteSpace(CustomEnvVars))
                return;

            var pairs = CustomEnvVars.Split(new [] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                    dictionary[parts[0].Trim()] = parts[1].Trim();
            }
        }

        private static string FindJitDirectory(string basePath)
        {
            string jitDir = Path.Combine(basePath, @"artifacts\bin\coreclr\windows.x64.Checked");
            if (Directory.Exists(jitDir))
            {
                return jitDir;
            }

            jitDir = Path.Combine(basePath, @"artifacts\bin\coreclr\windows.x64.Debug");
            if (Directory.Exists(jitDir))
            {
                return jitDir;
            }

            return null;
        }
    }
}
