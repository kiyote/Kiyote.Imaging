using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using Kiyote.Buffers;
using Kiyote.Imaging.UnitTests;

namespace Kiyote.Imaging.Png.UnitTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class PngReaderFormatTests {

	private const string FileName = "image.png";

	private static readonly string _fileFolder = OperatingSystem.IsWindows() ? @"C:\images" : "/images";
	private static readonly string _filePath = Path.Combine( _fileFolder, FileName );

	private MockFileSystem _fileSystem;
	private IImageReader _reader;

	[SetUp]
	public void Setup() {
		_fileSystem = new MockFileSystem();
		_fileSystem.AddDirectory( _fileFolder );
		_reader = new PngReader( MockBufferFactory.Create(), _fileSystem );
	}

	[Test]
	public void ReadImage_GreyscaleImage_ExpandsToRgba() {
		TestPngBuilder builder = new TestPngBuilder( 2, 2, 0 );
		builder.FillPattern();
		Write( builder );

		IBuffer<uint> pixels = _reader.ReadImage<uint>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			for( int y = 0; y < 2; y++ ) {
				for( int x = 0; x < 2; x++ ) {
					byte grey = builder.GetSample( x, y, 0 );
					Assert.That( pixels[x, y], Is.EqualTo( Rgba( grey, grey, grey, byte.MaxValue ) ), $"Pixel mismatch at ({x},{y})." );
				}
			}
		}
	}

	[Test]
	public void ReadImage_TruecolourImage_AddsOpaqueAlpha() {
		TestPngBuilder builder = new TestPngBuilder( 2, 2, 2 );
		builder.FillPattern();
		Write( builder );

		IBuffer<uint> pixels = _reader.ReadImage<uint>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			for( int y = 0; y < 2; y++ ) {
				for( int x = 0; x < 2; x++ ) {
					uint expected = Rgba(
						builder.GetSample( x, y, 0 ),
						builder.GetSample( x, y, 1 ),
						builder.GetSample( x, y, 2 ),
						byte.MaxValue
					);
					Assert.That( pixels[x, y], Is.EqualTo( expected ), $"Pixel mismatch at ({x},{y})." );
				}
			}
		}
	}

	[Test]
	public void ReadImage_GreyscaleWithAlphaImage_PreservesAlpha() {
		TestPngBuilder builder = new TestPngBuilder( 2, 2, 4 );
		builder.FillPattern();
		Write( builder );

		IBuffer<uint> pixels = _reader.ReadImage<uint>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			for( int y = 0; y < 2; y++ ) {
				for( int x = 0; x < 2; x++ ) {
					byte grey = builder.GetSample( x, y, 0 );
					byte alpha = builder.GetSample( x, y, 1 );
					Assert.That( pixels[x, y], Is.EqualTo( Rgba( grey, grey, grey, alpha ) ), $"Pixel mismatch at ({x},{y})." );
				}
			}
		}
	}

	[TestCase( (byte)1, TestName = "ReadImage_SubFilteredImage_IsReconstructed" )]
	[TestCase( (byte)2, TestName = "ReadImage_UpFilteredImage_IsReconstructed" )]
	[TestCase( (byte)3, TestName = "ReadImage_AverageFilteredImage_IsReconstructed" )]
	[TestCase( (byte)4, TestName = "ReadImage_PaethFilteredImage_IsReconstructed" )]
	public void ReadImage_FilteredImage_IsReconstructed(
		byte filter
	) {
		TestPngBuilder builder = new TestPngBuilder( 4, 3, 6 );
		builder.FillPattern();
		for( int y = 0; y < 3; y++ ) {
			builder.SetFilter( y, filter );
		}
		Write( builder );

		IBuffer<uint> pixels = _reader.ReadImage<uint>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			for( int y = 0; y < 3; y++ ) {
				for( int x = 0; x < 4; x++ ) {
					uint expected = Rgba(
						builder.GetSample( x, y, 0 ),
						builder.GetSample( x, y, 1 ),
						builder.GetSample( x, y, 2 ),
						builder.GetSample( x, y, 3 )
					);
					Assert.That( pixels[x, y], Is.EqualTo( expected ), $"Pixel mismatch at ({x},{y})." );
				}
			}
		}
	}

	[Test]
	public void ReadImage_MixedFilters_IsReconstructed() {
		TestPngBuilder builder = new TestPngBuilder( 4, 5, 6 );
		builder.FillPattern();
		for( int y = 0; y < 5; y++ ) {
			builder.SetFilter( y, (byte)y );
		}
		Write( builder );

		IBuffer<uint> pixels = _reader.ReadImage<uint>( _filePath );

		using( Assert.EnterMultipleScope() ) {
			for( int y = 0; y < 5; y++ ) {
				for( int x = 0; x < 4; x++ ) {
					uint expected = Rgba(
						builder.GetSample( x, y, 0 ),
						builder.GetSample( x, y, 1 ),
						builder.GetSample( x, y, 2 ),
						builder.GetSample( x, y, 3 )
					);
					Assert.That( pixels[x, y], Is.EqualTo( expected ), $"Pixel mismatch at ({x},{y})." );
				}
			}
		}
	}

	[Test]
	public void ReadImage_UnknownFilterType_ThrowsInvalidDataException() {
		TestPngBuilder builder = new TestPngBuilder( 2, 1, 6 );
		builder.FillPattern();
		builder.SetFilter( 0, 5 );
		Write( builder );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_UnsupportedCompressionMethod_ThrowsNotSupportedException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 6 ) {
			CompressionMethod = 1
		};
		Write( builder );

		_ = Assert.Throws<NotSupportedException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_UnsupportedFilterMethod_ThrowsNotSupportedException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 6 ) {
			FilterMethod = 1
		};
		Write( builder );

		_ = Assert.Throws<NotSupportedException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_InterlacedImage_ThrowsNotSupportedException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 6 ) {
			InterlaceMethod = 1
		};
		Write( builder );

		_ = Assert.Throws<NotSupportedException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_SixteenBitImage_ThrowsNotSupportedException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 6 ) {
			BitDepth = 16
		};
		Write( builder );

		_ = Assert.Throws<NotSupportedException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_PalettedImage_ThrowsNotSupportedException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 3 );
		Write( builder );

		_ = Assert.Throws<NotSupportedException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_MalformedHeaderChunk_ThrowsInvalidDataException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 6 ) {
			HeaderLength = 12
		};
		Write( builder );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_MissingHeaderChunk_ThrowsInvalidDataException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 6 ) {
			OmitHeader = true
		};
		Write( builder );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_ChunkLengthBeyondEndOfFile_ThrowsInvalidDataException() {
		TestPngBuilder builder = new TestPngBuilder( 1, 1, 6 ) {
			AppendMalformedChunk = true
		};
		Write( builder );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	[Test]
	public void ReadImage_TruncatedImageData_ThrowsInvalidDataException() {
		TestPngBuilder builder = new TestPngBuilder( 2, 4, 6 ) {
			RowCount = 1
		};
		builder.FillPattern();
		Write( builder );

		_ = Assert.Throws<InvalidDataException>( () => _reader.ReadImage<uint>( _filePath ) );
	}

	private static uint Rgba(
		byte r,
		byte g,
		byte b,
		byte a
	) {
		return ( (uint)r << 24 ) | ( (uint)g << 16 ) | ( (uint)b << 8 ) | a;
	}

	private void Write(
		TestPngBuilder builder
	) {
		_fileSystem.AddFile( _filePath, new MockFileData( builder.Build() ) );
	}
}
