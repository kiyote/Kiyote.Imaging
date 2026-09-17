using System.IO.Abstractions;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Gif;

internal static class GifChunkWriter {

	private static readonly byte[] _signature = "GIF89a"u8.ToArray();

	public const int BlackAndWhiteMinCodeSize = 2;
	public const int GrayscaleMinCodeSize = 8;

	public static void ThrowIfPixelTypeNotSupported<T>() {
		if( typeof( T ) != typeof( bool )
			&& typeof( T ) != typeof( byte )
		) {
			throw new NotSupportedException( "The pixel type is not supported. Supported types are: bool, byte." );
		}
	}

	public static void WriteSignature(
		Stream output
	) {
		output.Write( _signature );
	}

	public static void WriteLogicalScreenDescriptor(
		Stream output,
		int width,
		int height,
		int colorTableSizeExponent
	) {
		Span<byte> descriptor = stackalloc byte[7];
		BitConverter.TryWriteBytes( descriptor[..2], (ushort)width );
		BitConverter.TryWriteBytes( descriptor.Slice( 2, 2 ), (ushort)height );
		descriptor[4] = (byte)( 0b1000_0000 | colorTableSizeExponent );
		descriptor[5] = 0; // Background color index
		descriptor[6] = 0; // Pixel aspect ratio
		output.Write( descriptor );
	}

	public static byte[] GetPalette<T>() {
		if( typeof( T ) == typeof( bool ) ) {
			return [0, 0, 0, 255, 255, 255];
		}

		byte[] palette = new byte[256 * 3];
		for( int i = 0; i < 256; i++ ) {
			palette[i * 3] = (byte)i;
			palette[( i * 3 ) + 1] = (byte)i;
			palette[( i * 3 ) + 2] = (byte)i;
		}
		return palette;
	}

	public static int GetColorTableSizeExponent<T>() {
		return typeof( T ) == typeof( bool ) ? 0 : 7;
	}

	public static int GetMinCodeSize<T>() {
		return typeof( T ) == typeof( bool ) ? BlackAndWhiteMinCodeSize : GrayscaleMinCodeSize;
	}

	public static byte[] GetIndices<T>(
		IBuffer<T> pixels,
		int width,
		int height
	) {
		byte[] indices = new byte[width * height];

		if( typeof( T ) == typeof( bool ) ) {
			IBuffer<bool> source = (IBuffer<bool>)pixels;
			for( int y = 0; y < height; y++ ) {
				ReadOnlySpan<bool> row = source.GetRowSpan( y );
				Span<byte> destination = indices.AsSpan( y * width, width );
				for( int x = 0; x < width; x++ ) {
					destination[x] = row[x] ? (byte)1 : (byte)0;
				}
			}
			return indices;
		}

		IBuffer<byte> greyscaleSource = (IBuffer<byte>)pixels;
		for( int y = 0; y < height; y++ ) {
			ReadOnlySpan<byte> row = greyscaleSource.GetRowSpan( y );
			row.CopyTo( indices.AsSpan( y * width, width ) );
		}
		return indices;
	}

	public static void WriteImageDescriptor(
		Stream output,
		int width,
		int height
	) {
		Span<byte> descriptor = stackalloc byte[10];
		descriptor[0] = 0x2C;
		BitConverter.TryWriteBytes( descriptor.Slice( 1, 2 ), (ushort)0 ); // Left
		BitConverter.TryWriteBytes( descriptor.Slice( 3, 2 ), (ushort)0 ); // Top
		BitConverter.TryWriteBytes( descriptor.Slice( 5, 2 ), (ushort)width );
		BitConverter.TryWriteBytes( descriptor.Slice( 7, 2 ), (ushort)height );
		descriptor[9] = 0; // No local color table, not interlaced
		output.Write( descriptor );
	}

	public static void WriteImageData(
		Stream output,
		ReadOnlySpan<byte> indices,
		int minCodeSize
	) {
		output.WriteByte( (byte)minCodeSize );
		byte[] compressed = GifLzw.Encode( indices, minCodeSize );
		WriteSubBlocks( output, compressed );
	}

	public static void WriteSubBlocks(
		Stream output,
		ReadOnlySpan<byte> data
	) {
		int position = 0;
		while( position < data.Length ) {
			int chunkSize = Math.Min( 255, data.Length - position );
			output.WriteByte( (byte)chunkSize );
			output.Write( data.Slice( position, chunkSize ) );
			position += chunkSize;
		}
		output.WriteByte( 0 );
	}

	public static void WriteGraphicControlExtension(
		Stream output,
		ushort delayCentiseconds
	) {
		Span<byte> extension = stackalloc byte[8];
		extension[0] = 0x21;
		extension[1] = 0xF9;
		extension[2] = 4; // Block size
		extension[3] = 0b0000_0000; // Disposal method: none, no user input, no transparency
		BitConverter.TryWriteBytes( extension.Slice( 4, 2 ), delayCentiseconds );
		extension[6] = 0; // Transparent color index
		extension[7] = 0; // Block terminator
		output.Write( extension );
	}

	public static void WriteNetscapeLoopExtension(
		Stream output,
		int loopCount
	) {
		Span<byte> extension = stackalloc byte[19];
		extension[0] = 0x21;
		extension[1] = 0xFF;
		extension[2] = 11; // Block size
		"NETSCAPE2.0"u8.CopyTo( extension[3..] );
		extension[14] = 3; // Sub-block size
		extension[15] = 1;
		BitConverter.TryWriteBytes( extension.Slice( 16, 2 ), (ushort)loopCount );
		extension[18] = 0; // Block terminator
		output.Write( extension );
	}

	public static void WriteTrailer(
		Stream output
	) {
		output.WriteByte( 0x3B );
	}
}
