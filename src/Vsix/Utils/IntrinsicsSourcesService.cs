using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Disasmo.Utils
{
    public static class IntrinsicsSourcesService
    {
        public static async Task<List<IntrinsicsInfo>> ParseIntrinsics(Action<string> progressReporter)
        {
            List<IntrinsicsInfo> result = new List<IntrinsicsInfo>(600);
            const string baseUrl =
                "https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Private.CoreLib/src/System/Runtime/Intrinsics/";
            string[] files = {
                "X86/Aes.cs",
                "X86/Avx.cs",
                "X86/Avx2.cs",
                "X86/Bmi1.cs",
                "X86/Bmi2.cs",
                "X86/Fma.cs",
                "X86/Lzcnt.cs",
                "X86/Pclmulqdq.cs",
                "X86/Popcnt.cs",
                "X86/Sse.cs",
                "X86/Sse2.cs",
                "X86/Sse3.cs",
                "X86/Sse41.cs",
                "X86/Sse42.cs",
                "X86/Ssse3.cs",
                "X86/X86Base.cs",
                "X86/X86Serialize.cs",
                "X86/AvxVnni.cs",

                "Arm/AdvSimd.cs",
                "Arm/Aes.cs",
                "Arm/ArmBase.cs",
                "Arm/Crc32.cs",
                "Arm/Dp.cs",
                "Arm/Rdm.cs",
                "Arm/Sha1.cs",
                "Arm/Sha256.cs",

                "Vector64.cs",
                "Vector64_1.cs",
                "Vector128.cs",
                "Vector128_1.cs",
                "Vector256.cs",
                "Vector256_1.cs",
            };
            foreach (var file in files)
            {
                progressReporter(file);
                result.AddRange(await ParseSourceFile(baseUrl + file));
            }
            return result;
        }

        public static async Task<IEnumerable<IntrinsicsInfo>> ParseSourceFile(string url)
        {
            var client = new HttpClient();
            string content = await client.GetStringAsync(url);
            var result = new List<IntrinsicsInfo>();
            using (var workspace = new AdhocWorkspace())
            {
                Project proj = workspace.AddProject("ParseIntrinsics", LanguageNames.CSharp)
                    .WithMetadataReferences(new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
                Document doc = proj.AddDocument("foo", SourceText.From(content));
                Compilation compilation = await doc.Project.GetCompilationAsync();
                SyntaxNode root = await doc.GetSyntaxRootAsync();
                var model = compilation.GetSemanticModel(root.SyntaxTree);
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

                foreach (var method in methods)
                {
                    var tokens = method.ChildTokens().ToArray();
                    if (tokens.Length > 0)
                    {
                        var trivia = tokens.FirstOrDefault().LeadingTrivia;
                        string comments = string.Join("\n",
                            trivia.ToString().Split('\n').Select(i => i.Trim(' ', '\r', '\t'))
                                .Where(i => !string.IsNullOrWhiteSpace(i)));
                        var symbol = model.GetDeclaredSymbol(method);
                        var methodName = symbol.ToString()
                            .Replace("System.Runtime.Intrinsics.X86.", "")
                            .Replace("System.Runtime.Intrinsics.Arm.", "")
                            .Replace("System.Runtime.Intrinsics.", "");

                        var returnType = method.ReturnType.ToString();
                        result.Add(new IntrinsicsInfo { Method = returnType + " " + methodName, Comments = comments });
                    }
                }
            }

            return result;
        }
    }


    public class IntrinsicsInfo
    {
        public string Comments { get; set; }
        public string Method { get; set; }

        public bool Contains(string str)
        {
            return Comments.ToLowerInvariant().Contains(str.ToLowerInvariant()) ||
                   Method.ToLowerInvariant().Contains(str.ToLowerInvariant());
        }

        public override string ToString() => Method;
    }
}
