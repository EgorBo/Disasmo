using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Disasmo.ViewModels
{
    public class IntrinsicsViewModel : ViewModelBase
    {
        private string _input;
        private List<IntrinsicsInfoViewModel> _suggestions;
        private List<IntrinsicsInfoViewModel> _intrinsics;
        private bool _isBusy;
        private bool _isDownloading;
        private string _loadingStatus;

        public IntrinsicsViewModel()
        {
            if (IsInDesignMode)
            {
                Suggestions = new List<IntrinsicsInfoViewModel>
                {
                    new IntrinsicsInfoViewModel {Comments = "/// <summary>\n some comments 1\n</summary>", Method = "void Foo()"},
                    new IntrinsicsInfoViewModel {Comments = "/// <summary>\n some comments 2\n</summary>", Method = "void FooBoo(string str)"},
                };
            }
            else
                IsBusy = true;
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        public async void DownloadSources()
        {
            if (_isDownloading || _intrinsics?.Any() == true)
                return;

            IsBusy = true;
            _isDownloading = true;
            try
            {
                var result = new List<IntrinsicsInfoViewModel>();
                string loadingStatusPrefix = "Loading data from Github...\n";
                const string baseUrl =
                    "https://raw.githubusercontent.com/dotnet/coreclr/master/src/System.Private.CoreLib/shared/System/Runtime/Intrinsics/";
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
                    "X86/Ssse3.cs"
                    // Arm?
                };
                foreach (var file in files)
                {
                    LoadingStatus = loadingStatusPrefix + "Parsing " + file;
                    result.AddRange(await ParseSourceFile(baseUrl + file));
                }
                _intrinsics = result;
            }
            catch
            {
                // :(
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

        public List<IntrinsicsInfoViewModel> Suggestions
        {
            get => _suggestions;
            set => Set(ref _suggestions, value);
        }

        public static async Task<IEnumerable<IntrinsicsInfoViewModel>> ParseSourceFile(string url)
        {
            //var url = "https://raw.githubusercontent.com/dotnet/coreclr/master/src/System.Private.CoreLib/shared/System/Runtime/Intrinsics/X86/Sse2.cs";
            var client = new HttpClient();
            string content = await client.GetStringAsync(url);
            var result = new List<IntrinsicsInfoViewModel>();
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
                            .Replace("System.Runtime.Intrinsics.", "");

                        var returnType = method.ReturnType.ToString();
                        result.Add(new IntrinsicsInfoViewModel {Method = returnType + " " + methodName, Comments = comments });
                    }
                }
            }

            return result;
        }
    }

    public class IntrinsicsInfoViewModel : ViewModelBase
    {
        public string Comments { get; set; }
        public string Method { get; set; }

        public bool Contains(string str)
        {
            return Comments.ToLowerInvariant().Contains(str.ToLowerInvariant()) || 
                   Method.ToLowerInvariant().Contains(str.ToLowerInvariant());
        }
    }
}
