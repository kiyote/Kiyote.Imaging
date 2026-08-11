using System.Buffers.Binary;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Text;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Png;

internal sealed class PngReader : IImageReader {

	private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];

	private readonly IBufferFactory _bufferFactory;
	private readonly IFileSystem _fileSystem;

	public PngReader(
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
		if( typeof( T ) != typeof( uint )
			&& typeof( T ) != typeof( int )
			&& typeof( T ) != typeof( bool )
		) {
			throw new NotSupportedException( "The pixel type is not supported. Supported types are: uint, int, bool." );
		}

		byte[] bytes = _fileSystem.File.ReadAllBytes( filePath );

		if( bytes.Length < _signature.Length
			|| !bytes.AsSpan( 0, _signature.Length ).SequenceEqual( _signature )
		) {
			throw new InvalidDataException( "The file is not a PNG image." );
		}

		int width = 0;
		int height = 0;
		int bitDepth = 0;
		int colourType = 0;
		bool seenHeader = false;
		using MemoryStream compressed = new MemoryStream();

		int position = _signature.Length;
		while( position + 12 <= bytes.Length ) {
			int length = BinaryPrimitives.ReadInt32BigEndian( bytes.AsSpan( position, 4 ) );
			if( length < 0 || position + 12 + length > bytes.Length ) {
				throw new InvalidDataException( "The PNG contains a malformed chunk." );
			}

			string type = Encoding.ASCII.GetString( bytes, position + 4, 4 );
			ReadOnlySpan<byte> data = bytes.AsSpan( position + 8, length );
			uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian( bytes.AsSpan( position + 8 + length, 4 ) );
			if( PngCrc.Compute( bytes.AsSpan( position + 4, 4 + length ) ) != expectedCrc ) {
				throw new InvalidDataException( $"The {type} chunk failed its CRC check." );
			}

			if( type == "IHDR" ) {
				if( length != 13 ) {
					throw new InvalidDataException( "The IHDR chunk is malformed." );
				}
				width = BinaryPrimitives.ReadInt32BigEndian( data[..4] );
				height = BinaryPrimitives.ReadInt32BigEndian( data.Slice( 4, 4 ) );
				bitDepth = data[8];
				colourType = data[9];
				if( data[10] != 0 ) {
					throw new NotSupportedException( "Only deflate compressed PNG images are supported." );
				}
				if( data[11] != 0 ) {
					throw new NotSupportedException( "Only the adaptive PNG filter method is supported." );
				}
				if( data[12] != 0 ) {
					throw new NotSupportedException( "Interlaced PNG images are not supported." );
				}
				if( bitDepth != 8 ) {
					throw new NotSupportedException( "Only 8 bit per channel PNG images are supported." );
				}
				if( colourType is not 0 and not 2 and not 4 and not 6 ) {
					throw new NotSupportedException( "Only greyscale and truecolour PNG images are supported." );
				}
				seenHeader = true;
			} else if( type == "IDAT" ) {
				compressed.Write( data );
			} else if( type == "IEND" ) {
				break;
			}

			position += 12 + length;
		}

		if( !seenHeader ) {
			throw new InvalidDataException( "The PNG is missing its IHDR chunk." );
		}

		byte[] pixels = Decode( compressed, width, height, colourType );
		return CreateBuffer<T>( pixels, width, height );
	}

	private static byte[] Decode(
		MemoryStream compressed,
		int width,
		int height,
		int colourType
	) {
		int channels = colourType switch {
			0 => 1,
			2 => 3,
			4 => 2,
			_ => 4
		};
		int stride = width * channels;

		compressed.Position = 0;
		using MemoryStream inflated = new MemoryStream();
		using( ZLibStream decompressor = new ZLibStream( compressed, CompressionMode.Decompress, leaveOpen: true ) ) {
			decompressor.CopyTo( inflated );
		}

		byte[] raw = inflated.GetBuffer();
		if( inflated.Length < (long)( stride + 1 ) * height ) {
			throw new InvalidDataException( "The PNG image data is truncated." );
		}

		byte[] scanlines = new byte[stride * height];
		for( int y = 0; y < height; y++ ) {
			int source = ( y * ( stride + 1 ) ) + 1;
			byte filter = raw[source - 1];
			Span<byte> current = scanlines.AsSpan( y * stride, stride );
			raw.AsSpan( source, stride ).CopyTo( current );
			ReadOnlySpan<byte> previous = y == 0
				? default
				: scanlines.AsSpan( ( y - 1 ) * stride, stride );
			Unfilter( filter, current, previous, channels );
		}

		byte[] pixels = new byte[width * height * 4];
		for( int i = 0; i < width * height; i++ ) {
			int source = i * channels;
			int destination = i * 4;
			switch( colourType ) {
				case 0:
					pixels[destination] = scanlines[source];
					pixels[destination + 1] = scanlines[source];
					pixels[destination + 2] = scanlines[source];
					pixels[destination + 3] = byte.MaxValue;
					break;
				case 2:
					pixels[destination] = scanlines[source];
					pixels[destination + 1] = scanlines[source + 1];
					pixels[destination + 2] = scanlines[source + 2];
					pixels[destination + 3] = byte.MaxValue;
					break;
				case 4:
					pixels[destination] = scanlines[source];
					pixels[destination + 1] = scanlines[source];
					pixels[destination + 2] = scanlines[source];
					pixels[destination + 3] = scanlines[source + 1];
					break;
				default:
					pixels[destination] = scanlines[source];
					pixels[destination + 1] = scanlines[source + 1];
					pixels[destination + 2] = scanlines[source + 2];
					pixels[destination + 3] = scanlines[source + 3];
					break;
			}
		}

		return pixels;
	}

	private static void Unfilter(
		byte filter,
		Span<byte> current,
		ReadOnlySpan<byte> previous,
		int bpp
	) {
		switch( filter ) {
			case 0:
				break;
			case 1:
				for( int i = bpp; i < current.Length; i++ ) {
					current[i] = unchecked( (byte)( current[i] + current[i - bpp] ) );
				}
				break;
			case 2:
				if( !previous.IsEmpty ) {
					for( int i = 0; i < current.Length; i++ ) {
						current[i] = unchecked( (byte)( current[i] + previous[i] ) );
					}
				}
				break;
			case 3:
				for( int i = 0; i < current.Length; i++ ) {
					int left = i >= bpp ? current[i - bpp] : 0;
					int up = previous.IsEmpty ? 0 : previous[i];
					current[i] = unchecked( (byte)( current[i] + ( ( left + up ) / 2 ) ) );
				}
				break;
			case 4:
				for( int i = 0; i < current.Length; i++ ) {
					int left = i >= bpp ? current[i - bpp] : 0;
					int up = previous.IsEmpty ? 0 : previous[i];
					int upperLeft = previous.IsEmpty || i < bpp ? 0 : previous[i - bpp];
					current[i] = unchecked( (byte)( current[i] + Paeth( left, up, upperLeft ) ) );
				}
				break;
			default:
				throw new InvalidDataException( $"Unknown PNG filter type {filter}." );
		}
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

	private IBuffer<T> CreateBuffer<T>(
		byte[] pixels,
		int width,
		int height
	) {
		if( typeof( T ) == typeof( bool ) ) {
			IBuffer<bool> buffer = _bufferFactory.Create( width, height, false );
			for( int y = 0; y < height; y++ ) {
				for( int x = 0; x < width; x++ ) {
					int offset = ( ( y * width ) + x ) * 4;
					buffer[x, y] = pixels[offset] == 0
						&& pixels[offset + 1] == 0
						&& pixels[offset + 2] == 0;
				}
			}
			return (IBuffer<T>)buffer;
		}

		if( typeof( T ) == typeof( int ) ) {
			IBuffer<int> buffer = _bufferFactory.Create( width, height, 0 );
			for( int y = 0; y < height; y++ ) {
				for( int x = 0; x < width; x++ ) {
					buffer[x, y] = unchecked( (int)ToRgba( pixels, width, x, y ) );
				}
			}
			return (IBuffer<T>)buffer;
		}

		IBuffer<uint> pixelBuffer = _bufferFactory.Create( width, height, 0U );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				pixelBuffer[x, y] = ToRgba( pixels, width, x, y );
			}
		}
		return (IBuffer<T>)pixelBuffer;
	}

	private static uint ToRgba(
		byte[] pixels,
		int width,
		int x,
		int y
	) {
		return BinaryPrimitives.ReadUInt32BigEndian( pixels.AsSpan( ( ( y * width ) + x ) * 4, 4 ) );
	}
}
