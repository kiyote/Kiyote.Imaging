using System.Buffers;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Gif;

/// <summary>
/// Shared, allocation-conscious GIF encoding logic used by both
/// <see cref="GifImageWriter"/> (single images) and
/// <see cref="GifAnimationBuilder"/> (animation frames). LZW compression and
/// its supporting dictionary are performed using pooled arrays so that
/// writing a GIF image, whether standalone or as part of an animation, does
/// not require materializing the compressed output or pixel indices in a
/// dedicated allocation.
/// </summary>
internal abstract class GifWriterBase {

	protected const int BlackAndWhiteMinCodeSize = 2;
	protected const int GrayscaleMinCodeSize = 8;
	private const int MaxCodeSize = 12;
	private const int MaxDictionarySize = 4096;
	// A power-of-two capacity comfortably larger than MaxDictionarySize keeps
	// the open-addressing load factor low (< 50%) so lookups/inserts stay fast.
	private const int DictionaryCapacity = 8192;
	private const int SubBlockMaxSize = 255;
	// With bitCount always < 8 before a code is written, and codes never
	// exceeding MaxCodeSize (12) bits, a single WriteCode call can flush
	// at most floor( (7 + 12) / 8 ) = 2 whole bytes into the block buffer.
	private const int MaxBytesPerCode = 2;

	private static readonly byte[] _signature = "GIF89a"u8.ToArray();
	private static readonly byte[] _blackAndWhitePalette = [0, 0, 0, 255, 255, 255];

	protected static void ThrowIfPixelTypeNotSupported<T>() {
		if( typeof( T ) != typeof( bool )
			&& typeof( T ) != typeof( byte )
		) {
			throw new NotSupportedException( "The pixel type is not supported. Supported types are: bool, byte." );
		}
	}

	protected static int GetMinCodeSize<T>() {
		return typeof( T ) == typeof( bool ) ? BlackAndWhiteMinCodeSize : GrayscaleMinCodeSize;
	}

	protected static int GetColorTableSizeExponent<T>() {
		return typeof( T ) == typeof( bool ) ? 0 : 7;
	}

	protected static void WriteSignature(
		Stream output
	) {
		output.Write( _signature );
	}

