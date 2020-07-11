using System;

namespace CodeCounter
{
    public class Program
    {
        // The main entry point.
        // https://blog.codinghorror.com/coding-without-comments/
        // ex. CodeCounter -tree c:\mysources

        public const string kVersion = "2";

        static void Main(string[] args)
        {
            // Main entry point.

            Console.WriteLine("Code Counter v" + kVersion);

            bool waitOnDone = false;
            var stats = new CodeStats(Console.Out);
            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                string argL = arg.ToLower();

                if (argL == "-help" || argL == "-?")
                {
                    Console.WriteLine("CodeCounter walks a directory of .cs sources and compiles some statistics.");
                    Console.WriteLine("Use: CodeCounter -flag directory");
                    Console.WriteLine("-verbose : show methods.");
                    Console.WriteLine("-tree : display methods and names as tree.");
                    Console.WriteLine("-showmodules : list the modules.");
                    Console.WriteLine("-wait : pause at the end.");
                    return;
                }

                if (argL == "-wait")
                {
                    waitOnDone = true;
                    continue;
                }

                if (argL == "-showmodules")
                {
                    stats.ShowModules = true;
                    continue;
                }

                if (argL == "-tree")
                {
                    stats.MakeTree = true;
                    continue;
                }

                if (argL == "-verbose")
                {
                    stats.Verbose = true;
                    continue;
                }

                if (argL.StartsWith("-"))
                {
                    Console.WriteLine("Bad Arg");
                    return;
                }

                stats.RootDir = arg;
            }

            if (string.IsNullOrWhiteSpace(stats.RootDir))
            {
                stats.RootDir = Environment.CurrentDirectory;       // just use current dir.
            }

            Console.WriteLine($"Read Dir '{stats.RootDir}' for files of type {String.Join(",", CodeStats._exts)}");

            stats.ReadDir(stats.RootDir);
            stats.DumpStats();

            if (stats.ShowModules)
            {
                stats.NameSpaces.ShowModules(Console.Out);
            }

            if (waitOnDone)
            {
                Console.WriteLine("Press Enter to Continue");
                Console.ReadKey();
            }
        }
    }
}

