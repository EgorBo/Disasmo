using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disasmo.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Document = Microsoft.CodeAnalysis.Document;

namespace Disasmo;

internal class DisasmMethodOrClassAction : BaseSuggestedAction
{
    public DisasmMethodOrClassAction(CommonSuggestedActionsSource actionsSource) : base(actionsSource) {}

    public override async void Invoke(CancellationToken cancellationToken)
    {
        try
        {
            if (LastDocument != null)
            {
                var window = await IdeUtils.ShowWindowAsync<DisasmWindow>(true, cancellationToken);
                if (window?.ViewModel is {} viewModel)
                {
                    viewModel.RunOperationAsync(await GetSymbol(LastDocument, LastTokenPos, cancellationToken));
                }
            }
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
    }

    protected override async Task<bool> IsValidSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
    {
        try
        {
            if (Settings.Default.DisableLightBulb)
                return false;

            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null)
                return false;

            var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            var token = syntaxTree.FindToken(tokenPosition);
            if (token.Parent is MethodDeclarationSyntax)
                return true;
            if (token.Parent is ClassDeclarationSyntax)
                return true;
            if (token.Parent is StructDeclarationSyntax)
                return true;
            if (token.Parent is LocalFunctionStatementSyntax)
                return true;
            if (token.Parent is ConstructorDeclarationSyntax)
                return true;
            if (token.Parent is PropertyDeclarationSyntax)
                return true;
            if (token.Parent is OperatorDeclarationSyntax)
                return true;
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
        }
        return false;
    }


    static ISymbol FindRelatedSymbol(SemanticModel semanticModel, SyntaxNode node, bool allowClassesAndStructs, CancellationToken ct)
    {
        if (node is LocalFunctionStatementSyntax lf)
            return semanticModel.GetDeclaredSymbol(lf, ct);

        if (node is MethodDeclarationSyntax m)
            return semanticModel.GetDeclaredSymbol(m, ct);

        if (node is PropertyDeclarationSyntax p)
            return semanticModel.GetDeclaredSymbol(p, ct);

        if (node is OperatorDeclarationSyntax o)
            return semanticModel.GetDeclaredSymbol(o, ct);

        if (node is ConstructorDeclarationSyntax ctor)
            return semanticModel.GetDeclaredSymbol(ctor, ct);

        if (!allowClassesAndStructs)
            return null;

        if (node is ClassDeclarationSyntax c)
            return semanticModel.GetDeclaredSymbol(c, ct);

        if (node is StructDeclarationSyntax s)
            return semanticModel.GetDeclaredSymbol(s, ct);

        return null;
    }

    public static async Task<ISymbol> GetSymbolStatic(Document doc, int tok, CancellationToken ct, bool recursive = false)
    {
        try
        {
            SemanticModel semanticModel = await doc.GetSemanticModelAsync(ct);
            if (semanticModel == null)
                return null;

            SyntaxNode syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(ct);
            SyntaxToken token = syntaxTree.FindToken(tok);
            SyntaxNode parent = token.Parent;
            if (parent == null)
                return null;

            var symbol = FindRelatedSymbol(semanticModel, parent, true, ct);
            if (symbol == null && recursive)
            {
                while (true)
                {
                    parent = parent?.Parent;
                    if (parent == null)
                        return null;

                    symbol = FindRelatedSymbol(semanticModel, parent, false, ct);
                    if (symbol != null)
                    {
                        return symbol;
                    }
                }
            }
            return symbol;
        }
        catch (Exception exc)
        {
            Debug.WriteLine(exc);
            return null;
        }
    }

    protected virtual Task<ISymbol> GetSymbol(Document doc, int pos, CancellationToken ct) => 
        GetSymbolStatic(doc, pos, ct);

    public override string DisplayText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisasmoPackage.HotKey))
                return "Disasm this";
            return $"Disasm this ({DisasmoPackage.HotKey})";
        }
    }
}