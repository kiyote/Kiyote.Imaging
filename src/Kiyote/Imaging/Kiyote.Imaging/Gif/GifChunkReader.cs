namespace Kiyote.Imaging.Gif;

internal readonly struct GifFrame {
	public required int Width { get; init; }
	public required int Height { get; init; }
	public required byte[] Palette { get; init; }
	public required byte[] Indices { get; init; }
	public required ushort DelayCentiseconds { get; init; }
}

internal static class GifChunkReader {

	public static GifFrame ReadFirstFrame(
		byte[] bytes
	) {
		List<GifFrame> frames = ReadFrames( bytes, stopAfterFirst: true );
		if( frames.Count == 0 ) {
			throw new InvalidDataException( "The GIF does not contain any image data." );
		}
		return frames[0];
	}

	public static List<GifFrame> ReadAllFrames(
		byte[] bytes
	) {
		return ReadFrames( bytes, stopAfterFirst: false );
	}

	private static List<GifFrame> ReadFrames(
		byte[] bytes,
		bool stopAfterFirst
	) {
		if( bytes.Length < 13
			|| bytes[0] != (byte)'G'
			|| bytes[1] != (byte)'I'
			|| bytes[2] != (byte)'F'
		) {
			throw new InvalidDataException( "The file is not a GIF image." );
		}

		int width = bytes[6] | ( bytes[7] << 8 );
		int height = bytes[8] | ( bytes[9] << 8 );
		byte packed = bytes[10];
		int position = 13;

		byte[] globalPalette = [];
		if( ( packed & 0b1000_0000 ) != 0 ) {
			int globalColorTableSize = 1 << ( ( packed & 0b0000_0111 ) + 1 );
			globalPalette = bytes.AsSpan( position, globalColorTableSize * 3 ).ToArray();
			position += globalColorTableSize * 3;
		}

		List<GifFrame> frames = new List<GifFrame>();
		ushort delayCentiseconds = 0;

		while( position < bytes.Length ) {
			byte introducer = bytes[position];
			if( introducer == 0x21 ) {
				position++;
				byte label = bytes[position];
				position++;
				if( label == 0xF9 && bytes[position] >= 4 ) {
					delayCentiseconds = (ushort)( bytes[position + 2] | ( bytes[position + 3] << 8 ) );
				}
				position = SkipSubBlocks( bytes, position );
			} else if( introducer == 0x2C ) {
				position++;
				int frameWidth = bytes[position + 4] | ( bytes[position + 5] << 8 );
				int frameHeight = bytes[position + 6] | ( bytes[position + 7] << 8 );
				byte imagePacked = bytes[position + 8];
				position += 9;

				byte[] palette = globalPalette;
				if( ( imagePacked & 0b1000_0000 ) != 0 ) {
					int localColorTableSize = 1 << ( ( imagePacked & 0b0000_0111 ) + 1 );
					palette = bytes.AsSpan( position, localColorTableSize * 3 ).ToArray();
					position += localColorTableSize * 3;
				}

				int minCodeSize = bytes[position];
				position++;

				(byte[] compressed, int nextPosition) = ReadSubBlocks( bytes, position );
				position = nextPosition;

				byte[] indices = GifLzw.Decode( compressed, minCodeSize, frameWidth * frameHeight );

				frames.Add( new GifFrame {
					Width = frameWidth,
					Height = frameHeight,
					Palette = palette,
					Indices = indices,
					DelayCentiseconds = delayCentiseconds
				} );

				delayCentiseconds = 0;

				if( stopAfterFirst ) {
					break;
				}
			} else if( introducer == 0x3B ) {
				break;
			} else {
				position++;
			}
		}

		return frames;
	}

	private static int SkipSubBlocks(
		byte[] bytes,
		int position
	) {
		while( position < bytes.Length ) {
			byte length = bytes[position];
			position++;
			if( length == 0 ) {
				break;
			}
			position += length;
		}
		return position;
	}

	private static (byte[] Data, int Position) ReadSubBlocks(
		byte[] bytes,
		int position
	) {
		using MemoryStream data = new MemoryStream();
		while( position < bytes.Length ) {
			byte length = bytes[position];
			position++;
			if( length == 0 ) {
				break;
			}
			data.Write( bytes, position, length );
			position += length;
		}
		return ( data.ToArray(), position );
	}
}
