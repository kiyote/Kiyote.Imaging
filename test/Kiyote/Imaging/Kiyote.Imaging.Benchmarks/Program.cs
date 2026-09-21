
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Kiyote.Imaging.Benchmarks.Gif;

ManualConfig config = DefaultConfig.Instance
	.AddJob( Job
		 .MediumRun
		 .WithLaunchCount( 1 )
		 .WithToolchain( InProcessNoEmitToolchain.Instance ) );

BenchmarkSwitcher
	.FromTypes( [
		typeof( GifImageWriterBenchmarks ),
		typeof( GifAnimationWriterBenchmarks ),
	] )
	.RunAll( config, args );

