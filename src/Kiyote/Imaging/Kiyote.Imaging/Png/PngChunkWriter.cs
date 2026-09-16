using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Png;

internal static class PngChunkWriter {

	public const byte BitDepth = 8;
	public const byte ColourTypeRgba = 6;
	public const int BytesPerPixel = 4;

	public static void ThrowIfPixelTypeNotSupported<T>() {
		if( typeof( T ) != typeof( uint )
			&& typeof( T ) != typeof( int )
			&& typeof( T ) != typeof( bool )
			&& typeof( T ) != typeof( byte )
		) {
			throw new NotSupportedException( "The pixel type is not supported. Supported types are: uint, int, bool, byte." );
		}
	}

	public static void WriteChunk(
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

	public static void WriteHeader(
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

	public static byte[] CompressFrame<T>(
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

		return raw.ToArray();
	}

	public static void FillScanline<T>(
		IBuffer<T> pixels,
		int y,
		int width,
		Span<byte> destination
	) {
		if( typeof( T ) == typeof( bool ) ) {
			ReadOnlySpan<bool> source = ( (IBuffer<bool>)pixels ).GetRowSpan( y );
			for( int x = 0; x < width; x++ ) {
				byte value = source[x] ? byte.MaxValue : byte.MinValue;
				int offset = x * BytesPerPixel;
				destination[offset] = value;
				destination[offset + 1] = value;
				destination[offset + 2] = value;
				destination[offset + 3] = byte.MaxValue;
			}
			return;
		}

		if( typeof( T ) == typeof( byte ) ) {
			ReadOnlySpan<byte> greyscaleSource = ( (IBuffer<byte>)pixels ).GetRowSpan( y );
			for( int x = 0; x < width; x++ ) {
				byte value = greyscaleSource[x];
				int offset = x * BytesPerPixel;
				destination[offset] = value;
				destination[offset + 1] = value;
				destination[offset + 2] = value;
				destination[offset + 3] = byte.MaxValue;
			}
			return;
		}

		if( typeof( T ) == typeof( int ) ) {
			ReadOnlySpan<int> intSource = ( (IBuffer<int>)pixels ).GetRowSpan( y );
			for( int x = 0; x < width; x++ ) {
				BinaryPrimitives.WriteUInt32BigEndian(
					destination.Slice( x * BytesPerPixel, BytesPerPixel ),
					unchecked( (uint)intSource[x] )
				);
			}
			return;
		}

		ReadOnlySpan<uint> uintSource = ( (IBuffer<uint>)pixels ).GetRowSpan( y );
		for( int x = 0; x < width; x++ ) {
			BinaryPrimitives.WriteUInt32BigEndian(
				destination.Slice( x * BytesPerPixel, BytesPerPixel ),
				uintSource[x]
			);
		}
	}
}
