using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using Disasmo.Properties;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.VisualStudio.Text.Editor;

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
        private ObservableCollection<string> _customJits;
        private string _selectedCustomJit;

        public SettingsViewModel()
        {
            PathToLocalCoreClr = Settings.Default.PathToCoreCLR_V4;
            ShowAsmComments = Settings.Default.ShowAsmComments_V4;
            CustomEnvVars = Settings.Default.CustomEnvVars3_V4.Replace(";", Environment.NewLine);
            JitDumpInsteadOfDisasm = Settings.Default.JitDumpInsteadOfDisasm_V4;
            AllowDisasmInvocations = Settings.Default.AllowDisasmInvocations_V4;
            UseDotnetBuildForReload = Settings.Default.UseDotnetBuildForReload_V4;
            RunAppMode = Settings.Default.RunAppMode_V4;
            UpdateIsAvailable = false;
            CheckUpdates();
        }

        private async void CheckUpdates()
        {
            CurrentVersion = DisasmoPackage.Current?.GetCurrentVersion();
            AvailableVersion = await DisasmoPackage.GetLatestVersionOnline();
            if (CurrentVersion != null && AvailableVersion > CurrentVersion)
                UpdateIsAvailable = true;
        }

        public string PathToLocalCoreClr
        {
            get => _pathToLocalCoreClr;
            set
            {
                Set(ref _pathToLocalCoreClr, value);
                Settings.Default.PathToCoreCLR_V4 = value;
                Settings.Default.Save();

                if (!string.IsNullOrWhiteSpace(_pathToLocalCoreClr))
                {
                    string jitDir = Path.Combine(_pathToLocalCoreClr, @"artifacts\bin\coreclr\windows.x64.Checked");
                    if (Directory.Exists(jitDir))
                    {
                        var jits = Directory.GetFiles(jitDir, "clrjit*.dll");
                        CustomJits = new ObservableCollection<string>(jits.Select(j => Path.GetFileName(j)));
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

        public event Action<string> CurrentJitIsChanged;

        public string SelectedCustomJit
        {
            get => _selectedCustomJit;
            set
            {
                Set(ref _selectedCustomJit, value);
                CurrentJitIsChanged?.Invoke(_selectedCustomJit);
            }
        }

        public bool RunAppMode
        {
            get => _runAppMode;
            set
            {
                Set(ref _runAppMode, value);
                Settings.Default.RunAppMode_V4 = value;
                Settings.Default.Save();
            }
        }

        public bool UseDotnetPublishForReload
        {
            get => _useDotnetPublishForReload;
            set
            {
                Set(ref _useDotnetPublishForReload, value);
                Set(ref _useDotnetBuildForReload, !value);
                Settings.Default.UseDotnetBuildForReload_V4 = !value;
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
                Settings.Default.UseDotnetBuildForReload_V4 = value;
                Settings.Default.Save();
            }
        }

        public bool JitDumpInsteadOfDisasm
        {
            get => _jitDumpInsteadOfDisasm;
            set
            {
                Set(ref _jitDumpInsteadOfDisasm, value);
                Settings.Default.JitDumpInsteadOfDisasm_V4 = value;
                Settings.Default.Save();
            }
        }

        public bool ShowAsmComments
        {
            get => _showAsmComments;
            set
            {
                Set(ref _showAsmComments, value);
                Settings.Default.ShowAsmComments_V4 = value;
                Settings.Default.Save();
            }
        }

        public string CustomEnvVars
        {
            get => _customEnvVars;
            set
            {
                Set(ref _customEnvVars, value);
                Settings.Default.CustomEnvVars3_V4 = value;
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
                Settings.Default.AllowDisasmInvocations_V4 = value;
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
    }
}
