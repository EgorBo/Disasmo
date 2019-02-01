using System;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Disasmo
{
    public static class DiffTools
    {
        public static void Diff(string contentLeft, string contentRight)
        {
            string tmpFileLeft = Path.GetTempFileName();
            string tmpFileRight = Path.GetTempFileName();

            File.WriteAllText(tmpFileLeft, contentLeft);
            File.WriteAllText(tmpFileRight, contentRight);

            try
            {
                // Copied from https://github.com/madskristensen/FileDiffer/blob/master/src/Commands/DiffFilesCommand.cs#L48-L56 (c) madskristensen
                object args = $"\"{tmpFileLeft}\" \"{tmpFileRight}\"";
                ((DTE)Package.GetGlobalService(typeof(SDTE))).Commands.Raise("5D4C0442-C0A2-4BE8-9B4D-AB1C28450942", 256, ref args, ref args);
            }
            catch (Exception e)
            {
                return;
            }
            finally
            {
                File.Delete(tmpFileLeft);
                File.Delete(tmpFileRight);
            }
        }
    }
}
