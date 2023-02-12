using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;

namespace Disasmo;

public class FlowgraphItemViewModel : ViewModelBase
{
    private readonly SettingsViewModel _settingsView;
    private string _imageUrl;
    private string _dotFileUrl;
    private string _name;
    private bool _isBusy;

    public FlowgraphItemViewModel(SettingsViewModel settingsView)
    {
        _settingsView = settingsView;
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public bool IsInitialPhase => Name?.Contains("Pre-import") == true;

    public string DotFileUrl
    {
        get => _dotFileUrl;
        set => Set(ref _dotFileUrl, value);
    }

    public string ImageUrl
    {
        get => _imageUrl;
        set => Set(ref _imageUrl, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    public async Task LoadImageAsync(CancellationToken ct)
    {
        if (File.Exists(DotFileUrl + ".png"))
        {
            ImageUrl = DotFileUrl + ".png";
        }
        else
        {
            IsBusy = true;
            try
            {
                var img = DotFileUrl + ".png";
                string dotExeArgs = $"-Tpng -o\"{img}\" -Kdot \"{DotFileUrl}\"";
                await ProcessUtils.RunProcess(_settingsView.GraphvisDotPath, dotExeArgs, cancellationToken: ct);
                ImageUrl = img;
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            }
            IsBusy = false;
        }
    }
}