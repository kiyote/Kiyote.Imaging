using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using Kiyote.Buffers;
using Kiyote.Imaging.UnitTests;

namespace Kiyote.Imaging.Png.UnitTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class PngWriterTests {

	private static readonly string[] _expectedChunkTypes = ["IHDR", "IDAT", "IEND"];

	private const string FileName = "image.png";

	private static readonly string _fileFolder = OperatingSystem.IsWindows() ? @"C:\images" : "/images";
	private static readonly string _filePath = Path.Combine( _fileFolder, FileName );

	private MockFileSystem _fileSystem;
	private IImageWriter _writer;

	[SetUp]
	public void Setup() {
		_fileSystem = new MockFileSystem();
		_fileSystem.AddDirectory( _fileFolder );
		_writer = new PngWriter( _fileSystem );
	}

	[Test]
	public void WriteImage_UnsupportedPixelType_ThrowsNotSupportedException() {
		IBuffer<long> pixels = MockBuffer.Create<long>( 1, 1 );

		_ = Assert.Throws<NotSupportedException>( () => _writer.WriteImage( _filePath, pixels ) );
		Assert.That( _fileSystem.File.Exists( _filePath ), Is.False );
	}

	[Test]
	public void WriteImage_UIntBuffer_WritesToSuppliedPath() {
		IBuffer<uint> pixels = MockBuffer.Create<uint>( 1, 1 );

		_writer.WriteImage( _filePath, pixels );

		Assert.That( _fileSystem.File.Exists( _filePath ), Is.True );
	}

	[Test]
	public void WriteImage_UIntBuffer_WritesExpectedHeader() {
		IBuffer<uint> pixels = MockBuffer.Create<uint>( 3, 2 );

		_writer.WriteImage( _filePath, pixels );

		DecodedPng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Width, Is.EqualTo( 3 ) );
			Assert.That( png.Height, Is.EqualTo( 2 ) );
			Assert.That( png.BitDepth, Is.EqualTo( 8 ) );
			Assert.That( png.ColourType, Is.EqualTo( 6 ) );
			Assert.That( png.CompressionMethod, Is.Zero );
			Assert.That( png.FilterMethod, Is.Zero );
			Assert.That( png.InterlaceMethod, Is.Zero );
		}
	}

	[Test]
	public void WriteImage_UIntBuffer_WritesExpectedChunkSequence() {
		IBuffer<uint> pixels = MockBuffer.Create<uint>( 2, 2 );

		_writer.WriteImage( _filePath, pixels );

		DecodedPng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Chunks.Select( c => c.Type ), Is.EqualTo( _expectedChunkTypes ) );
			Assert.That( png.Chunks.All( c => c.CrcValid ), Is.True, "One or more chunk CRCs are invalid." );
			Assert.That( png.Chunks[2].Data, Is.Empty );
		}
	}

	[Test]
	public void WriteImage_UIntBuffer_WritesRgbaPixels() {
		IBuffer<uint> pixels = MockBuffer.Create<uint>( 2, 1 );
		pixels[0, 0] = 0x11223344U;
		pixels[1, 0] = 0xAABBCCDDU;

		_writer.WriteImage( _filePath, pixels );

		DecodedPng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.GetPixel( 0, 0 ), Is.EqualTo( (0x11, 0x22, 0x33, 0x44) ) );
			Assert.That( png.GetPixel( 1, 0 ), Is.EqualTo( (0xAA, 0xBB, 0xCC, 0xDD) ) );
		}
	}

	[Test]
	public void WriteImage_IntBuffer_WritesRgbaPixels() {
		IBuffer<int> pixels = MockBuffer.Create<int>( 2, 1 );
		pixels[0, 0] = 0x11223344;
		pixels[1, 0] = unchecked((int)0xAABBCCDDU);

		_writer.WriteImage( _filePath, pixels );

		DecodedPng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.GetPixel( 0, 0 ), Is.EqualTo( (0x11, 0x22, 0x33, 0x44) ) );
			Assert.That( png.GetPixel( 1, 0 ), Is.EqualTo( (0xAA, 0xBB, 0xCC, 0xDD) ) );
		}
	}

	[Test]
	public void WriteImage_BoolBuffer_WritesBlackAndWhitePixels() {
		IBuffer<bool> pixels = MockBuffer.Create<bool>( 2, 1 );
		pixels[0, 0] = true;
		pixels[1, 0] = false;

		_writer.WriteImage( _filePath, pixels );

		DecodedPng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.GetPixel( 0, 0 ), Is.EqualTo( (0xFF, 0xFF, 0xFF, 0xFF) ) );
			Assert.That( png.GetPixel( 1, 0 ), Is.EqualTo( (0x00, 0x00, 0x00, 0xFF) ) );
		}
	}

	[Test]
	public void WriteImage_MultipleRows_WritesPixelsInRowOrder() {
		IBuffer<uint> pixels = MockBuffer.Create<uint>( 2, 2 );
		pixels[0, 0] = 0x000000FFU;
		pixels[1, 0] = 0x0000FF00U;
		pixels[0, 1] = 0x00FF0000U;
		pixels[1, 1] = 0xFF000000U;

		_writer.WriteImage( _filePath, pixels );

		DecodedPng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.GetPixel( 0, 0 ), Is.EqualTo( (0x00, 0x00, 0x00, 0xFF) ) );
			Assert.That( png.GetPixel( 1, 0 ), Is.EqualTo( (0x00, 0x00, 0xFF, 0x00) ) );
			Assert.That( png.GetPixel( 0, 1 ), Is.EqualTo( (0x00, 0xFF, 0x00, 0x00) ) );
			Assert.That( png.GetPixel( 1, 1 ), Is.EqualTo( (0xFF, 0x00, 0x00, 0x00) ) );
		}
	}

	[Test]
	public void WriteImage_ByteBuffer_WritesGreyscalePixels() {
		IBuffer<byte> pixels = MockBuffer.Create<byte>( 3, 1 );
		pixels[0, 0] = 0x00;
		pixels[1, 0] = 0x80;
		pixels[2, 0] = 0xFF;

		_writer.WriteImage( _filePath, pixels );

		DecodedPng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.GetPixel( 0, 0 ), Is.EqualTo( (0x00, 0x00, 0x00, 0xFF) ), "0 should be black." );
			Assert.That( png.GetPixel( 1, 0 ), Is.EqualTo( (0x80, 0x80, 0x80, 0xFF) ) );
			Assert.That( png.GetPixel( 2, 0 ), Is.EqualTo( (0xFF, 0xFF, 0xFF, 0xFF) ), "255 should be white." );
		}
	}

	private DecodedPng Decode() {
		return TestPngDecoder.Read( _fileSystem.File.ReadAllBytes( _filePath ) );
	}
}


