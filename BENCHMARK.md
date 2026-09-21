# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.9448/24H2/2024Update/HudsonValley)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
```

## GifAnimationWriter
| Method         | Mean     | Error   | StdDev  | Allocated |
|--------------- |---------:|--------:|--------:|----------:|
| WriteAnimation | 727.1 us | 6.17 us | 4.81 us |      61 B |


## GifImageWriter
| Method     | Mean     | Error    | StdDev   | Allocated |
|----------- |---------:|---------:|---------:|----------:|
| WriteImage | 68.64 us | 0.834 us | 0.652 us |         - |
