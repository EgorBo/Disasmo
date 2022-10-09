namespace Disasmo.ViewModels
{
    public static class SettingsViewModelExtensions
    {
        public static DisasmoRunnerSettings ToDisasmoRunnerSettings(this SettingsViewModel settingsViewModel)
        {
            return new DisasmoRunnerSettings(settingsViewModel.PathToLocalCoreClr)
            {
                ShowAsmComments = settingsViewModel.ShowAsmComments,
                CustomEnvVars = settingsViewModel.CustomEnvVars,
                Crossgen2Args = settingsViewModel.Crossgen2Args,
                IlcArgs = settingsViewModel.IlcArgs,
                JitDumpInsteadOfDisasm = settingsViewModel.JitDumpInsteadOfDisasm,
                UseDotnetBuildForReload = settingsViewModel.UseDotnetBuildForReload,
                UseDotnetPublishForReload = settingsViewModel.UseDotnetPublishForReload,
                RunAppMode = settingsViewModel.RunAppMode,
                UseNoRestoreFlag = settingsViewModel.UseNoRestoreFlag,
                PresenterMode = settingsViewModel.PresenterMode,
                UseTieredJit = settingsViewModel.UseTieredJit,
                UseCustomRuntime = settingsViewModel.UseCustomRuntime,
                GraphvisDotPath = settingsViewModel.GraphvisDotPath,
                FgPhase = settingsViewModel.FgPhase,
                FgEnable = settingsViewModel.FgEnable,
                PrintInlinees = settingsViewModel.PrintInlinees,
                UsePGO = settingsViewModel.UsePGO,
                UseUnloadableContext = settingsViewModel.UseUnloadableContext,
                CustomJitName = settingsViewModel.SelectedCustomJit
            };
        }
    }
}