using System.IO.Abstractions;
using BenchmarkDotNet.Attributes;
using Kiyote.Buffers;
using Kiyote.Imaging.Gif;

namespace Kiyote.Imaging.Benchmarks.Gif;


[MemoryDiagnoser]
public class GifImageWriterBenchmarks {

	public const int Size = 100;

	private readonly IImageWriter _gifImageWriter;
	private readonly IBuffer<byte> _frame;

	private MemoryStream _stream = null!;

	public GifImageWriterBenchmarks() {
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
		_gifImageWriter = new GifImageWriter( new FileSystem() );
	}

	[GlobalSetup]
	public void GlobalSetup() {
		_stream = new MemoryStream( Size * Size );
	}

	[GlobalCleanup]
	public void GlobalCleanup() {
		_stream.Dispose();
	}

	[Benchmark]
	public void WriteImage() {
		// IterationSetup only runs once per batch of unrolled invocations, not
		// once per call, so the stream must be reset here to keep every
		// invocation measuring a single clean write instead of letting the
		// buffer keep growing (and reallocating) across the batch.
		_stream.Position = 0;
		_gifImageWriter.WriteImage( _stream, _frame );
	}

}

