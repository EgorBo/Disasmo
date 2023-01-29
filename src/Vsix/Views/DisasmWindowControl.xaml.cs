using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Navigation;
using System.Xml;
using Disasmo.Utils;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.Shell;

namespace Disasmo
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for DisasmWindowControl.
    /// </summary>
    public partial class DisasmWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DisasmWindowControl"/> class.
        /// </summary>
        public DisasmWindowControl()
        {
            this.InitializeComponent();
            MainViewModel.PropertyChanged += (s, e) =>
            {
                // AvalonEdit is not bindable (lazy workaround)
                if (e.PropertyName == "Output") OutputEditor.Text = MainViewModel.Output;
                if (e.PropertyName == "PreviousOutput") OutputEditorPrev.Text = MainViewModel.PreviousOutput;
                if (e.PropertyName == "Success") ApplySyntaxHighlighting(MainViewModel.Success && !MainViewModel.SettingsVm.JitDumpInsteadOfDisasm);
            };
            MainViewModel.MainPageRequested += () =>
            {
                if (TabControl.SelectedIndex != 2) // ugly fix: don't leave "flowgraph" tab on reload
                    TabControl.SelectedIndex = 0;
            };
        }

        private void ApplySyntaxHighlighting(bool asm)
        {
            if (asm)
            {
                using (Stream stream = typeof(DisasmWindowControl).Assembly.GetManifestResourceStream("Disasmo.Resources.AsmSyntax.xshd"))
                using (var reader = new XmlTextReader(stream))
                {
                    var sh = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    OutputEditor.SyntaxHighlighting = sh;
                    OutputEditorPrev.SyntaxHighlighting = sh;
                }
            }
            else
            {
                var sh = (IHighlightingDefinition)new HighlightingDefinitionTypeConverter().ConvertFrom("txt");
                OutputEditor.SyntaxHighlighting = sh;
                OutputEditorPrev.SyntaxHighlighting = sh;
            }
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void OnOpenLogs(object s, RequestNavigateEventArgs e)
        {
            try
            {
                IdeUtils.DTE().ItemOperations.OpenFile(UserLogger.LogFile);
            }
            catch { }
        }

        private void OnOpenReleaseNotes(object s, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(e.Uri.ToString());
            }
            catch { }
        }

        private void OnClearLogs(object s, RequestNavigateEventArgs e)
        {
            try
            {
                File.WriteAllText(UserLogger.LogFile, "");
            }
            catch { }
        }

        private void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabControl.SelectedIndex == 3) MainViewModel.IntrinsicsVm.DownloadSources();
        }

        private async void OnOpenFolderWithFlowGraphs(object sender, RequestNavigateEventArgs e)
        {
            var file = e.Uri?.ToString() ?? "";
            file = file.Replace("file:///", "");
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    await ProcessUtils.RunProcess("explorer.exe", Path.GetDirectoryName(file));
                }
                catch
                {
                }
            }
        }

        private void OnAddGenericArgument(object sender, RoutedEventArgs e)
        {
            MainViewModel.SettingsVm.GenericArguments.Add(new GenericArgument("*=System.Int32"));
        }

        private void OnRemoveGenericArgument(object sender, RoutedEventArgs e)
        {
            foreach (var item in GenericArgumentGrid.SelectedItems.Cast<GenericArgument>().ToArray())
            {
                MainViewModel.SettingsVm.GenericArguments.Remove(item);
            }
        }

        private void OnMoveUpGenericArgument(object sender, RoutedEventArgs e)
        {
            foreach (int index in GenericArgumentGrid.SelectedItems.Cast<GenericArgument>().Select(MainViewModel.SettingsVm.GenericArguments.IndexOf).OrderBy(v => v))
            {
                if (index > 0)
                {
                    MainViewModel.SettingsVm.GenericArguments.Move(index, index - 1);
                }
            }
        }

        private void OnMoveDownGenericArgument(object sender, RoutedEventArgs e)
        {
            foreach (int index in GenericArgumentGrid.SelectedItems.Cast<GenericArgument>().Select(MainViewModel.SettingsVm.GenericArguments.IndexOf).OrderByDescending(v => v))
            {
                if (index >= 0 && index < MainViewModel.SettingsVm.GenericArguments.Count - 1)
                {
                    MainViewModel.SettingsVm.GenericArguments.Move(index, index + 1);
                }
            }
        }

        private void OnCopyGenericArgument(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(string.Join("\n", MainViewModel.SettingsVm.GenericArguments));
        }

        private void OnPasteGenericArgument(object sender, RoutedEventArgs e)
        {
            string text = Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                MainViewModel.SettingsVm.GenericArguments = new ObservableCollection<GenericArgument>(
                    text.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => new GenericArgument(s))
                );
            }
        }
    }

    [Guid("97cd0cd6-1d77-4848-8b6e-dc82cdccc6d7")]
    public class DisasmWindow : ToolWindowPane
    {
        public MainViewModel ViewModel => (MainViewModel) ((DisasmWindowControl) Content).DataContext;

        public DisasmWindow() : base(null)
        {
            this.Caption = "Disasmo";
            this.Content = new DisasmWindowControl();
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) 
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) 
            => value is Visibility visibility && visibility == Visibility.Visible;
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => !(bool)value;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}