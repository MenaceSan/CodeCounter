//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeCounter
{
    public class Program
    {
        // The main entry point.
        // https://blog.codinghorror.com/coding-without-comments/
        // ex. CodeCounter -tree c:\mysources
        // -wait -v -tree -graph2 -un FourTe -ignore Chess C:\FourTe\Src C:\FourTe\Dot
        // -wait -v C:\Dennis\Source\Gray\GrayCore
        // C:\Dennis\Public\CodeCounter
        // C:\Lenovo\Udc1_0

        public const string kVersion = "5";

        static void Main(string[] args)
        {
            // Main entry point.

            Console.WriteLine("CodeCounter v" + kVersion);

            bool waitOnDone = false;
            var stats = new CodeStats(Console.Out);
            var roots = new List<string>();

            for (int argN = 0; argN < args.Length; argN++)
            {
                string arg = args[argN];
                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                switch (arg.ToLower())
                {
                    case "-h":
                    case "-help":
                    case "-?":
                        Console.WriteLine("CodeCounter walks a directory of .cs or .cpp sources and compiles some statistics.");
                        Console.WriteLine("Use: CodeCounter -flag directory directory2 ...");
                        Console.WriteLine("-graph1 : output the graphviz markup.");
                        Console.WriteLine("-ignore XX: Ignore all directories that match this regex pattern. e.g. (Test= all directories with the word Test in it)");
                        Console.WriteLine("-out XXX : pipe all output to a file.");
                        Console.WriteLine("-tree : display dir/file/class/methods as tree.");
                        Console.WriteLine("-unprefix XX: remove prefix from names.");
                        Console.WriteLine("-verbose : show methods.");
                        Console.WriteLine("-wait : pause at the end.");

                        // TODO unfinished commands
                        // Console.WriteLine("-graph Name : output the modules list to a .graphviz file by name.");
                        // "-licenses : list of licenses included with libraries and packages."
                        // "-showlibs : list local binary lib and nuget references."
                        // "-removexwhite : remove unnecessary whitespace on line ends."

                        return;

                    case "-g":
                    case "-graph1":
                        stats.GraphLevel = 1;
                        continue;
                    case "-graph2":
                        stats.GraphLevel = 2;
                        continue;
                    case "-i":
                    case "-ignore":
                        stats.Ignore.Add(args[++argN]);
                        continue;
                    case "-o":
                    case "-out":
                    case "-output":
                        stats.Out = new StreamWriter(File.OpenWrite(args[++argN]));
                        continue;
                    case "-t":
                    case "-tree":
                        stats.MakeTree = true;
                        continue;
                    case "-u":
                    case "-un":
                    case "-unprefix":
                        stats.NameSpaces.Unprefix = args[++argN];
                        continue;
                    case "-v":
                    case "-verbose":
                        stats.Verbose = true;
                        continue;
                    case "-w":
                    case "-wait":
                        waitOnDone = true;
                        continue;
                }

                if (arg.StartsWith("-"))
                {
                    Console.WriteLine($"Bad Arg '{arg}'");
                    return;
                }

                // Add multiple root directories ?
                if (!string.IsNullOrWhiteSpace(arg))
                    roots.Add(arg);
            }

            if (roots.Count <= 0)
            {
                roots.Add(Environment.CurrentDirectory);       // just use current dir.
            }

            int dirsProcessed = 0;
            foreach (string root in roots)
            {
                dirsProcessed++;
                stats.Out.WriteLine($"Read Dir '{root}' for files of type {string.Join(",", CodeStats.kExtsAll)}");
                stats.ReadRoot(root, dirsProcessed >= roots.Count);
            }

            stats.NameSpaces.FixupPackages();
            stats.DumpStats();

            if (stats.GraphLevel > 0)
            {
                stats.NameSpaces.ShowGraph(Console.Out, stats.GraphLevel);
            }

            if (waitOnDone)
            {
                Console.WriteLine("Press Enter to Continue");
                Console.ReadKey();
            }
        }
    }
}

