using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CodeAnalysis;

namespace Disasmo.Tests
{
    [TestClass]
    public class DisasmMethodOrClassActionTests
    {
        [TestMethod]
        public void TestDisasmoSuggestedActionSymbol()
        {
            var semanticModel = RoslynHelper.GetSemanticModelForSourceCode(ISymbolExtensionsTests.SourceCode);

            Check("MyClass");
            Check("MyStruct");
            Check("MyMethod");
            Check("MyProperty");
            Check("get { return p_");
            Check("set { p_");
            Check("this[");
            Check("get { return i_");
            Check("set { i_");
            Check("set { i_");

            Check("p_field", isInteresting: false);
            Check("i_field", isInteresting: false);
            Check("MyNamespace", isInteresting: false);
            Check("void", isInteresting: false);
            Check("return", isInteresting: false);
            Check("struct", isInteresting: false);
            Check("=", isInteresting: false);
            Check("value", isInteresting: false);

            async void Check(string uniqueIdentifier, bool isInteresting = true)
            {
                int tokenPosition = ISymbolExtensionsTests.SourceCode.IndexOf(uniqueIdentifier);
                var actionSymbol = await DisasmMethodOrClassAction.GetSymbolAsync(semanticModel, tokenPosition, default);
                var expectedSymbol = isInteresting ? RoslynHelper.GetSymbolAtPosition(semanticModel, tokenPosition) : null;
                Assert.IsTrue(SymbolEqualityComparer.Default.Equals(expectedSymbol, actionSymbol));
            }
        }
    }
}
