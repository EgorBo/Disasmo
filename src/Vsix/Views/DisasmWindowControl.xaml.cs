using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Navigation;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Build.Utilities;

namespace Disasmo;

/// <summary>
/// Interaction logic for DisasmWindowControl.
/// </summary>
public partial class DisasmWindowControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisasmWindowControl"/> class.
    /// </summary>
    public DisasmWindowControl()
    {
        InitializeComponent();
        MainViewModel.PropertyChanged += (_, e) =>
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
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
    }

    private void OnOpenReleaseNotes(object s, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(e.Uri.ToString());
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
    }

    private void OnClearLogs(object s, RequestNavigateEventArgs e)
    {
        try
        {
            File.WriteAllText(UserLogger.LogFile, "");
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
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
            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            }
        }
    }

	private void AvalonEdit_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
	{
        if ((Control.ModifierKeys & Keys.Control) == Keys.Control) {
            double fontSize = this.MainViewModel.SettingsVm.FontSize;
            fontSize += e.Delta * SystemInformation.MouseWheelScrollLines / 120;

            if (fontSize < 8)
                fontSize = 8;
            else if (fontSize > 50)
                fontSize = 50;
            
            this.MainViewModel.SettingsVm.FontSize = fontSize;

            e.Handled = true;
        }
    }
}
