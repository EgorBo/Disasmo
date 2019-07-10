using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disasmo.Utils
{
    public static class DotnetCliUtils
    {
        public static string GetDotnetCliPath(string pathToLocalClr)
        {
            if (!string.IsNullOrWhiteSpace(pathToLocalClr))
            {
                string path = Path.Combine(pathToLocalClr, @".dotnet\dotnet.exe");
                if (File.Exists(path))
                    return path;
            }
            return "dotnet"; // from PATH
        }
    }
}
