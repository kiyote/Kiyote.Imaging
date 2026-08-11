using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Imaging.Png.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class PngWriterTests {

	private ServiceProvider _provider;
	private IBufferFactory _bufferFactory;
	private IImageWriter _writer;
	private string _folder;

	[SetUp]
	public void Setup() {
		ServiceCollection services = new ServiceCollection();
		_ = services.AddPngImaging();
		_provider = services.BuildServiceProvider();
		_bufferFactory = _provider.GetRequiredService<IBufferFactory>();
		_writer = _provider.GetRequiredService<IImageWriter>();

		_folder = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() );
		_ = Directory.CreateDirectory( _folder );
	}

	[TearDown]
	public void TearDown() {
		_provider.Dispose();
		Directory.Delete( _folder, true );
	}

	[Test]
	public void WriteImage_UIntBuffer_RoundTripsThroughFileSystem() {
		const int width = 37;
		const int height = 19;
		IBuffer<uint> pixels = _bufferFactory.Create( width, height, 0U );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				pixels[x, y] = unchecked( (uint)( ( x << 24 ) | ( y << 16 ) | 0x00_00_10_FF ) );
			}
		}
		string filePath = Path.Combine( _folder, "roundtrip.png" );

		_writer.WriteImage( filePath, pixels );

		DecodedPng png = TestPngDecoder.Read( filePath );
		Assert.That( png.Width, Is.EqualTo( width ) );
		Assert.That( png.Height, Is.EqualTo( height ) );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				uint expected = pixels[x, y];
				(byte r, byte g, byte b, byte a) = png.GetPixel( x, y );
				uint actual = ( (uint)r << 24 ) | ( (uint)g << 16 ) | ( (uint)b << 8 ) | a;
				Assert.That( actual, Is.EqualTo( expected ), $"Pixel mismatch at ({x},{y})." );
			}
		}
	}

	[Test]
	public void WriteImage_BoolBuffer_ProducesBlackAndWhiteImage() {
		const int width = 16;
		const int height = 16;
		IBuffer<bool> pixels = _bufferFactory.Create( width, height, false );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				pixels[x, y] = ( x + y ) % 2 == 0;
			}
		}
		string filePath = Path.Combine( _folder, "checker.png" );

		_writer.WriteImage( filePath, pixels );

		DecodedPng png = TestPngDecoder.Read( filePath );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				(byte R, byte G, byte B, byte A) expected = pixels[x, y]
					? ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF)
					: ((byte)0x00, (byte)0x00, (byte)0x00, (byte)0xFF);
				Assert.That( png.GetPixel( x, y ), Is.EqualTo( expected ), $"Pixel mismatch at ({x},{y})." );
			}
		}
	}

	[Test]
	public void WriteImage_ExistingFile_IsOverwritten() {
		string filePath = Path.Combine( _folder, "overwrite.png" );
		File.WriteAllText( filePath, "this is not a png" );
		IBuffer<int> pixels = _bufferFactory.Create( 4, 4, unchecked( (int)0xFF0000FFU ) );

		_writer.WriteImage( filePath, pixels );

		DecodedPng png = TestPngDecoder.Read( filePath );
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Width, Is.EqualTo( 4 ) );
			Assert.That( png.Height, Is.EqualTo( 4 ) );
			Assert.That( png.GetPixel( 2, 3 ), Is.EqualTo( (0xFF, 0x00, 0x00, 0xFF) ) );
			Assert.That( png.Chunks.All( c => c.CrcValid ), Is.True );
		}
	}

	[Test]
	public void WriteImage_LargeBuffer_ProducesCompressedOutput() {
		const int width = 256;
		const int height = 256;
		IBuffer<uint> pixels = _bufferFactory.Create( width, height, 0xFFFFFFFFU );
		string filePath = Path.Combine( _folder, "large.png" );

		_writer.WriteImage( filePath, pixels );

		long fileLength = new FileInfo( filePath ).Length;
		Assert.That( fileLength, Is.LessThan( width * height * 4 ), "Output was not compressed." );
	}
}
