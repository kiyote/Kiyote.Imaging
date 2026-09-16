using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using Kiyote.Buffers;
using Kiyote.Imaging.UnitTests;

namespace Kiyote.Imaging.Png.UnitTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class APngWriterTests {

	private const string FileName = "animation.png";

	private static readonly string _fileFolder = OperatingSystem.IsWindows() ? @"C:\images" : "/images";
	private static readonly string _filePath = Path.Combine( _fileFolder, FileName );

	private MockFileSystem _fileSystem;
	private IAnimationWriter _writer;

	[SetUp]
	public void Setup() {
		_fileSystem = new MockFileSystem();
		_fileSystem.AddDirectory( _fileFolder );
		_writer = new APngWriter( _fileSystem );
	}

	[Test]
	public void AddFrame_UnsupportedPixelType_ThrowsNotSupportedException() {
		IBuffer<long> frame = MockBuffer.Create<long>( 1, 1 );

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
		IBuffer<uint> first = MockBuffer.Create<uint>( 1, 1 );
		IBuffer<uint> second = MockBuffer.Create<uint>( 1, 1 );

		using IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );
		builder.AddFrame( first );
		builder.FinishAnimation();

		_ = Assert.Throws<InvalidOperationException>( () => builder.AddFrame( second ) );
	}

	[Test]
	public void FinishAnimation_AfterFinishAnimation_ThrowsInvalidOperationException() {
		IBuffer<uint> frame = MockBuffer.Create<uint>( 1, 1 );

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
	public void FinishAnimation_SingleFrame_WritesToSuppliedPath() {
		IBuffer<uint> frame = MockBuffer.Create<uint>( 2, 2 );

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) ) ) {
			builder.AddFrame( frame );
			builder.FinishAnimation();
		}

		Assert.That( _fileSystem.File.Exists( _filePath ), Is.True );
	}

	[Test]
	public void FinishAnimation_SingleFrame_WritesExpectedHeader() {
		IBuffer<uint> frame = MockBuffer.Create<uint>( 3, 2 );

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) ) ) {
			builder.AddFrame( frame );
			builder.FinishAnimation();
		}

		DecodedApng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Width, Is.EqualTo( 3 ) );
			Assert.That( png.Height, Is.EqualTo( 2 ) );
			Assert.That( png.BitDepth, Is.EqualTo( 8 ) );
			Assert.That( png.ColourType, Is.EqualTo( 6 ) );
		}
	}

	[Test]
	public void FinishAnimation_MultipleFrames_WritesExpectedChunkSequence() {
		IBuffer<uint> first = MockBuffer.Create<uint>( 2, 2 );
		IBuffer<uint> second = MockBuffer.Create<uint>( 2, 2 );
		IBuffer<uint> third = MockBuffer.Create<uint>( 2, 2 );

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) ) ) {
			builder.AddFrame( first );
			builder.AddFrame( second );
			builder.AddFrame( third );
			builder.FinishAnimation();
		}

		DecodedApng png = Decode();
		string[] expectedChunkTypes = ["IHDR", "acTL", "fcTL", "IDAT", "fcTL", "fdAT", "fcTL", "fdAT", "IEND"];
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Chunks.Select( c => c.Type ), Is.EqualTo( expectedChunkTypes ) );
			Assert.That( png.Chunks.All( c => c.CrcValid ), Is.True, "One or more chunk CRCs are invalid." );
			Assert.That( png.NumFrames, Is.EqualTo( 3 ) );
			Assert.That( png.Frames, Has.Count.EqualTo( 3 ) );
		}
	}

	[Test]
	public void FinishAnimation_LoopCount_WritesToAnimationControl() {
		IBuffer<uint> frame = MockBuffer.Create<uint>( 1, 1 );

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ), 5 ) ) {
			builder.AddFrame( frame );
			builder.FinishAnimation();
		}

		DecodedApng png = Decode();
		Assert.That( png.NumPlays, Is.EqualTo( 5 ) );
	}

	[Test]
	public void FinishAnimation_FrameDelay_WritesToFrameControl() {
		IBuffer<uint> frame = MockBuffer.Create<uint>( 1, 1 );

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 250 ) ) ) {
			builder.AddFrame( frame );
			builder.FinishAnimation();
		}

		DecodedApng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Frames[0].Control.DelayNum, Is.EqualTo( 250 ) );
			Assert.That( png.Frames[0].Control.DelayDen, Is.EqualTo( 1000 ) );
		}
	}

	[Test]
	public void FinishAnimation_MultipleFrames_WritesEachFramePixels() {
		IBuffer<uint> first = MockBuffer.Create<uint>( 1, 1 );
		first[0, 0] = 0x11223344U;
		IBuffer<uint> second = MockBuffer.Create<uint>( 1, 1 );
		second[0, 0] = 0xAABBCCDDU;

		using( IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) ) ) {
			builder.AddFrame( first );
			builder.AddFrame( second );
			builder.FinishAnimation();
		}

		DecodedApng png = Decode();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( png.Frames[0].GetPixel( 0, 0 ), Is.EqualTo( (0x11, 0x22, 0x33, 0x44) ) );
			Assert.That( png.Frames[1].GetPixel( 0, 0 ), Is.EqualTo( (0xAA, 0xBB, 0xCC, 0xDD) ) );
		}
	}

	[Test]
	public void AddFrame_MismatchedFrameDimensions_ThrowsNotSupportedException() {
		IBuffer<uint> first = MockBuffer.Create<uint>( 2, 2 );
		IBuffer<uint> second = MockBuffer.Create<uint>( 3, 3 );

		using IAnimationBuilder builder = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );
		builder.AddFrame( first );

		_ = Assert.Throws<NotSupportedException>( () => builder.AddFrame( second ) );
	}

	[Test]
	public void StartAnimation_MultipleConcurrentBuilders_AreIndependent() {
		string otherFilePath = Path.Combine( _fileFolder, "other.png" );
		IBuffer<uint> first = MockBuffer.Create<uint>( 1, 1 );
		IBuffer<uint> second = MockBuffer.Create<uint>( 1, 1 );

		using IAnimationBuilder builderOne = _writer.StartAnimation( _filePath, TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder builderTwo = _writer.StartAnimation( otherFilePath, TimeSpan.FromMilliseconds( 100 ) );

		builderOne.AddFrame( first );
		builderTwo.AddFrame( second );
		builderOne.FinishAnimation();
		builderTwo.FinishAnimation();

		Assert.That( _fileSystem.File.Exists( _filePath ), Is.True );
		Assert.That( _fileSystem.File.Exists( otherFilePath ), Is.True );
	}

	private DecodedApng Decode() {
		return TestApngDecoder.Read( _fileSystem.File.ReadAllBytes( _filePath ) );
	}
}
