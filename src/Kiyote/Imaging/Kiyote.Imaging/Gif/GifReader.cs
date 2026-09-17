using System.IO.Abstractions;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Gif;

public sealed class GifReader : IImageReader {

	private readonly IBufferFactory _bufferFactory;
	private readonly IFileSystem _fileSystem;

	public GifReader(
		IBufferFactory bufferFactory,
		IFileSystem fileSystem
	) {
		ArgumentNullException.ThrowIfNull( bufferFactory );
		ArgumentNullException.ThrowIfNull( fileSystem );
		_bufferFactory = bufferFactory;
		_fileSystem = fileSystem;
	}

	IBuffer<T> IImageReader.ReadImage<T>(
		string filePath
	) {
		GifChunkWriter.ThrowIfPixelTypeNotSupported<T>();

		byte[] bytes = _fileSystem.File.ReadAllBytes( filePath );
		GifFrame frame = GifChunkReader.ReadFirstFrame( bytes );

		byte[] grayscale = new byte[frame.Width * frame.Height];
		for( int i = 0; i < frame.Indices.Length; i++ ) {
			grayscale[i] = frame.Palette[frame.Indices[i] * 3];
		}

		return CreateBuffer<T>( grayscale, frame.Width, frame.Height );
	}

	private IBuffer<T> CreateBuffer<T>(
		byte[] grayscale,
		int width,
		int height
	) {
		if( typeof( T ) == typeof( bool ) ) {
			IBuffer<bool> buffer = _bufferFactory.Create( width, height, false );
			for( int y = 0; y < height; y++ ) {
				ReadOnlySpan<byte> source = grayscale.AsSpan( y * width, width );
				Span<bool> row = buffer.GetRowSpan( y );
				for( int x = 0; x < width; x++ ) {
					row[x] = source[x] != 0;
				}
			}
			return (IBuffer<T>)buffer;
		}

		IBuffer<byte> greyscaleBuffer = _bufferFactory.Create( width, height, byte.MinValue );
		for( int y = 0; y < height; y++ ) {
			grayscale.AsSpan( y * width, width ).CopyTo( greyscaleBuffer.GetRowSpan( y ) );
		}
		return (IBuffer<T>)greyscaleBuffer;
	}
}
