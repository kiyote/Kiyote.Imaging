using System.Buffers.Binary;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Text;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Png;

internal sealed class PngWriter : IImageWriter {

	private const byte BitDepth = 8;
	private const byte ColourTypeRgba = 6;
	private const int BytesPerPixel = 4;

	private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];

	private readonly IFileSystem _fileSystem;

	public PngWriter(
		IFileSystem fileSystem
	) {
		ArgumentNullException.ThrowIfNull( fileSystem );
		_fileSystem = fileSystem;
	}

	void IImageWriter.WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	) {
		if (typeof(T) != typeof(uint)
			&& typeof(T) != typeof(int)
			&& typeof(T) != typeof(bool)
		) {
			throw new NotSupportedException( "The pixel type is not supported. Supported types are: uint, int, bool." );
		}

		ArgumentNullException.ThrowIfNull( pixels );

		int width = pixels.Columns;
		int height = pixels.Rows;

		using Stream output = _fileSystem.File.Create( filePath );
		output.Write( _signature );

		WriteHeader( output, width, height );
		WriteData( output, pixels, width, height );
		WriteChunk( output, "IEND", [] );
	}

	private static void WriteHeader(
		Stream output,
		int width,
		int height
	) {
		byte[] header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian( header.AsSpan( 0, 4 ), width );
		BinaryPrimitives.WriteInt32BigEndian( header.AsSpan( 4, 4 ), height );
		header[8] = BitDepth;
		header[9] = ColourTypeRgba;
		header[10] = 0; // Compression method
		header[11] = 0; // Filter method
		header[12] = 0; // Interlace method

		WriteChunk( output, "IHDR", header );
	}

	private static void WriteData<T>(
		Stream output,
		IBuffer<T> pixels,
		int width,
		int height
	) {
		using MemoryStream raw = new MemoryStream();
		using( ZLibStream compressor = new ZLibStream( raw, CompressionLevel.Optimal, leaveOpen: true ) ) {
			byte[] scanline = new byte[1 + ( width * BytesPerPixel )];
			for( int y = 0; y < height; y++ ) {
				scanline[0] = 0; // Filter type: None
				FillScanline( pixels, y, width, scanline.AsSpan( 1 ) );
				compressor.Write( scanline );
			}
		}

		WriteChunk( output, "IDAT", raw.GetBuffer().AsSpan( 0, (int)raw.Length ) );
	}

	private static void FillScanline<T>(
		IBuffer<T> pixels,
		int y,
		int width,
		Span<byte> destination
	) {
		if( typeof( T ) == typeof( bool ) ) {
			IBuffer<bool> source = (IBuffer<bool>)pixels;
			for( int x = 0; x < width; x++ ) {
				byte value = source[x, y] ? byte.MinValue : byte.MaxValue;
				int offset = x * BytesPerPixel;
				destination[offset] = value;
				destination[offset + 1] = value;
				destination[offset + 2] = value;
				destination[offset + 3] = byte.MaxValue;
			}
			return;
		}

		if( typeof( T ) == typeof( int ) ) {
			IBuffer<int> intSource = (IBuffer<int>)pixels;
			for( int x = 0; x < width; x++ ) {
				BinaryPrimitives.WriteUInt32BigEndian(
					destination.Slice( x * BytesPerPixel, BytesPerPixel ),
					unchecked( (uint)intSource[x, y] )
				);
			}
			return;
		}

		IBuffer<uint> uintSource = (IBuffer<uint>)pixels;
		for( int x = 0; x < width; x++ ) {
			BinaryPrimitives.WriteUInt32BigEndian(
				destination.Slice( x * BytesPerPixel, BytesPerPixel ),
				uintSource[x, y]
			);
		}
	}

	private static void WriteChunk(
		Stream output,
		string type,
		ReadOnlySpan<byte> data
	) {
		Span<byte> length = stackalloc byte[4];
		BinaryPrimitives.WriteInt32BigEndian( length, data.Length );
		output.Write( length );

		Span<byte> typeBytes = stackalloc byte[4];
		_ = Encoding.ASCII.GetBytes( type, typeBytes );
		output.Write( typeBytes );
		output.Write( data );

		uint crc = PngCrc.Update( 0xFFFFFFFFU, typeBytes );
		crc = PngCrc.Update( crc, data ) ^ 0xFFFFFFFFU;

		Span<byte> crcBytes = stackalloc byte[4];
		BinaryPrimitives.WriteUInt32BigEndian( crcBytes, crc );
		output.Write( crcBytes );
	}
}
