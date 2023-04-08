namespace Disasmo;

public class DisasmoSymbolInfo
{
    public DisasmoSymbolInfo(string target, string className, string methodName, string genericArguments)
    {
        Target = target;
        ClassName = className;
        MethodName = methodName;
        GenericArguments = genericArguments;
    }

    public string Target { get; }
    public string ClassName { get; }
    public string MethodName { get; }
    public string GenericArguments { get; }
}