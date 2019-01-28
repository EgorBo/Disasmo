using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.CodeAnalysis;
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
        private ISymbol _currentMethodSymbol;

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
                if (e.PropertyName == "Success") ApplySyntaxHighlighting(MainViewModel.Success);
            };
        }

        private void ApplySyntaxHighlighting(bool asm)
        {
            if (asm)
            {
                using (Stream stream = typeof(DisasmWindowControl).Assembly.GetManifestResourceStream("Disasmo.AsmSyntax.xshd"))
                using (var reader = new XmlTextReader(stream))
                    OutputEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            else
            {
                var typeConverter = new HighlightingDefinitionTypeConverter();
                OutputEditor.SyntaxHighlighting = (IHighlightingDefinition)typeConverter.ConvertFrom("txt");
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
        {
            if (value is Boolean && (bool)value)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility && (Visibility)value == Visibility.Visible)
                return true;
            return false;
        }
    }
}