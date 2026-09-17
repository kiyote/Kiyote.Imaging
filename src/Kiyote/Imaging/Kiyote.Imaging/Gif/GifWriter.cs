using System.IO.Abstractions;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Gif;

public sealed class GifWriter : IImageWriter {

	private readonly IFileSystem _fileSystem;

	public GifWriter(
		IFileSystem fileSystem
	) {
		ArgumentNullException.ThrowIfNull( fileSystem );
		_fileSystem = fileSystem;
	}

	void IImageWriter.WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	) {
		GifChunkWriter.ThrowIfPixelTypeNotSupported<T>();

		ArgumentNullException.ThrowIfNull( pixels );

		int width = pixels.Columns;
		int height = pixels.Rows;

		byte[] palette = GifChunkWriter.GetPalette<T>();
		int colorTableSizeExponent = GifChunkWriter.GetColorTableSizeExponent<T>();
		int minCodeSize = GifChunkWriter.GetMinCodeSize<T>();
		byte[] indices = GifChunkWriter.GetIndices( pixels, width, height );

		using Stream output = _fileSystem.File.Create( filePath );
		GifChunkWriter.WriteSignature( output );
		GifChunkWriter.WriteLogicalScreenDescriptor( output, width, height, colorTableSizeExponent );
		output.Write( palette );
		GifChunkWriter.WriteImageDescriptor( output, width, height );
		GifChunkWriter.WriteImageData( output, indices, minCodeSize );
		GifChunkWriter.WriteTrailer( output );
	}
}
