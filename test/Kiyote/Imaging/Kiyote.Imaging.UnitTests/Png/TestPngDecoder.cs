using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;

namespace Kiyote.Imaging.Png.UnitTests;

[ExcludeFromCodeCoverage]
internal sealed record PngChunk(
	string Type,
	byte[] Data,
	bool CrcValid
);

[ExcludeFromCodeCoverage]
internal sealed record DecodedPng(
	int Width,
	int Height,
	byte BitDepth,
	byte ColourType,
	byte CompressionMethod,
	byte FilterMethod,
	byte InterlaceMethod,
	byte[] Pixels,
	IReadOnlyList<PngChunk> Chunks
) {
	public (byte R, byte G, byte B, byte A) GetPixel(
		int x,
		int y
	) {
		int offset = ( ( y * Width ) + x ) * 4;
		return (Pixels[offset], Pixels[offset + 1], Pixels[offset + 2], Pixels[offset + 3]);
	}
}

[ExcludeFromCodeCoverage]
internal static class TestPngDecoder {

	private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];

	public static DecodedPng Read(
		byte[] bytes
	) {
		Assert.That( bytes.AsSpan( 0, 8 ).SequenceEqual( _signature ), Is.True, "PNG signature is missing." );

		List<PngChunk> chunks = [];
		using MemoryStream compressed = new MemoryStream();
		int width = 0;
		int height = 0;
		byte bitDepth = 0;
		byte colourType = 0;
		byte compressionMethod = 0;
		byte filterMethod = 0;
		byte interlaceMethod = 0;

		int position = 8;
		while( position < bytes.Length ) {
			int length = BinaryPrimitives.ReadInt32BigEndian( bytes.AsSpan( position, 4 ) );
			string type = Encoding.ASCII.GetString( bytes, position + 4, 4 );
			byte[] data = bytes.AsSpan( position + 8, length ).ToArray();
			uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian( bytes.AsSpan( position + 8 + length, 4 ) );
			uint actualCrc = Crc32( bytes.AsSpan( position + 4, 4 + length ) );

			chunks.Add( new PngChunk( type, data, expectedCrc == actualCrc ) );

			if( type == "IHDR" ) {
				width = BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 0, 4 ) );
				height = BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 4, 4 ) );
				bitDepth = data[8];
				colourType = data[9];
				compressionMethod = data[10];
				filterMethod = data[11];
				interlaceMethod = data[12];
			} else if( type == "IDAT" ) {
				compressed.Write( data );
			}

			position += 12 + length;
		}

		compressed.Position = 0;
		using MemoryStream inflated = new MemoryStream();
		using( ZLibStream decompressor = new ZLibStream( compressed, CompressionMode.Decompress, leaveOpen: true ) ) {
			decompressor.CopyTo( inflated );
		}

		byte[] raw = inflated.ToArray();
		byte[] pixels = new byte[width * height * 4];
		int stride = ( width * 4 ) + 1;
		for( int y = 0; y < height; y++ ) {
			Assert.That( raw[y * stride], Is.Zero, $"Unexpected filter type on scanline {y}." );
			raw.AsSpan( ( y * stride ) + 1, width * 4 ).CopyTo( pixels.AsSpan( y * width * 4 ) );
		}

		return new DecodedPng(
			width,
			height,
			bitDepth,
			colourType,
			compressionMethod,
			filterMethod,
			interlaceMethod,
			pixels,
			chunks
		);
	}

	private static uint Crc32(
		ReadOnlySpan<byte> data
	) {
		uint crc = 0xFFFFFFFFU;
		for( int i = 0; i < data.Length; i++ ) {
			crc ^= data[i];
			for( int k = 0; k < 8; k++ ) {
				crc = ( crc & 1 ) == 1 ? 0xEDB88320U ^ ( crc >> 1 ) : crc >> 1;
			}
		}
		return crc ^ 0xFFFFFFFFU;
	}
}
