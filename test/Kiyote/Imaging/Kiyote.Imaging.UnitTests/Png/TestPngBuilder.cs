using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;

namespace Kiyote.Imaging.Png.UnitTests;

/// <summary>
/// Builds PNG files with arbitrary headers, colour types and scanline filters so
/// the reader can be exercised against images it cannot itself produce.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class TestPngBuilder {

	private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];
	private static readonly uint[] _crcTable = CreateCrcTable();

	private readonly int _width;
	private readonly int _height;
	private readonly byte _colourType;
	private readonly byte[] _samples;
	private readonly byte[] _filters;

	public TestPngBuilder(
		int width,
		int height,
		byte colourType
	) {
		_width = width;
		_height = height;
		_colourType = colourType;
		Channels = colourType switch {
			0 => 1,
			2 => 3,
			4 => 2,
			_ => 4
		};
		_samples = new byte[width * height * Channels];
		_filters = new byte[height];
		RowCount = height;
	}

	public int Channels { get; }

	public byte BitDepth { get; set; } = 8;

	public byte CompressionMethod { get; set; }

	public byte FilterMethod { get; set; }

	public byte InterlaceMethod { get; set; }

	public bool OmitHeader { get; set; }

	public int HeaderLength { get; set; } = 13;

	/// <summary>
	/// The number of scanlines actually emitted, allowing truncated images to be built.
	/// </summary>
	public int RowCount { get; set; }

	public bool AppendMalformedChunk { get; set; }

	public void SetFilter(
		int y,
		byte filter
	) {
		_filters[y] = filter;
	}

	public void SetSample(
		int x,
		int y,
		int channel,
		byte value
	) {
		_samples[( ( ( y * _width ) + x ) * Channels ) + channel] = value;
	}

	public byte GetSample(
		int x,
		int y,
		int channel
	) {
		return _samples[( ( ( y * _width ) + x ) * Channels ) + channel];
	}

	/// <summary>
	/// Fills every sample with a deterministic, non-uniform pattern so that the
	/// scanline filters produce meaningful differences between rows.
	/// </summary>
	public void FillPattern() {
		for( int y = 0; y < _height; y++ ) {
			for( int x = 0; x < _width; x++ ) {
				for( int c = 0; c < Channels; c++ ) {
					SetSample( x, y, c, unchecked( (byte)( ( y * 37 ) + ( x * 11 ) + ( c * 53 ) + 7 ) ) );
				}
			}
		}
	}

	public byte[] Build() {
		using MemoryStream output = new MemoryStream();
		output.Write( _signature );

		if( !OmitHeader ) {
			byte[] header = new byte[13];
			BinaryPrimitives.WriteInt32BigEndian( header.AsSpan( 0, 4 ), _width );
			BinaryPrimitives.WriteInt32BigEndian( header.AsSpan( 4, 4 ), _height );
			header[8] = BitDepth;
			header[9] = _colourType;
			header[10] = CompressionMethod;
			header[11] = FilterMethod;
			header[12] = InterlaceMethod;
			WriteChunk( output, "IHDR", header.AsSpan( 0, Math.Min( HeaderLength, header.Length ) ) );
		}

		WriteChunk( output, "IDAT", Compress() );

		if( AppendMalformedChunk ) {
			Span<byte> malformed = stackalloc byte[12];
			BinaryPrimitives.WriteInt32BigEndian( malformed[..4], 1_048_576 );
			Encoding.ASCII.GetBytes( "tEXt" ).CopyTo( malformed[4..8] );
			output.Write( malformed );
		}

		WriteChunk( output, "IEND", [] );

		return output.ToArray();
	}

	private byte[] Compress() {
		int stride = _width * Channels;
		byte[] filtered = new byte[RowCount * ( stride + 1 )];
		for( int y = 0; y < RowCount; y++ ) {
			byte filter = _filters[y];
			int destination = y * ( stride + 1 );
			filtered[destination] = filter;
			for( int i = 0; i < stride; i++ ) {
				int raw = _samples[( y * stride ) + i];
				int left = i >= Channels ? _samples[( y * stride ) + i - Channels] : 0;
				int up = y == 0 ? 0 : _samples[( ( y - 1 ) * stride ) + i];
				int upperLeft = y == 0 || i < Channels ? 0 : _samples[( ( y - 1 ) * stride ) + i - Channels];
				int value = filter switch {
					1 => raw - left,
					2 => raw - up,
					3 => raw - ( ( left + up ) / 2 ),
					4 => raw - Paeth( left, up, upperLeft ),
					_ => raw
				};
				filtered[destination + 1 + i] = unchecked( (byte)value );
			}
		}

		using MemoryStream compressed = new MemoryStream();
		using( ZLibStream compressor = new ZLibStream( compressed, CompressionLevel.Optimal, leaveOpen: true ) ) {
			compressor.Write( filtered );
		}
		return compressed.ToArray();
	}

	private static int Paeth(
		int a,
		int b,
		int c
	) {
		int p = a + b - c;
		int pa = Math.Abs( p - a );
		int pb = Math.Abs( p - b );
		int pc = Math.Abs( p - c );
		if( pa <= pb && pa <= pc ) {
			return a;
		}
		return pb <= pc ? b : c;
	}

	private static void WriteChunk(
		Stream output,
		string type,
		ReadOnlySpan<byte> data
	) {
		Span<byte> length = stackalloc byte[4];
		BinaryPrimitives.WriteInt32BigEndian( length, data.Length );
		output.Write( length );

		byte[] typeAndData = new byte[4 + data.Length];
		Encoding.ASCII.GetBytes( type ).CopyTo( typeAndData.AsSpan( 0, 4 ) );
		data.CopyTo( typeAndData.AsSpan( 4 ) );
		output.Write( typeAndData );

		Span<byte> crc = stackalloc byte[4];
		BinaryPrimitives.WriteUInt32BigEndian( crc, Crc( typeAndData ) );
		output.Write( crc );
	}

	private static uint[] CreateCrcTable() {
		uint[] table = new uint[256];
		for( uint n = 0; n < 256; n++ ) {
			uint c = n;
			for( int k = 0; k < 8; k++ ) {
				c = ( c & 1 ) != 0 ? 0xEDB88320U ^ ( c >> 1 ) : c >> 1;
			}
			table[n] = c;
		}
		return table;
	}

	private static uint Crc(
		ReadOnlySpan<byte> data
	) {
		uint c = 0xFFFFFFFFU;
		foreach( byte b in data ) {
			c = _crcTable[( c ^ b ) & 0xFF] ^ ( c >> 8 );
		}
		return c ^ 0xFFFFFFFFU;
	}
}
