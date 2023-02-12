using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Disasmo;

[Guid("97cd0cd6-1d77-4848-8b6e-dc82cdccc6d7")]
public class DisasmWindow : ToolWindowPane
{
    public MainViewModel ViewModel => (MainViewModel) ((DisasmWindowControl) Content).DataContext;

    public DisasmWindow() : base(null)
    {
        Caption = "Disasmo";
        Content = new DisasmWindowControl();
    }
}
