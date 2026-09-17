namespace Kiyote.Imaging.Gif;

internal static class GifLzw {

	private const int MaxCodeSize = 12;
	private const int MaxDictionarySize = 4096;

	public static byte[] Encode(
		ReadOnlySpan<byte> indices,
		int minCodeSize
	) {
		int clearCode = 1 << minCodeSize;
		int endCode = clearCode + 1;
		int codeSize = minCodeSize + 1;
		int nextCode = endCode + 1;

		Dictionary<(int Prefix, byte Value), int> dictionary = new Dictionary<(int, byte), int>();
		BitWriter writer = new BitWriter();
		writer.WriteCode( clearCode, codeSize );

		int prefix = -1;
		foreach( byte value in indices ) {
			if( prefix == -1 ) {
				prefix = value;
				continue;
			}

			if( dictionary.TryGetValue( ( prefix, value ), out int existingCode ) ) {
				prefix = existingCode;
				continue;
			}

			writer.WriteCode( prefix, codeSize );

			if( nextCode < MaxDictionarySize ) {
				dictionary[( prefix, value )] = nextCode;
				nextCode++;
				if( nextCode > ( 1 << codeSize ) && codeSize < MaxCodeSize ) {
					codeSize++;
				}
			} else {
				writer.WriteCode( clearCode, codeSize );
				dictionary.Clear();
				nextCode = endCode + 1;
				codeSize = minCodeSize + 1;
			}

			prefix = value;
		}

		if( prefix != -1 ) {
			writer.WriteCode( prefix, codeSize );
		}
		writer.WriteCode( endCode, codeSize );

		return writer.ToArray();
	}

	public static byte[] Decode(
		ReadOnlySpan<byte> data,
		int minCodeSize,
		int expectedLength
	) {
		int clearCode = 1 << minCodeSize;
		int endCode = clearCode + 1;
		int codeSize = minCodeSize + 1;

		List<byte[]> dictionary = CreateInitialDictionary( clearCode );
		int nextCode = endCode + 1;

		BitReader reader = new BitReader( data );
		List<byte> output = new List<byte>( expectedLength );
		byte[]? previous = null;

		while( true ) {
			int code = reader.ReadCode( codeSize );
			if( code < 0 || code == endCode ) {
				break;
			}

			if( code == clearCode ) {
				dictionary = CreateInitialDictionary( clearCode );
				nextCode = endCode + 1;
				codeSize = minCodeSize + 1;
				previous = null;
				continue;
			}

			byte[] entry;
			if( code < dictionary.Count ) {
				entry = dictionary[code];
			} else if( code == dictionary.Count && previous is not null ) {
				entry = [.. previous, previous[0]];
			} else {
				throw new InvalidDataException( "The GIF image data contains an invalid LZW code." );
			}

			output.AddRange( entry );

			if( previous is not null && nextCode < MaxDictionarySize ) {
				dictionary.Add( [.. previous, entry[0]] );
				nextCode++;
				if( nextCode == ( 1 << codeSize ) && codeSize < MaxCodeSize ) {
					codeSize++;
				}
			}

			previous = entry;
		}

		return [.. output];
	}

	private static List<byte[]> CreateInitialDictionary(
		int clearCode
	) {
		List<byte[]> dictionary = new List<byte[]>( MaxDictionarySize );
		for( int i = 0; i < clearCode; i++ ) {
			dictionary.Add( [(byte)i] );
		}
		// Reserve the clear and end-of-information codes.
		dictionary.Add( [] );
		dictionary.Add( [] );
		return dictionary;
	}

	private sealed class BitWriter {

		private readonly List<byte> _bytes = new List<byte>();
		private int _bitBuffer;
		private int _bitCount;

		public void WriteCode(
			int code,
			int codeSize
		) {
			_bitBuffer |= code << _bitCount;
			_bitCount += codeSize;
			while( _bitCount >= 8 ) {
				_bytes.Add( (byte)( _bitBuffer & 0xFF ) );
				_bitBuffer >>= 8;
				_bitCount -= 8;
			}
		}

		public byte[] ToArray() {
			if( _bitCount > 0 ) {
				_bytes.Add( (byte)( _bitBuffer & 0xFF ) );
				_bitBuffer = 0;
				_bitCount = 0;
			}
			return [.. _bytes];
		}
	}

	private ref struct BitReader {

		private readonly ReadOnlySpan<byte> _data;
		private int _bytePosition;
		private int _bitBuffer;
		private int _bitCount;

		public BitReader(
			ReadOnlySpan<byte> data
		) {
			_data = data;
			_bytePosition = 0;
			_bitBuffer = 0;
			_bitCount = 0;
		}

		public int ReadCode(
			int codeSize
		) {
			while( _bitCount < codeSize ) {
				if( _bytePosition >= _data.Length ) {
					return -1;
				}
				_bitBuffer |= _data[_bytePosition] << _bitCount;
				_bytePosition++;
				_bitCount += 8;
			}

			int code = _bitBuffer & ( ( 1 << codeSize ) - 1 );
			_bitBuffer >>= codeSize;
			_bitCount -= codeSize;
			return code;
		}
	}
}