	protected static void WriteLogicalScreenDescriptor(
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

	protected static void WritePalette<T>(
		Stream output
	) {
		if( typeof( T ) == typeof( bool ) ) {
			output.Write( _blackAndWhitePalette );
			return;
		}

		Span<byte> palette = stackalloc byte[256 * 3];
		for( int i = 0; i < 256; i++ ) {
			palette[i * 3] = (byte)i;
			palette[( i * 3 ) + 1] = (byte)i;
			palette[( i * 3 ) + 2] = (byte)i;
		}
		output.Write( palette );
	}

	protected static void WriteImageDescriptor(
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

	protected static void WriteGraphicControlExtension(
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

	protected static void WriteNetscapeLoopExtension(
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

	protected static void WriteTrailer(
		Stream output
	) {
		output.WriteByte( 0x3B );
	}

	protected static void WriteImageData<T>(
		Stream output,
		IBuffer<T> pixels,
		int width,
		int height,
		int minCodeSize
	) {
		output.WriteByte( (byte)minCodeSize );

		byte[] rented = ArrayPool<byte>.Shared.Rent( SubBlockMaxSize + MaxBytesPerCode );
		try {
			LzwBlockWriter writer = new LzwBlockWriter( output, rented );
			WriteCompressedData( ref writer, pixels, width, height, minCodeSize );
		} finally {
			ArrayPool<byte>.Shared.Return( rented );
		}
	}

	private static void WriteCompressedData<T>(
		ref LzwBlockWriter writer,
		IBuffer<T> pixels,
		int width,
		int height,
		int minCodeSize
	) {
		int clearCode = 1 << minCodeSize;
		int endCode = clearCode + 1;
		int codeSize = minCodeSize + 1;
		int nextCode = endCode + 1;

		int[] dictionaryKeys = ArrayPool<int>.Shared.Rent( DictionaryCapacity );
		int[] dictionaryCodes = ArrayPool<int>.Shared.Rent( DictionaryCapacity );
		try {
			LzwDictionary dictionary = new LzwDictionary( dictionaryKeys, dictionaryCodes );
			dictionary.Clear();
			writer.WriteCode( clearCode, codeSize );

			int prefix = -1;
			bool isBoolean = typeof( T ) == typeof( bool );
			IBuffer<bool>? booleanSource = isBoolean ? (IBuffer<bool>)pixels : null;
			IBuffer<byte>? greyscaleSource = isBoolean ? null : (IBuffer<byte>)pixels;

			for( int y = 0; y < height; y++ ) {
				ReadOnlySpan<bool> booleanRow = isBoolean ? booleanSource!.GetRowSpan( y ) : default;
				ReadOnlySpan<byte> greyscaleRow = isBoolean ? default : greyscaleSource!.GetRowSpan( y );

				for( int x = 0; x < width; x++ ) {
					byte value = isBoolean
						? ( booleanRow[x] ? (byte)1 : (byte)0 )
						: greyscaleRow[x];

					if( prefix == -1 ) {
						prefix = value;
						continue;
					}

					if( dictionary.TryGetValue( prefix, value, out int existingCode ) ) {
						prefix = existingCode;
						continue;
					}

					writer.WriteCode( prefix, codeSize );

					if( nextCode < MaxDictionarySize ) {
						dictionary.Add( prefix, value, nextCode );
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
			}

			if( prefix != -1 ) {
				writer.WriteCode( prefix, codeSize );
			}
			writer.WriteCode( endCode, codeSize );

			writer.Finish();
		} finally {
			ArrayPool<int>.Shared.Return( dictionaryKeys );
			ArrayPool<int>.Shared.Return( dictionaryCodes );
		}
	}

	/// <summary>
	/// Accumulates LZW compressed bits into a caller-owned rented buffer,
	/// flushing complete 255-byte GIF sub-blocks to the destination stream as
	/// soon as enough data has accumulated, rather than materializing the
	/// entire compressed output in memory. Declared as a ref struct so it
	/// lives on the stack alongside its state instead of requiring its own
	/// heap allocation.
	/// </summary>
	private ref struct LzwBlockWriter {

		private readonly Stream _output;
		private readonly byte[] _buffer;
		private int _length;
		private int _bitBuffer;
		private int _bitCount;

		public LzwBlockWriter(
			Stream output,
			byte[] buffer
		) {
			_output = output;
			_buffer = buffer;
			_length = 0;
			_bitBuffer = 0;
			_bitCount = 0;
		}

		public void WriteCode(
			int code,
			int codeSize
		) {
			_bitBuffer |= code << _bitCount;
			_bitCount += codeSize;
			while( _bitCount >= 8 ) {
				_buffer[_length++] = (byte)( _bitBuffer & 0xFF );
				_bitBuffer >>= 8;
				_bitCount -= 8;
			}

			if( _length >= SubBlockMaxSize ) {
				Flush();
			}
		}

		public void Finish() {
			if( _bitCount > 0 ) {
				_buffer[_length++] = (byte)( _bitBuffer & 0xFF );
				_bitBuffer = 0;
				_bitCount = 0;
				if( _length >= SubBlockMaxSize ) {
					Flush();
				}
			}

			Flush();
			_output.WriteByte( 0 ); // Block terminator
		}

		private void Flush() {
			if( _length == 0 ) {
				return;
			}

			int chunkSize = Math.Min( SubBlockMaxSize, _length );
			_output.WriteByte( (byte)chunkSize );
			_output.Write( _buffer, 0, chunkSize );

			int remaining = _length - chunkSize;
			if( remaining > 0 ) {
				Buffer.BlockCopy( _buffer, chunkSize, _buffer, 0, remaining );
			}
			_length = remaining;
		}
	}

	/// <summary>
	/// Maps (prefix, value) LZW code pairs to their assigned dictionary code
	/// using open addressing over caller-owned pooled arrays, avoiding the
	/// managed allocations a <see cref="Dictionary{TKey,TValue}"/> would
	/// otherwise require for every image written.
	/// </summary>
	private readonly ref struct LzwDictionary {

		private readonly int[] _keys;
		private readonly int[] _codes;

		public LzwDictionary(
			int[] keys,
			int[] codes
		) {
			_keys = keys;
			_codes = codes;
		}

		public void Clear() {
			Array.Fill( _keys, -1, 0, DictionaryCapacity );
		}

		public bool TryGetValue(
			int prefix,
			byte value,
			out int code
		) {
			int key = ( prefix << 8 ) | value;
			int index = Hash( key );
			while( true ) {
				int existingKey = _keys[index];
				if( existingKey == -1 ) {
					code = 0;
					return false;
				}
				if( existingKey == key ) {
					code = _codes[index];
					return true;
				}
				index = ( index + 1 ) & ( DictionaryCapacity - 1 );
			}
		}

		public void Add(
			int prefix,
			byte value,
			int code
		) {
			int key = ( prefix << 8 ) | value;
			int index = Hash( key );
			while( _keys[index] != -1 ) {
				index = ( index + 1 ) & ( DictionaryCapacity - 1 );
			}
			_keys[index] = key;
			_codes[index] = code;
		}

		private static int Hash(
			int key
		) {
			uint h = (uint)key * 2654435761u;
			return (int)( h >> 19 ) & ( DictionaryCapacity - 1 );
		}
	}
}
