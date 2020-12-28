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
            // We replace pluses with dots because 'typeName' is a C#'y name of the type
            // Unfortunately, Roslyn doesn't have a display option to output the runtime name of the type
            // And we do not want to complicate things by formatting the type's name ourselves
            // This is the easiest solution to that problem
            if (string.IsNullOrWhiteSpace(typeName) || type.FullName.Replace('+', '.').Contains(typeName))
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