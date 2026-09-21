using Kiyote.Buffers;

namespace Kiyote.Imaging.Gif;

internal sealed class GifAnimationBuilder : IAnimationBuilder {

	private readonly Stream _output;
	private readonly TimeSpan _frameDelay;
	private readonly int _loopCount;
	private bool _headerWritten;
	private bool _finished;
	private int _width;
	private int _height;
	private int _minCodeSize;
	private int _frameCount;

	public GifAnimationBuilder(
		Stream stream,
		TimeSpan frameDelay,
		int loopCount
	) {
		_output = stream;
		_frameDelay = frameDelay;
		_loopCount = loopCount;
	}

	void IAnimationBuilder.AddFrame<T>(
		IBuffer<T> frame
	) {
		GifChunkWriter.ThrowIfPixelTypeNotSupported<T>();
		ArgumentNullException.ThrowIfNull( frame );

		if( _finished ) {
			throw new InvalidOperationException( "The animation has already been finished." );
		}

		if( !_headerWritten ) {
			_width = frame.Columns;
			_height = frame.Rows;
			_minCodeSize = GifChunkWriter.GetMinCodeSize<T>();

			GifChunkWriter.WriteSignature( _output );
			GifChunkWriter.WriteLogicalScreenDescriptor( _output, _width, _height, GifChunkWriter.GetColorTableSizeExponent<T>() );
			_output.Write( GifChunkWriter.GetPalette<T>() );
			GifChunkWriter.WriteNetscapeLoopExtension( _output, _loopCount );
			_headerWritten = true;
		} else if( frame.Columns != _width || frame.Rows != _height ) {
			throw new NotSupportedException( "All frames of an animation must have the same dimensions." );
		}

		ushort delayCentiseconds = (ushort)Math.Clamp( _frameDelay.TotalMilliseconds / 10, 0, ushort.MaxValue );
		GifChunkWriter.WriteGraphicControlExtension( _output, delayCentiseconds );

		byte[] indices = GifChunkWriter.GetIndices( frame, _width, _height );
		GifChunkWriter.WriteImageDescriptor( _output, _width, _height );
		GifChunkWriter.WriteImageData( _output, indices, _minCodeSize );

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
			GifChunkWriter.WriteTrailer( _output );
		} finally {
			_output.Dispose();
			_finished = true;
		}
	}

	void IDisposable.Dispose() {
		_output?.Dispose();
	}
}
