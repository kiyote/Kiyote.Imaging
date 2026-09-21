using System.IO.Abstractions;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Gif;

internal sealed class GifImageWriter : GifWriterBase, IImageWriter {

	private readonly IFileSystem _fileSystem;

	public GifImageWriter(
		IFileSystem fileSystem
	) {
		ArgumentNullException.ThrowIfNull( fileSystem );
		_fileSystem = fileSystem;
	}

	void IImageWriter.WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	) {
		ThrowIfPixelTypeNotSupported<T>();
		ArgumentException.ThrowIfNullOrWhiteSpace( filePath );
		ArgumentNullException.ThrowIfNull( pixels );

		using Stream file = _fileSystem.File.Create( filePath );
		DoWriteImage( file, pixels );
	}

	void IImageWriter.WriteImage<T>(
		Stream file,
		IBuffer<T> pixels
	) {
		ThrowIfPixelTypeNotSupported<T>();
		ArgumentNullException.ThrowIfNull( file );
		ArgumentNullException.ThrowIfNull( pixels );

		DoWriteImage( file, pixels );
	}

	private static void DoWriteImage<T>(
		Stream file,
		IBuffer<T> pixels
	) {
		int width = pixels.Columns;
		int height = pixels.Rows;
		int colorTableSizeExponent = GetColorTableSizeExponent<T>();
		int minCodeSize = GetMinCodeSize<T>();

		WriteSignature( file );
		WriteLogicalScreenDescriptor( file, width, height, colorTableSizeExponent );
		WritePalette<T>( file );
		WriteImageDescriptor( file, width, height );
		WriteImageData( file, pixels, width, height, minCodeSize );
		WriteTrailer( file );
	}
}
