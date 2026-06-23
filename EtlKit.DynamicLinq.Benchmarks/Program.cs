using BenchmarkDotNet.Running;

namespace EtlKit.DynamicLinq.Benchmarks;

public static class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
