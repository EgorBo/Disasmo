﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Disasmo.Utils
{
    public class ComPlusDisassemblyPrettifier
    {
        /// <summary>
        /// Handles DOTNET_JitDisasm's asm format:
        /// 
        ///   ; Assembly listing for method Program:MyMethod()
        ///   ; bla-bla
        ///   ; bla-bla
        /// 
        ///   G_M42249_IG01:
        ///          0F1F440000       nop
        ///        
        ///   G_M42249_IG02:
        ///          B82A000000       mov eax, 42
        ///        
        ///   G_M42249_IG03:
        ///          C3               ret
        ///        
        ///   ; Total bytes of code 76, prolog size 5 for method Program:SelectBucketIndex_old(int):int
        ///   ; ============================================================
        /// </summary>
        public static string Prettify(string rawAsm, bool minimalComments)
        {
            if (!minimalComments)
                return rawAsm;
            try
            { 
                var lines = rawAsm.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var blocks = new List<Block>();

                var prevBlock = BlockType.Unknown;
                var currentMethod = "";

                foreach (var line in lines)
                {
                    if (line.Contains("; Assembly listing for method "))
                        currentMethod = line.Remove(0, "; Assembly listing for method ".Length);
                    else if (currentMethod == "")
                        return rawAsm; // in case if format is changed

                    var currentBlock = BlockType.Unknown;

                    if (line.StartsWith(";"))
                        currentBlock = BlockType.Comments;
                    else if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    else 
                    {
                        currentBlock = BlockType.Code;
                        if (Regex.IsMatch(line, @"^\w+:"))
                        {
                            prevBlock = BlockType.Unknown;
                        }
                    }

                    if (currentBlock != prevBlock)
                    {
                        blocks.Add(new Block { MethodName = currentMethod, Type = currentBlock,  Data = $"\n{line}\n" });
                        prevBlock = currentBlock;
                    }
                    else
                        blocks[blocks.Count - 1].Data += line + "\n";
                }

                var blocksByMethods = blocks.GroupBy(b => b.MethodName);
                var output = new StringBuilder();

                foreach (var method in blocksByMethods)
                {
                    List<Block> methodBlocks = method.ToList();

                    int size = ParseMethodTotalSizes(methodBlocks);

                    if (minimalComments)
                    {
                        methodBlocks = methodBlocks.Where(m => m.Type != BlockType.Comments).ToList();
                        output.Append($"; Method {method.Key}");
                    }

                    foreach (var block in methodBlocks)
                        output.Append(block.Data);

                    if (minimalComments)
                    {
                        output.Append("; Total bytes of code: ")
                            .Append(size)
                            .AppendLine()
                            .AppendLine();
                    }
                }

                return output.ToString();
            }
            catch
            {
                return rawAsm; // format is changed - leave it as is
            }
        }

        private static int ParseMethodTotalSizes(List<Block> methodBlocks)
        {
            const string marker = "; Total bytes of code ";
            string lineToParse = methodBlocks.First(b => b.Data.Contains(marker)).Data;
            string size = lineToParse.Substring(marker.Length, lineToParse.IndexOf(',') - marker.Length);
            return int.Parse(size);
        }

        private enum BlockType
        {
            Unknown,
            Comments,
            Code
        }

        private class Block
        {
            public string MethodName { get; set; }
            public BlockType Type { get; set; }
            public string Data { get; set; }
        }
    }
}
