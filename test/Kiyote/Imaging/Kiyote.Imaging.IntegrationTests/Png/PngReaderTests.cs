using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Imaging.Png.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class PngReaderTests {

	private ServiceProvider _provider;
	private IBufferFactory _bufferFactory;
	private IImageWriter _writer;
	private IImageReader _reader;
	private string _folder;

	[SetUp]
	public void Setup() {
		ServiceCollection services = new ServiceCollection();
		_ = services
			.AddBuffers()
			.AddPngImaging();
		_provider = services.BuildServiceProvider();
		_bufferFactory = _provider.GetRequiredService<IBufferFactory>();
		_writer = _provider.GetRequiredService<IImageWriter>();
		_reader = _provider.GetRequiredService<IImageReader>();

		_folder = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() );
		_ = Directory.CreateDirectory( _folder );
	}

	[TearDown]
	public void TearDown() {
		_provider.Dispose();
		Directory.Delete( _folder, true );
	}

	[Test]
	public void ReadImage_UIntImageWrittenToDisk_RoundTrips() {
		const int width = 23;
		const int height = 31;
		IBuffer<uint> source = _bufferFactory.Create( width, height, 0U );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				source[x, y] = unchecked( (uint)( ( x << 24 ) | ( y << 16 ) | 0x0000_20FF ) );
			}
		}
		string filePath = Path.Combine( _folder, "roundtrip.png" );
		_writer.WriteImage( filePath, source );

		IBuffer<uint> pixels = _reader.ReadImage<uint>( filePath );

		Assert.That( pixels.Columns, Is.EqualTo( width ) );
		Assert.That( pixels.Rows, Is.EqualTo( height ) );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				Assert.That( pixels[x, y], Is.EqualTo( source[x, y] ), $"Pixel mismatch at ({x},{y})." );
			}
		}
	}

	[Test]
	public void ReadImage_IntImageWrittenToDisk_RoundTrips() {
		IBuffer<int> source = _bufferFactory.Create( 8, 8, unchecked( (int)0xFF0000FFU ) );
		string filePath = Path.Combine( _folder, "int.png" );
		_writer.WriteImage( filePath, source );

		IBuffer<int> pixels = _reader.ReadImage<int>( filePath );

		Assert.That( pixels[4, 5], Is.EqualTo( unchecked( (int)0xFF0000FFU ) ) );
	}

	[Test]
	public void ReadImage_BoolImageWrittenToDisk_RoundTrips() {
		const int width = 16;
		const int height = 16;
		IBuffer<bool> source = _bufferFactory.Create( width, height, false );
		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				source[x, y] = ( x + y ) % 2 == 0;
			}
		}
		string filePath = Path.Combine( _folder, "checker.png" );
		_writer.WriteImage( filePath, source );

		IBuffer<bool> pixels = _reader.ReadImage<bool>( filePath );

		for( int y = 0; y < height; y++ ) {
			for( int x = 0; x < width; x++ ) {
				Assert.That( pixels[x, y], Is.EqualTo( source[x, y] ), $"Pixel mismatch at ({x},{y})." );
			}
		}
	}

	[Test]
	public void ReadImage_ColourImageAsBool_MapsBlackToTrue() {
		IBuffer<uint> source = _bufferFactory.Create( 4, 4, 0x0A0B0CFFU );
		source[1, 1] = 0x000000FFU;
		string filePath = Path.Combine( _folder, "asbool.png" );
		_writer.WriteImage( filePath, source );

		IBuffer<bool> pixels = _reader.ReadImage<bool>( filePath );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( pixels[1, 1], Is.True );
			Assert.That( pixels[0, 0], Is.False );
		}
	}
}
