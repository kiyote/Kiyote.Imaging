using System.IO.Abstractions;
using Kiyote.Buffers;

namespace Kiyote.Imaging.Png;

public sealed class PngWriter : IImageWriter {

	private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];

	private readonly IFileSystem _fileSystem;

	public PngWriter(
		IFileSystem fileSystem
	) {
		ArgumentNullException.ThrowIfNull( fileSystem );
		_fileSystem = fileSystem;
	}

	void IImageWriter.WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	) {
		PngChunkWriter.ThrowIfPixelTypeNotSupported<T>();

		ArgumentNullException.ThrowIfNull( pixels );

		int width = pixels.Columns;
		int height = pixels.Rows;

		using Stream output = _fileSystem.File.Create( filePath );
		output.Write( _signature );

		PngChunkWriter.WriteHeader( output, width, height );
		byte[] compressed = PngChunkWriter.CompressFrame( pixels, width, height );
		PngChunkWriter.WriteChunk( output, "IDAT", compressed );
		PngChunkWriter.WriteChunk( output, "IEND", [] );
	}
}
