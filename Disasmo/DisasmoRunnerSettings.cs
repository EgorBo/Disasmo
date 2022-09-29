using System;
using System.Collections.Generic;

namespace Disasmo;

public class DisasmoRunnerSettings
{
    public string PathToLocalCoreClr { get; }
    public bool ShowAsmComments { get; set; }
    public string CustomEnvVars { get; set; } = "";
    public string Crossgen2Args { get; set; } = "";
    public string IlcArgs { get; set; } = "";
    public bool JitDumpInsteadOfDisasm { get; set; }
    public bool UseDotnetBuildForReload { get; set; }
    public bool UseDotnetPublishForReload { get; set; }
    public bool RunAppMode { get; set; }
    public bool UseNoRestoreFlag { get; set; }
    public bool PresenterMode { get; set; }
    public bool UseTieredJit { get; set; }
    public bool UseCustomRuntime { get; set; }
    public string GraphvisDotPath { get; set; } = "";
    public string FgPhase { get; set; } = "";
    public bool FgEnable { get; set; }
    public bool PrintInlinees { get; set; }
    public bool UsePGO { get; set; }
    public bool UseUnloadableContext { get; set; }
    public string CustomJitName { get; set; }

    public DisasmoRunnerSettings(string pathToLocalCoreClr)
    {
        PathToLocalCoreClr = pathToLocalCoreClr;
    }

    public bool IsNonCustomDotnetAotMode()
    {
        return !UseCustomRuntime &&
               CustomJitName is Constants.Crossgen or Constants.Ilc;
    }

    public bool IsCrossgenMode() => CustomJitName?.StartsWith("crossgen") == true;

    public bool IsNativeAotMode() => CustomJitName?.StartsWith("ilc") == true;

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