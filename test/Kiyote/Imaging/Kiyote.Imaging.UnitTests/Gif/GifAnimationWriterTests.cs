using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using Kiyote.Buffers;
using Kiyote.Imaging.UnitTests;

namespace Kiyote.Imaging.Gif.UnitTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GifAnimationWriterTests {

	private const string FileName = "animation.gif";

	private static readonly string _fileFolder = OperatingSystem.IsWindows() ? @"C:\images" : "/images";
	private static readonly string _filePath = Path.Combine( _fileFolder, FileName );

	private MockFileSystem _fileSystem;
	private IAnimationWriter _writer;

	[SetUp]
	public void Setup() {
		_fileSystem = new MockFileSystem();
		_fileSystem.AddDirectory( _fileFolder );
		_writer = new GifAnimationWriter( _fileSystem );
	}

	[Test]
	public void AddFrame_UnsupportedPixelType_ThrowsNotSupportedException() {
		IBuffer<uint> frame = MockBuffer.Create<uint>( 1, 1 );

		using IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );

		_ = Assert.Throws<NotSupportedException>( () => builder.AddFrame( frame ) );
	}

	[Test]
	public void FinishAnimation_NoFrames_ThrowsInvalidOperationException() {
		using IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );

		_ = Assert.Throws<InvalidOperationException>( () => builder.FinishAnimation() );
	}

	[Test]
	public void AddFrame_AfterFinishAnimation_ThrowsInvalidOperationException() {
		IBuffer<byte> first = MockBuffer.Create<byte>( 1, 1 );
		IBuffer<byte> second = MockBuffer.Create<byte>( 1, 1 );

		using IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );
		builder.AddFrame( first );
		builder.FinishAnimation();

		_ = Assert.Throws<InvalidOperationException>( () => builder.AddFrame( second ) );
	}

	[Test]
	public void FinishAnimation_AfterFinishAnimation_ThrowsInvalidOperationException() {
		IBuffer<byte> frame = MockBuffer.Create<byte>( 1, 1 );

		using IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );
		builder.AddFrame( frame );
		builder.FinishAnimation();

		_ = Assert.Throws<InvalidOperationException>( () => builder.FinishAnimation() );
	}

	[Test]
	public void StartAnimation_NegativeLoopCount_ThrowsArgumentOutOfRangeException() {
		_ = Assert.Throws<ArgumentOutOfRangeException>( () => _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ), -1 ) );
	}

	[Test]
	public void FinishAnimation_MultipleFrames_WritesToSuppliedPath() {
		IBuffer<byte> first = MockBuffer.Create<byte>( 2, 2 );
		IBuffer<byte> second = MockBuffer.Create<byte>( 2, 2 );

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) ) ) {
			builder.AddFrame( first );
			builder.AddFrame( second );
			builder.FinishAnimation();
		}

		Assert.That( _fileSystem.File.Exists( _filePath ), Is.True );
	}

	[Test]
	public void FinishAnimation_MismatchedDimensions_ThrowsNotSupportedException() {
		IBuffer<byte> first = MockBuffer.Create<byte>( 2, 2 );
		IBuffer<byte> second = MockBuffer.Create<byte>( 3, 3 );

		using IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );
		builder.AddFrame( first );

		_ = Assert.Throws<NotSupportedException>( () => builder.AddFrame( second ) );
	}

	[Test]
	public void FinishAnimation_MultipleFrames_CanBeReadBackAsFrames() {
		IBuffer<byte> first = MockBuffer.Create<byte>( 2, 2 );
		first[0, 0] = 10;
		IBuffer<byte> second = MockBuffer.Create<byte>( 2, 2 );
		second[0, 0] = 200;

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ), 5 ) ) {
			builder.AddFrame( first );
			builder.AddFrame( second );
			builder.FinishAnimation();
		}

		byte[] bytes = _fileSystem.File.ReadAllBytes( _filePath );
		System.Collections.Generic.List<GifFrame> frames = GifChunkReader.ReadAllFrames( bytes );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( frames, Has.Count.EqualTo( 2 ) );
			Assert.That( frames[0].Palette[frames[0].Indices[0] * 3], Is.EqualTo( 10 ) );
			Assert.That( frames[1].Palette[frames[1].Indices[0] * 3], Is.EqualTo( 200 ) );
		}
	}
}
