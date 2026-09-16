using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Imaging.Png.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class APngWriterTests {

	private ServiceProvider _provider;
	private IBufferFactory _bufferFactory;
	private IAnimationWriter _writer;
	private string _folder;

	[SetUp]
	public void Setup() {
		ServiceCollection services = new ServiceCollection();
		_ = services.AddPngImaging();
		_provider = services.BuildServiceProvider();
		_bufferFactory = _provider.GetRequiredService<IBufferFactory>();
		_writer = _provider.GetRequiredService<IAnimationWriter>();

		_folder = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() );
		_ = Directory.CreateDirectory( _folder );
	}

	[TearDown]
	public void TearDown() {
		_provider.Dispose();
		Directory.Delete( _folder, true );
	}

	[Test]
	public void FinishAnimation_UIntFrames_RoundTripsThroughFileSystem() {
		const int width = 17;
		const int height = 11;
		const int frameCount = 4;
		IBuffer<uint>[] frames = new IBuffer<uint>[frameCount];
		for( int frameIndex = 0; frameIndex < frameCount; frameIndex++ ) {
			IBuffer<uint> pixels = _bufferFactory.Create( width, height, 0U );
			for( int y = 0; y < height; y++ ) {
				for( int x = 0; x < width; x++ ) {
					pixels[x, y] = unchecked( (uint)( ( x << 24 ) | ( y << 16 ) | ( frameIndex << 8 ) | 0xFF ) );
				}
			}
			frames[frameIndex] = pixels;
		}
		string filePath = Path.Combine( _folder, "animation.png" );

		using( IAnimationBuilder builder = _writer.StartAnimation( filePath, TimeSpan.FromMilliseconds( 150 ), 3 ) ) {
			foreach( IBuffer<uint> frame in frames ) {
				builder.AddFrame( frame );
			}
			builder.FinishAnimation();
		}

		DecodedApng png = TestApngDecoder.Read( filePath );
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Width, Is.EqualTo( width ) );
			Assert.That( png.Height, Is.EqualTo( height ) );
			Assert.That( png.NumFrames, Is.EqualTo( frameCount ) );
			Assert.That( png.NumPlays, Is.EqualTo( 3 ) );
			Assert.That( png.Frames, Has.Count.EqualTo( frameCount ) );
			Assert.That( png.Chunks.All( c => c.CrcValid ), Is.True );
		}

		for( int frameIndex = 0; frameIndex < frameCount; frameIndex++ ) {
			ApngFrame frame = png.Frames[frameIndex];
			Assert.That( frame.Control.DelayNum, Is.EqualTo( 150 ) );
			Assert.That( frame.Control.DelayDen, Is.EqualTo( 1000 ) );
			for( int y = 0; y < height; y++ ) {
				for( int x = 0; x < width; x++ ) {
					uint expected = frames[frameIndex][x, y];
					(byte r, byte g, byte b, byte a) = frame.GetPixel( x, y );
					uint actual = ( (uint)r << 24 ) | ( (uint)g << 16 ) | ( (uint)b << 8 ) | a;
					Assert.That( actual, Is.EqualTo( expected ), $"Pixel mismatch at frame {frameIndex} ({x},{y})." );
				}
			}
		}
	}

	[Test]
	public void FinishAnimation_DefaultLoopCount_IsInfinite() {
		IBuffer<uint> frame = _bufferFactory.Create( 4, 4, 0xFF0000FFU );
		string filePath = Path.Combine( _folder, "loop.png" );

		using( IAnimationBuilder builder = _writer.StartAnimation( filePath, TimeSpan.FromMilliseconds( 100 ) ) ) {
			builder.AddFrame( frame );
			builder.FinishAnimation();
		}

		DecodedApng png = TestApngDecoder.Read( filePath );
		Assert.That( png.NumPlays, Is.Zero );
	}

	[Test]
	public void FinishAnimation_FramesAddedOneAtATime_DoesNotRequireBufferingAllFrames() {
		const int width = 8;
		const int height = 8;
		string filePath = Path.Combine( _folder, "streamed.png" );

		using( IAnimationBuilder builder = _writer.StartAnimation( filePath, TimeSpan.FromMilliseconds( 100 ) ) ) {
			for( int frameIndex = 0; frameIndex < 5; frameIndex++ ) {
				IBuffer<uint> frame = _bufferFactory.Create( width, height, unchecked( (uint)( frameIndex << 24 | 0xFF ) ) );
				builder.AddFrame( frame );
			}
			builder.FinishAnimation();
		}

		DecodedApng png = TestApngDecoder.Read( filePath );
		Assert.That( png.NumFrames, Is.EqualTo( 5 ) );
	}
}
