using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;

namespace Kiyote.Imaging.Png.UnitTests;

[ExcludeFromCodeCoverage]
internal sealed record ApngChunk(
	string Type,
	byte[] Data,
	bool CrcValid
);

[ExcludeFromCodeCoverage]
internal sealed record ApngFrameControl(
	uint SequenceNumber,
	int Width,
	int Height,
	int XOffset,
	int YOffset,
	ushort DelayNum,
	ushort DelayDen,
	byte DisposeOp,
	byte BlendOp
);

[ExcludeFromCodeCoverage]
internal sealed record ApngFrame(
	ApngFrameControl Control,
	byte[] Pixels
) {
	public (byte R, byte G, byte B, byte A) GetPixel(
		int x,
		int y
	) {
		int offset = ( ( ( y * Control.Width ) + x ) * 4 );
		return (Pixels[offset], Pixels[offset + 1], Pixels[offset + 2], Pixels[offset + 3]);
	}
}

[ExcludeFromCodeCoverage]
internal sealed record DecodedApng(
	int Width,
	int Height,
	byte BitDepth,
	byte ColourType,
	int NumFrames,
	int NumPlays,
	IReadOnlyList<ApngFrame> Frames,
	IReadOnlyList<ApngChunk> Chunks
);

[ExcludeFromCodeCoverage]
internal static class TestApngDecoder {

	private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];

	public static DecodedApng Read(
		byte[] bytes
	) {
		Assert.That( bytes.AsSpan( 0, 8 ).SequenceEqual( _signature ), Is.True, "PNG signature is missing." );

		List<ApngChunk> chunks = [];
		int width = 0;
		int height = 0;
		byte bitDepth = 0;
		byte colourType = 0;
		int numFrames = 0;
		int numPlays = 0;
		List<ApngFrame> frames = [];
		ApngFrameControl? currentControl = null;
		MemoryStream? currentData = null;

		void FinishFrame() {
			if( currentControl is not null ) {
				byte[] pixels = DecodeFrame( currentData!.ToArray(), currentControl.Width, currentControl.Height );
				frames.Add( new ApngFrame( currentControl, pixels ) );
			}
		}

		int position = 8;
		while( position < bytes.Length ) {
			int length = BinaryPrimitives.ReadInt32BigEndian( bytes.AsSpan( position, 4 ) );
			string type = Encoding.ASCII.GetString( bytes, position + 4, 4 );
			byte[] data = bytes.AsSpan( position + 8, length ).ToArray();
			uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian( bytes.AsSpan( position + 8 + length, 4 ) );
			uint actualCrc = Crc32( bytes.AsSpan( position + 4, 4 + length ) );

			chunks.Add( new ApngChunk( type, data, expectedCrc == actualCrc ) );

			switch( type ) {
				case "IHDR":
					width = BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 0, 4 ) );
					height = BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 4, 4 ) );
					bitDepth = data[8];
					colourType = data[9];
					break;
				case "acTL":
					numFrames = BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 0, 4 ) );
					numPlays = BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 4, 4 ) );
					break;
				case "fcTL":
					FinishFrame();
					currentControl = new ApngFrameControl(
						BinaryPrimitives.ReadUInt32BigEndian( data.AsSpan( 0, 4 ) ),
						BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 4, 4 ) ),
						BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 8, 4 ) ),
						BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 12, 4 ) ),
						BinaryPrimitives.ReadInt32BigEndian( data.AsSpan( 16, 4 ) ),
						BinaryPrimitives.ReadUInt16BigEndian( data.AsSpan( 20, 2 ) ),
						BinaryPrimitives.ReadUInt16BigEndian( data.AsSpan( 22, 2 ) ),
						data[24],
						data[25]
					);
					currentData = new MemoryStream();
					break;
				case "IDAT":
					currentData!.Write( data );
					break;
				case "fdAT":
					currentData!.Write( data.AsSpan( 4 ) );
					break;
				case "IEND":
					position = bytes.Length;
					continue;
			}

			position += 12 + length;
		}

		FinishFrame();

		return new DecodedApng(
			width,
			height,
			bitDepth,
			colourType,
			numFrames,
			numPlays,
			frames,
			chunks
		);
	}

	private static byte[] DecodeFrame(
		byte[] compressed,
		int width,
		int height
	) {
		using MemoryStream source = new MemoryStream( compressed );
		using MemoryStream inflated = new MemoryStream();
		using( ZLibStream decompressor = new ZLibStream( source, CompressionMode.Decompress, leaveOpen: true ) ) {
			decompressor.CopyTo( inflated );
		}

		byte[] raw = inflated.ToArray();
		byte[] pixels = new byte[width * height * 4];
		int stride = ( width * 4 ) + 1;
		for( int y = 0; y < height; y++ ) {
			Assert.That( raw[y * stride], Is.Zero, $"Unexpected filter type on scanline {y}." );
			raw.AsSpan( ( y * stride ) + 1, width * 4 ).CopyTo( pixels.AsSpan( y * width * 4 ) );
		}

		return pixels;
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
