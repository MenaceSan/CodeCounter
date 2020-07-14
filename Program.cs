//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;

namespace CodeCounter
{
    public class Program
    {
        // The main entry point.
        // https://blog.codinghorror.com/coding-without-comments/
        // ex. CodeCounter -tree c:\mysources
        // -wait -tree -graph0 -ignore Chess C:\FourTe\Src C:\FourTe\Dot

        public const string kVersion = "3";

        static void Main(string[] args)
        {
            // Main entry point.

            Console.WriteLine("Code Counter v" + kVersion);

            bool waitOnDone = false;
            var stats = new CodeStats(Console.Out);
            var roots = new List<string>();

            for (int argN = 0; argN < args.Length; argN++)
            {
                string arg = args[argN];
                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                string argL = arg.ToLower();

                if (argL == "-help" || argL == "-?")
                {
                    Console.WriteLine("CodeCounter walks a directory of .cs sources and compiles some statistics.");
                    Console.WriteLine("Use: CodeCounter -flag directory directory2 ...");
                    Console.WriteLine("-graph0 : list the modules to output.");
                    Console.WriteLine("-ignore XX: Ignore all directories that match this regex pattern. e.g. (Test= all directories with the word Test in it)");
                    Console.WriteLine("-tree : display methods and names as tree.");
                    Console.WriteLine("-unprefix XX: remove prefix from names.");
                    Console.WriteLine("-verbose : show methods.");
                    Console.WriteLine("-wait : pause at the end.");

                    // TODO
                    // Console.WriteLine("-graph Name : output the modules list to a graphviz file by name.");
                    // -licenses : list of licenses included with libraries and packages.
                    // -showlibs (local and nuget)

                    return;
                }

                if (argL == "-graph0")
                {
                    stats.Graph0 = true;
                    continue;
                }

                if (argL == "-ignore")
                {
                    stats.Ignore.Add(args[++argN]);
                    continue;
                }

                if (argL == "-tree")
                {
                    stats.MakeTree = true;
                    continue;
                }

                if (argL == "-unprefix")
                {
                    stats.Unprefix = args[++argN];
                    continue;
                }

                if (argL == "-verbose")
                {
                    stats.Verbose = true;
                    continue;
                }

                if (argL == "-wait")
                {
                    waitOnDone = true;
                    continue;
                }

                if (argL.StartsWith("-"))
                {
                    Console.WriteLine($"Bad Arg '{argL}'");
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
                Console.WriteLine($"Read Dir '{root}' for files of type {String.Join(",", CodeStats.kExtsAll)}");
                stats.RootDir = root;
                stats.ReadDir(root, dirsProcessed >= roots.Count);
            }

            stats.DumpStats();

            if (stats.Graph0)
            {
                stats.NameSpaces.ShowGraph0(Console.Out);
            }

            if (waitOnDone)
            {
                Console.WriteLine("Press Enter to Continue");
                Console.ReadKey();
            }
        }
    }
}

