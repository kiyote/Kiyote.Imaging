using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using Kiyote.Buffers;
using Kiyote.Imaging.UnitTests;

namespace Kiyote.Imaging.Gif.UnitTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GifWriterTests {

	private const string FileName = "image.gif";

	private static readonly string _fileFolder = OperatingSystem.IsWindows() ? @"C:\images" : "/images";
	private static readonly string _filePath = Path.Combine( _fileFolder, FileName );

	private MockFileSystem _fileSystem;
	private IImageWriter _writer;

	[SetUp]
	public void Setup() {
		_fileSystem = new MockFileSystem();
		_fileSystem.AddDirectory( _fileFolder );
		_writer = new GifWriter( _fileSystem );
	}

	[Test]
	public void WriteImage_UnsupportedPixelType_ThrowsNotSupportedException() {
		IBuffer<uint> pixels = MockBuffer.Create<uint>( 1, 1 );

		_ = Assert.Throws<NotSupportedException>( () => _writer.WriteImage( _filePath, pixels ) );
		Assert.That( _fileSystem.File.Exists( _filePath ), Is.False );
	}

	[Test]
	public void WriteImage_ByteBuffer_WritesToSuppliedPath() {
		IBuffer<byte> pixels = MockBuffer.Create<byte>( 1, 1 );

		_writer.WriteImage( _filePath, pixels );

		Assert.That( _fileSystem.File.Exists( _filePath ), Is.True );
	}

	[Test]
	public void WriteImage_ByteBuffer_WritesGifSignature() {
		IBuffer<byte> pixels = MockBuffer.Create<byte>( 2, 2 );

		_writer.WriteImage( _filePath, pixels );

		byte[] bytes = _fileSystem.File.ReadAllBytes( _filePath );
		Assert.That( bytes.Take( 6 ), Is.EqualTo( "GIF89a"u8.ToArray() ) );
	}

	[Test]
	public void WriteImage_BoolBuffer_WritesGifSignature() {
		IBuffer<bool> pixels = MockBuffer.Create<bool>( 2, 2 );

		_writer.WriteImage( _filePath, pixels );

		byte[] bytes = _fileSystem.File.ReadAllBytes( _filePath );
		Assert.That( bytes.Take( 6 ), Is.EqualTo( "GIF89a"u8.ToArray() ) );
	}
}
