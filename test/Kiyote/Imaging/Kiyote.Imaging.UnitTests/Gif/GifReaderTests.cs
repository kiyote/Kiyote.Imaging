using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using Kiyote.Buffers;
using Kiyote.Imaging.UnitTests;

namespace Kiyote.Imaging.Gif.UnitTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GifReaderTests {

	private const string FileName = "image.gif";

	private static readonly string _fileFolder = OperatingSystem.IsWindows() ? @"C:\images" : "/images";
	private static readonly string _filePath = Path.Combine( _fileFolder, FileName );

	private MockFileSystem _fileSystem;
	private IImageWriter _writer;
	private IImageReader _reader;

	[SetUp]
	public void Setup() {
		_fileSystem = new MockFileSystem();
		_fileSystem.AddDirectory( _fileFolder );
		_writer = new GifImageWriter( _fileSystem );
		_reader = new GifReader( MockBufferFactory.Create(), _fileSystem );
	}

	[Test]
	public void ReadImage_UnsupportedPixelType_ThrowsNotSupportedException() {
		IBuffer<byte> pixels = MockBuffer.Create<byte>( 1, 1 );
		_writer.WriteImage( _filePath, pixels );

		_ = Assert.Throws<NotSupportedException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_NotAGif_ThrowsInvalidDataException() {
		_fileSystem.AddFile( _filePath, new MockFileData( "this is not a gif" ) );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<byte>( _filePath ) );
	}

	[Test]
	public void ReadImage_ByteImage_RoundTrips() {
		IBuffer<byte> source = MockBuffer.Create<byte>( 3, 2 );
		source[0, 0] = 0;
		source[1, 0] = 128;
		source[2, 0] = 255;
		source[0, 1] = 64;
		source[1, 1] = 200;
		source[2, 1] = 32;
		_writer.WriteImage( _filePath, source );

		IBuffer<byte> pixels = _reader.ReadImage<byte>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( pixels.Columns, Is.EqualTo( 3 ) );
			Assert.That( pixels.Rows, Is.EqualTo( 2 ) );
			for( int y = 0; y < 2; y++ ) {
				for( int x = 0; x < 3; x++ ) {
					Assert.That( pixels[x, y], Is.EqualTo( source[x, y] ), $"Pixel mismatch at ({x},{y})." );
				}
			}
		}
	}

	[Test]
	public void ReadImage_BoolImage_RoundTrips() {
		IBuffer<bool> source = MockBuffer.Create<bool>( 3, 2 );
		source[0, 0] = true;
		source[2, 1] = true;
		_writer.WriteImage( _filePath, source );

		IBuffer<bool> pixels = _reader.ReadImage<bool>( _filePath );

		for( int y = 0; y < 2; y++ ) {
			for( int x = 0; x < 3; x++ ) {
				Assert.That( pixels[x, y], Is.EqualTo( source[x, y] ), $"Pixel mismatch at ({x},{y})." );
			}
		}
	}

	[Test]
	public void ReadImage_LargeByteImage_RoundTrips() {
		IBuffer<byte> source = MockBuffer.Create<byte>( 64, 64 );
		for( int y = 0; y < 64; y++ ) {
			for( int x = 0; x < 64; x++ ) {
				source[x, y] = (byte)( ( x * 4 ) ^ ( y * 3 ) );
			}
		}
		_writer.WriteImage( _filePath, source );

		IBuffer<byte> pixels = _reader.ReadImage<byte>( _filePath );

		for( int y = 0; y < 64; y++ ) {
			for( int x = 0; x < 64; x++ ) {
				Assert.That( pixels[x, y], Is.EqualTo( source[x, y] ), $"Pixel mismatch at ({x},{y})." );
			}
		}
	}
}
