# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.9448/24H2/2024Update/HudsonValley)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
```

## GifWriter
| Method     | Mean     | Error    | StdDev   | Gen0    | Gen1    | Gen2    | Allocated |
|----------- |---------:|---------:|---------:|--------:|--------:|--------:|----------:|
| WriteImage | 262.9 us | 12.94 us | 11.47 us | 30.2734 | 30.2734 | 30.2734 |  232.4 KB |


## GifImageWriter
| Method     | Mean     | Error    | StdDev   | Allocated |
|----------- |---------:|---------:|---------:|----------:|
| WriteImage | 65.93 us | 1.363 us | 1.208 us |       1 B |
