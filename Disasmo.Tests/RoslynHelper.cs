using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Disasmo.Tests
{
    public static class RoslynHelper
    {
        private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);

        public static SemanticModel GetSemanticModelForSourceCode(string source)
        {
            const string TestProjectName = "TestProject";

            var projectId = ProjectId.CreateNewId();
            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName, LanguageNames.CSharp)
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, SystemCoreReference);

            var sourceText = SourceText.From(source);
            var document = solution.Projects.Single().AddDocument("NewFile.cs", sourceText);

            return document.GetSemanticModelAsync().Result;
        }

        public static ISymbol GetSymbolAtPosition(this SemanticModel semanticModel, int tokenPosition)
        {
            var syntaxTree = semanticModel.SyntaxTree.GetRoot();
            var token = syntaxTree.FindToken(tokenPosition);
            var symbol = semanticModel.GetDeclaredSymbol(token.Parent); //null for getters and setters
            return symbol ?? semanticModel.GetEnclosingSymbol(tokenPosition);
        }

        public static ISymbol GetSymbolByUniqueIdentifier(string source, string symbolIdentifier)
        {
            var semanticModel = GetSemanticModelForSourceCode(source);
            return semanticModel.GetSymbolAtPosition(source.IndexOf(symbolIdentifier));
        }
    }
}