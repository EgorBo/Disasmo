using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DisasmoConsoleApp
{
    public class Program
    {
        static void Main(string[] args)
        {
            var flags = BindingFlags.Instance | 
                        BindingFlags.Static   |
                        BindingFlags.Public   | 
                        BindingFlags.NonPublic;

            //TODO: use reflection to access %typename%

// generic methods (generic types are not supported yet)
#if DISASMO_PREPARE_GENERIC_METHOD
            typeof(%typename%).GetMethods(flags)
                .Where(w => w.DeclaringType == typeof(%typename%) && 
                            w.IsGenericMethod && 
                            w.GetGenericArguments().Length == %typeparameterscount%)
                .ToList()
                .ForEach(m => RuntimeHelpers.PrepareMethod(m.MethodHandle, 
                    new RuntimeTypeHandle[]
                    {
                        %typeparameters%
                    }));
#endif

// regular methods and types
#if DISASMO_PREPARE_CLASS
            typeof(%typename%).GetMethods(flags)
                .Where(w => w.DeclaringType == typeof(%typename%) && !w.IsGenericMethod).ToList()
                    .ForEach(m => RuntimeHelpers.PrepareMethod(m.MethodHandle));
            System.Console.WriteLine(" ");
#if DISASMO_WAIT_FOR_ATTACH
            System.Console.ReadLine();
#endif
#endif

// ObjectLayoutInspector
#if DISASMO_OBJECT_LAYOUT_INSPECTOR
            ObjectLayoutInspector.TypeLayout.PrintLayout<%typename%>(recursively: true);
#endif
        }
    }
}
