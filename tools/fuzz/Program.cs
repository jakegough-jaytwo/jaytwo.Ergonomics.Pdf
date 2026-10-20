using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SharpFuzz;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

/// <summary>
/// Commands:
/// <list type="bullet">
/// <item><c>smoke [--iterations N] [--seed S] [--out DIR]</c>: the fixed-seed CI smoke run.</item>
/// <item><c>seeds DIR</c>: writes the in-process seed corpus, one directory per target.</item>
/// <item><c>run TARGET PATH...</c>: runs files or directories through a target and reports uncontrolled failures.</item>
/// <item><c>libfuzzer TARGET</c>: the SharpFuzz entry point for libfuzzer-dotnet campaigns.</item>
/// <item><c>list</c>: prints the target names and maximum input lengths.</item>
/// </list>
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: smoke | seeds DIR | run TARGET PATH... | libfuzzer TARGET | list");
            return 64;
        }

        string root = RepositoryRoot();
        switch (args[0])
        {
            case "list":
                foreach (FuzzTarget target in FuzzTargets.All)
                {
                    Console.WriteLine($"{target.Name} {target.MaxLength}");
                }

                return 0;
            case "seeds":
                SeedWriter.Write(SeedWriter.Build(root), args[1]);
                return 0;
            case "smoke":
                return Smoke(root, args);
            case "run":
                return RunFiles(FuzzTargets.Find(args[1]), args[2..]);
            case "libfuzzer":
                FuzzTarget fuzzTarget = FuzzTargets.Find(args[1]);
                Fuzzer.LibFuzzer.Run(span =>
                {
                    if (span.Length <= fuzzTarget.MaxLength)
                    {
                        fuzzTarget.Run(span.ToArray());
                    }
                });
                return 0;
            default:
                Console.Error.WriteLine("Unknown command '" + args[0] + "'.");
                return 64;
        }
    }

    private static int Smoke(string root, string[] args)
    {
        int iterations = 2000;
        int seed = 1019;
        string output = Path.Combine(root, "out", "fuzz-smoke");
        for (int i = 1; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--iterations":
                    iterations = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
                    break;
                case "--seed":
                    seed = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
                    break;
                case "--out":
                    output = args[i + 1];
                    break;
                default:
                    throw new ArgumentException("Unknown option '" + args[i] + "'.");
            }
        }

        Dictionary<string, List<byte[]>> seeds = SeedWriter.Build(root);
        string regressions = Path.Combine(root, "tools", "fuzz", "regressions");
        foreach (FuzzTarget target in FuzzTargets.All)
        {
            string directory = Path.Combine(regressions, target.Name);
            if (Directory.Exists(directory))
            {
                foreach (string path in SeedWriter.Sorted(Directory.GetFiles(directory)))
                {
                    seeds[target.Name].Add(File.ReadAllBytes(path));
                }
            }
        }

        Console.WriteLine($"fuzz smoke: seed {seed}, {iterations} mutations per target");
        int failures = new SmokeRunner(output).Run(seeds, iterations, seed);
        Console.WriteLine(failures == 0 ? "fuzz smoke passed" : $"fuzz smoke failed: {failures} uncontrolled failures");
        return failures == 0 ? 0 : 1;
    }

    private static int RunFiles(FuzzTarget target, string[] paths)
    {
        var files = new List<string>();
        foreach (string path in paths)
        {
            files.AddRange(Directory.Exists(path) ? SeedWriter.Sorted(Directory.GetFiles(path)) : new[] { path });
        }

        int failures = 0;
        foreach (string file in files)
        {
            byte[] input = File.ReadAllBytes(file);
            try
            {
                target.Run(input);
                Console.WriteLine($"ok {file}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"UNCONTROLLED {file}: {ex.GetType().FullName}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static string RepositoryRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (DirectoryInfo? directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "jaytwo.Ergonomics.Pdf.sln")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new InvalidOperationException("The repository root was not found.");
    }
}
