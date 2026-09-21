using System.IO.Abstractions;
using BenchmarkDotNet.Attributes;
using Kiyote.Buffers;
using Kiyote.Imaging.Gif;

namespace Kiyote.Imaging.Benchmarks.Gif;


[MemoryDiagnoser]
public class GifAnimationWriterBenchmarks {

	public const int Size = 100;
	public const int FrameCount = 10;

	private readonly IAnimationWriter _gifAnimationWriter;
	private readonly IBuffer<byte> _frame;

	private MemoryStream _stream = null!;

	public GifAnimationWriterBenchmarks() {
		_frame = new ArrayBuffer<byte>( Size, Size, 0 );
		byte value = 0;
		for( int row = 0; row < Size; row++ ) {
			for( int col = 0; col < Size; col++ ) {
				_frame[row, col] = value;
				unchecked {
					value++;
				}
			}
		}
		_gifAnimationWriter = new GifAnimationWriter( new FileSystem() );
	}

	[GlobalSetup]
	public void GlobalSetup() {
		_stream = new MemoryStream( Size * Size * FrameCount );
	}

	[GlobalCleanup]
	public void GlobalCleanup() {
		_stream.Dispose();
	}

	[Benchmark]
	public void WriteAnimation() {
		// The stream is reset here, rather than in an [IterationSetup], since
		// IterationSetup only runs once per batch of unrolled invocations,
		// not once per call. Resetting here keeps every invocation measuring
		// a single clean animation write instead of letting the buffer keep
		// growing (and reallocating) across the batch.
		_stream.Position = 0;
		_stream.SetLength( 0 );

		using IAnimationBuilder builder = _gifAnimationWriter.StartAnimation( _stream, TimeSpan.FromMilliseconds( 100 ) );
		for( int i = 0; i < FrameCount; i++ ) {
			builder.AddFrame( _frame );
		}
		builder.FinishAnimation();
	}

}
