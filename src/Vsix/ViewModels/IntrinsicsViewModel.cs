using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Disasmo.Utils;
using GalaSoft.MvvmLight;

namespace Disasmo.ViewModels;

public class IntrinsicsViewModel : ViewModelBase
{
    private string _input;
    private List<IntrinsicsInfo> _suggestions;
    private List<IntrinsicsInfo> _intrinsics;
    private bool _isBusy;
    private bool _isDownloading;
    private string _loadingStatus;
        
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    private async void StartDownloadSources()
    {
        if (IsInDesignMode || _isDownloading || _intrinsics?.Any() == true)
            return;

        IsBusy = true;
        _isDownloading = true;
        try
        {
            _intrinsics = await IntrinsicsSourcesService.ParseIntrinsics(file =>
                {
                    LoadingStatus = "Loading data from Github...\nParsing " + file;
                });
            
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
            
        IsBusy = false;
        _isDownloading = false;
    }

    public string Input
    {
        get => _input;
        set
        {
            Set(ref _input, value);
            StartDownloadSources();
            if (_intrinsics == null || string.IsNullOrWhiteSpace(value) || value.Length < 3)
                Suggestions = null;
            else
                Suggestions = _intrinsics.Where(i => i.Contains(value)).Take(15).ToList();
        }
    }

    public string LoadingStatus
    {
        get => _loadingStatus;
        set => Set(ref _loadingStatus, value);
    }

    public List<IntrinsicsInfo> Suggestions
    {
        get => _suggestions;
        set => Set(ref _suggestions, value);
    }
}