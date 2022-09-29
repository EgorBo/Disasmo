namespace Disasmo;

public class DisasmoSymbolInfo
{
    public DisasmoSymbolInfo(string target, string methodName, string className)
    {
        Target = target;
        MethodName = methodName;
        ClassName = className;
    }

    public string Target { get; }
    public string MethodName { get; }
    public string ClassName { get; }
}