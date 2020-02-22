using Microsoft.VisualStudio.TestTools.UnitTesting;
using Disasmo.Analyzers;

namespace Disasmo.Tests
{
    [TestClass]
    public class ISymbolExtensionsTests
    {
        internal const string SourceCode = @"
namespace MyNamespace
{
    class MyClass
    {
        int p_field;
        int i_field;

        void MyMethod() { }

        int MyProperty
        {
            get { return p_field; }
            set { p_field = value; }
        }

        int this[int i]
        {
            get { return i_field; }
            set { i_field = value; }
        }
    }
    struct MyStruct { }
}
";

        [TestMethod]
        public void GetDisasmSuggestedActionDisplayTextTest()
        {
            Check("Disasm 'MyClass' class", "MyClass");
            Check("Disasm 'MyStruct' struct", "MyStruct");
            Check("Disasm 'MyMethod' method", "MyMethod");
            Check("Disasm 'MyProperty' property", "MyProperty");
            Check("Disasm 'MyProperty' property getter", "get { return p_");
            Check("Disasm 'MyProperty' property setter", "set { p_");
            Check("Disasm 'this[]' indexer", "this[");
            Check("Disasm 'this[]' indexer getter", "get { return i_");
            Check("Disasm 'this[]' indexer setter", "set { i_");

            void Check(string expectedText, string uniqueIdentifier)
            {
                var symbol = RoslynHelper.GetSymbolByUniqueIdentifier(SourceCode, uniqueIdentifier);
                Assert.AreEqual(expectedText, symbol.GetDisasmSuggestedActionDisplayText());
            }
        }

        [TestMethod]
        public void GetJitDisasmTargetTest()
        {
            Check("MyClass::*", "MyClass");
            Check("MyStruct::*", "MyStruct");
            Check("MyClass::MyMethod", "MyMethod");
            Check("MyClass::get_MyProperty MyClass::set_MyProperty", "MyProperty");
            Check("MyClass::get_MyProperty", "get { return p_");
            Check("MyClass::set_MyProperty", "set { p_");
            Check("MyClass::get_Item MyClass::set_Item", "this[");
            Check("MyClass::get_Item", "get { return i_");
            Check("MyClass::set_Item", "set { i_");

            void Check(string expectedText, string uniqueIdentifier)
            {
                var symbol = RoslynHelper.GetSymbolByUniqueIdentifier(SourceCode, uniqueIdentifier);
                Assert.AreEqual(expectedText, symbol.GetJitDisasmTarget());
            }
        }

        [TestMethod]
        public void GetContainingTypeNameOrSelfTest()
        {
            Check("MyStruct", "MyStruct");
            Check("MyClass", "MyClass");
            Check("MyClass", "MyMethod");
            Check("MyClass", "MyProperty");
            Check("MyClass", "get { return p_");
            Check("MyClass", "set { p_");
            Check("MyClass", "this[");
            Check("MyClass", "get { return i_");
            Check("MyClass", "set { i_");

            static void Check(string expectedName, string uniqueIdentifier)
            {
                var symbol = RoslynHelper.GetSymbolByUniqueIdentifier(SourceCode, uniqueIdentifier);
                Assert.AreEqual(expectedName, symbol.GetContainingTypeNameOrSelf());
            }
        }
    }
}
