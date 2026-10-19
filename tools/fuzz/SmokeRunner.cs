using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

/// <summary>
/// The fixed-seed CI smoke run. Every seed runs as-is, then a fixed number of deterministic mutations per target.
/// An escaping exception is a failure; an input running past the time limit ends the process.
/// </summary>
internal sealed class SmokeRunner
{
    private static readonly TimeSpan InputTimeLimit = TimeSpan.FromSeconds(10);

    private readonly string _outputDirectory;
    private readonly object _gate = new object();
    private byte[]? _current;
    private string _currentTarget = string.Empty;
    private long _startedTicks;

    public SmokeRunner(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
    }

    public int Run(Dictionary<string, List<byte[]>> seeds, int iterations, int seed)
    {
        Directory.CreateDirectory(_outputDirectory);
        var watchdog = new Thread(Watch) { IsBackground = true, Name = "fuzz-smoke-watchdog" };
        watchdog.Start();

        int failures = 0;
        foreach (FuzzTarget target in FuzzTargets.All)
        {
            List<byte[]> pool = seeds[target.Name];
            if (pool.Count == 0)
            {
                Console.WriteLine($"{target.Name}: no seeds");
                failures++;
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            int targetFailures = 0;
            foreach (byte[] input in pool)
            {
                targetFailures += Execute(target, input);
            }

            var mutator = new Mutator(seed + StableHash(target.Name));
            for (int i = 0; i < iterations; i++)
            {
                targetFailures += Execute(target, mutator.Mutate(pool[i % pool.Count], pool, target.MaxLength));
            }

            Console.WriteLine($"{target.Name}: {pool.Count} seeds, {iterations} mutations, {targetFailures} failures, {stopwatch.Elapsed.TotalSeconds:F1} s");
            failures += targetFailures;
        }

        Volatile.Write(ref _current, null);
        return failures;
    }

    private static int StableHash(string value)
    {
        int hash = 17;
        foreach (char c in value)
        {
            hash = unchecked((hash * 31) + c);
        }

        return hash & 0x7FFFFFFF;
    }

    private int Execute(FuzzTarget target, byte[] input)
    {
        lock (_gate)
        {
            _currentTarget = target.Name;
            _startedTicks = Stopwatch.GetTimestamp();
            _current = input;
        }

        try
        {
            target.Run(input);
            return 0;
        }
        catch (Exception ex)
        {
            string path = Save("failure", target.Name, input);
            Console.WriteLine($"{target.Name}: uncontrolled {ex.GetType().FullName}: {ex.Message}");
            Console.WriteLine($"  input: {path}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
        finally
        {
            lock (_gate)
            {
                _current = null;
            }
        }
    }

    private void Watch()
    {
        while (true)
        {
            Thread.Sleep(500);
            lock (_gate)
            {
                if (_current != null && Stopwatch.GetElapsedTime(_startedTicks) > InputTimeLimit)
                {
                    string path = Save("hang", _currentTarget, _current);
                    Console.WriteLine($"{_currentTarget}: input ran past {InputTimeLimit.TotalSeconds} s: {path}");
                    Console.Out.Flush();
                    Environment.Exit(2);
                }
            }
        }
    }

    private string Save(string kind, string target, byte[] input)
    {
        string path = Path.Combine(_outputDirectory, kind + "-" + target + "-" + SeedWriter.Hash(input));
        File.WriteAllBytes(path, input);
        return path;
    }
}
