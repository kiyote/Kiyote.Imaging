using System.Buffers.Binary;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Png;

internal sealed class APngAnimationBuilder : IAnimationBuilder {

	private const int DelayDenominator = 1000;
	private const byte DisposeOpNone = 0;
	private const byte BlendOpSource = 0;

	private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];

	private readonly TimeSpan _frameDelay;
	private readonly int _loopCount;

	private readonly Stream _output;
	private readonly bool _ownsStream;
	private bool _headerWritten;
	private bool _finished;
	private int _width;
	private int _height;
	private uint _sequenceNumber;
	private int _frameCount;
	private long _animationControlPosition;

	public APngAnimationBuilder(
		Stream stream,
		TimeSpan frameDelay,
		int loopCount,
		bool ownsStream = false
	) {
		_output = stream;
		_frameDelay = frameDelay;
		_loopCount = loopCount;
		_ownsStream = ownsStream;
	}

	void IAnimationBuilder.AddFrame<T>(
		IBuffer<T> frame
	) {
		PngChunkWriter.ThrowIfPixelTypeNotSupported<T>();
		ArgumentNullException.ThrowIfNull( frame );

		if( _finished ) {
			throw new InvalidOperationException( "The animation has already been finished." );
		}

		if( !_headerWritten ) {
			_output.Write( _signature );
			_width = frame.Columns;
			_height = frame.Rows;
			PngChunkWriter.WriteHeader( _output, _width, _height );
			_animationControlPosition = _output.Position;
			WriteAnimationControl( _output, 0, _loopCount );
			_headerWritten = true;
		} else if( frame.Columns != _width || frame.Rows != _height ) {
			throw new NotSupportedException( "All frames of an animation must have the same dimensions." );
		}

		ushort delayNum = (ushort)Math.Clamp( _frameDelay.TotalMilliseconds, 0, ushort.MaxValue );
		WriteFrameControl( _output, ref _sequenceNumber, _width, _height, delayNum );

		byte[] compressed = PngChunkWriter.CompressFrame( frame, _width, _height );
		if( _frameCount == 0 ) {
			PngChunkWriter.WriteChunk( _output, "IDAT", compressed );
		} else {
			WriteFrameData( _output, ref _sequenceNumber, compressed );
		}

		_frameCount++;
	}

	void IAnimationBuilder.FinishAnimation() {
		if( _finished ) {
			throw new InvalidOperationException( "The animation has already been finished." );
		}

		if( _output is null || _frameCount == 0 ) {
			throw new InvalidOperationException( "At least one frame is required to write an animation." );
		}

		try {
			PngChunkWriter.WriteChunk( _output, "IEND", [] );

			long endPosition = _output.Position;
			_output.Position = _animationControlPosition;
			WriteAnimationControl( _output, _frameCount, _loopCount );
			_output.Position = endPosition;
		} finally {
			if( _ownsStream ) {
				_output.Dispose();
			}
			_finished = true;
		}
	}

	void IDisposable.Dispose() {
		if( _ownsStream ) {
			_output?.Dispose();
		}
	}

	private static void WriteAnimationControl(
		Stream output,
		int frameCount,
		int loopCount
	) {
		byte[] data = new byte[8];
		BinaryPrimitives.WriteInt32BigEndian( data.AsSpan( 0, 4 ), frameCount );
		BinaryPrimitives.WriteInt32BigEndian( data.AsSpan( 4, 4 ), loopCount );

		PngChunkWriter.WriteChunk( output, "acTL", data );
	}

	private static void WriteFrameControl(
		Stream output,
		ref uint sequenceNumber,
		int width,
		int height,
		ushort delayNum
	) {
		byte[] data = new byte[26];
		BinaryPrimitives.WriteUInt32BigEndian( data.AsSpan( 0, 4 ), sequenceNumber );
		BinaryPrimitives.WriteInt32BigEndian( data.AsSpan( 4, 4 ), width );
		BinaryPrimitives.WriteInt32BigEndian( data.AsSpan( 8, 4 ), height );
		BinaryPrimitives.WriteInt32BigEndian( data.AsSpan( 12, 4 ), 0 ); // x_offset
		BinaryPrimitives.WriteInt32BigEndian( data.AsSpan( 16, 4 ), 0 ); // y_offset
		BinaryPrimitives.WriteUInt16BigEndian( data.AsSpan( 20, 2 ), delayNum );
		BinaryPrimitives.WriteUInt16BigEndian( data.AsSpan( 22, 2 ), DelayDenominator );
		data[24] = DisposeOpNone;
		data[25] = BlendOpSource;

		PngChunkWriter.WriteChunk( output, "fcTL", data );
		sequenceNumber++;
	}

	private static void WriteFrameData(
		Stream output,
		ref uint sequenceNumber,
		ReadOnlySpan<byte> compressed
	) {
		byte[] data = new byte[4 + compressed.Length];
		BinaryPrimitives.WriteUInt32BigEndian( data.AsSpan( 0, 4 ), sequenceNumber );
		compressed.CopyTo( data.AsSpan( 4 ) );

		PngChunkWriter.WriteChunk( output, "fdAT", data );
		sequenceNumber++;
	}
}
