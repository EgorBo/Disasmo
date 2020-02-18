using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Disasmo.Utils;

namespace ConsolePlayground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            List<IntrinsicsInfo> intrinsics = await IntrinsicsSourcesService.ParseIntrinsics(Console.WriteLine);
            foreach (var intrin in intrinsics)
            {
                Console.WriteLine(intrin.Method);
            }
        }
    }
}
