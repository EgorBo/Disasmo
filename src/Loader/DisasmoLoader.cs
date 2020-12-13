using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

// A small console app to load a specific assembly via ALC and precompile specified methods
//   "Disasmo.Loader MyDll.dll MyType"

public class DisasmoLoader
{
    public static void Main(string[] args)
    {
        PrecompileAllMethodsInType(args);
    }

    public static void PrecompileAllMethodsInType(string[] args)
    {
        if ((args.Length < 1) || (args.Length > 2))
            throw new InvalidOperationException();

        string assemblyName = args[0];
        string typeName = args.Length == 2 ? args[1] : null;

        var alc = new AssemblyLoadContext(null);
        Assembly asm = alc.LoadFromAssemblyPath(Path.Combine(Environment.CurrentDirectory, assemblyName));
        foreach (Type type in asm.GetTypes())
        {
            if (string.IsNullOrWhiteSpace(typeName) || type.FullName.Contains(typeName))
            {
                foreach (var method in type.GetMethods((BindingFlags)60))
                {
                    if (method.DeclaringType == type && !method.IsGenericMethod)
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    }
                }
            }
        }
    }
}