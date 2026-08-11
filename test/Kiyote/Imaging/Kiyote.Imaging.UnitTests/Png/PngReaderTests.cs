using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using Kiyote.Buffers;
using Kiyote.Imaging.UnitTests;

namespace Kiyote.Imaging.Png.UnitTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class PngReaderTests {

	private const string FileName = "image.png";

	private static readonly string _fileFolder = OperatingSystem.IsWindows() ? @"C:\images" : "/images";
	private static readonly string _filePath = Path.Combine( _fileFolder, FileName );

	private MockFileSystem _fileSystem;
	private IImageWriter _writer;
	private IImageReader _reader;

	[SetUp]
	public void Setup() {
		_fileSystem = new MockFileSystem();
		_fileSystem.AddDirectory( _fileFolder );
		_writer = new PngWriter( _fileSystem );
		_reader = new PngReader( MockBufferFactory.Create(), _fileSystem );
	}

	[Test]
	public void ReadImage_UnsupportedPixelType_ThrowsNotSupportedException() {
		WriteUInt( 0x11223344U );

		_ = Assert.Throws<NotSupportedException>( () => _reader.ReadImage<byte>( _filePath ) );
	}

	[Test]
	public void ReadImage_NotAPng_ThrowsInvalidDataException() {
		_fileSystem.AddFile( _filePath, new MockFileData( "this is not a png" ) );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_CorruptedChunk_ThrowsInvalidDataException() {
		WriteUInt( 0x11223344U );
		byte[] bytes = _fileSystem.File.ReadAllBytes( _filePath );
		bytes[20] ^= 0xFF;
		_fileSystem.File.WriteAllBytes( _filePath, bytes );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_UIntImage_ReturnsRgbaValues() {
		WriteUInt( 0x11223344U, 0xAABBCCDDU );

		IBuffer<uint> pixels = _reader.ReadImage<uint>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( pixels.Columns, Is.EqualTo( 2 ) );
			Assert.That( pixels.Rows, Is.EqualTo( 1 ) );
			Assert.That( pixels[0, 0], Is.EqualTo( 0x11223344U ) );
			Assert.That( pixels[1, 0], Is.EqualTo( 0xAABBCCDDU ) );
		}
	}

	[Test]
	public void ReadImage_IntImage_ReturnsRgbaValues() {
		WriteUInt( 0x11223344U, 0xAABBCCDDU );

		IBuffer<int> pixels = _reader.ReadImage<int>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( pixels[0, 0], Is.EqualTo( 0x11223344 ) );
			Assert.That( pixels[1, 0], Is.EqualTo( unchecked( (int)0xAABBCCDDU ) ) );
		}
	}

	[Test]
	public void ReadImage_AsBool_BlackPixelsAreTrue() {
		WriteUInt( 0x000000FFU, 0xFFFFFFFFU, 0x01000000U );

		IBuffer<bool> pixels = _reader.ReadImage<bool>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( pixels[0, 0], Is.True, "Black pixel should be true." );
			Assert.That( pixels[1, 0], Is.False, "White pixel should be false." );
			Assert.That( pixels[2, 0], Is.False, "Non-black pixel should be false." );
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

	private void WriteUInt(
		params uint[] values
	) {
		IBuffer<uint> buffer = MockBuffer.Create<uint>( values.Length, 1 );
		for( int x = 0; x < values.Length; x++ ) {
			buffer[x, 0] = values[x];
		}
		_writer.WriteImage( _filePath, buffer );
	}
}


