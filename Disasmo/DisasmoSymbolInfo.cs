namespace Disasmo;

public class DisasmoSymbolInfo
{
    public DisasmoSymbolInfo(string target, string className, string methodName)
    {
        Target = target;
        ClassName = className;
        MethodName = methodName;
    }

    public string Target { get; }
    public string ClassName { get; }
    public string MethodName { get; }
}