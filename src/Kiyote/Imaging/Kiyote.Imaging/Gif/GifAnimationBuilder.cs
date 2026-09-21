using Kiyote.Buffers;

namespace Kiyote.Imaging.Gif;

internal sealed class GifAnimationBuilder : GifWriterBase, IAnimationBuilder {

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
		ThrowIfPixelTypeNotSupported<T>();
		ArgumentNullException.ThrowIfNull( frame );

		if( _finished ) {
			throw new InvalidOperationException( "The animation has already been finished." );
		}

		if( !_headerWritten ) {
			_width = frame.Columns;
			_height = frame.Rows;
			_minCodeSize = GetMinCodeSize<T>();

			WriteSignature( _output );
			WriteLogicalScreenDescriptor( _output, _width, _height, GetColorTableSizeExponent<T>() );
			WritePalette<T>( _output );
			WriteNetscapeLoopExtension( _output, _loopCount );
			_headerWritten = true;
		} else if( frame.Columns != _width || frame.Rows != _height ) {
			throw new NotSupportedException( "All frames of an animation must have the same dimensions." );
		}

		ushort delayCentiseconds = (ushort)Math.Clamp( _frameDelay.TotalMilliseconds / 10, 0, ushort.MaxValue );
		WriteGraphicControlExtension( _output, delayCentiseconds );

		WriteImageDescriptor( _output, _width, _height );
		WriteImageData( _output, frame, _width, _height, _minCodeSize );

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
			WriteTrailer( _output );
		} finally {
			_output.Dispose();
			_finished = true;
		}
	}

	void IDisposable.Dispose() {
		_output?.Dispose();
	}
}

