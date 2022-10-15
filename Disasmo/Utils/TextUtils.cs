using System;
using System.IO;

namespace Disasmo.Utils;

public class TextUtils
{
    public static void SaveEmbeddedResourceTo(string resource, string folder, Func<string, string> contentProcessor = null)
        {
            string filePath = Path.Combine(folder, resource.Replace("_template", ""));
            if (File.Exists(filePath))
                return;

            using Stream stream = typeof(TextUtils).Assembly.GetManifestResourceStream("Disasmo.Resources."  + resource);
            using StreamReader reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            File.WriteAllText(filePath, contentProcessor is { } ? contentProcessor(content) : content);
        }

    public static string NormalizeLineEndings(string text) =>
        // normalize endings (DiffTool constantly complains)
        text.Replace(Environment.NewLine, "\n").Replace("\n", Environment.NewLine) + Environment.NewLine;
}