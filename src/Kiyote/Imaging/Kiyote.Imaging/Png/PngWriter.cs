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
		Stream file,
		IBuffer<T> pixels
	) {
		PngChunkWriter.ThrowIfPixelTypeNotSupported<T>();
		ArgumentNullException.ThrowIfNull( pixels );

		DoWriteImage( file, pixels );
	}

	void IImageWriter.WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	) {
		PngChunkWriter.ThrowIfPixelTypeNotSupported<T>();
		ArgumentNullException.ThrowIfNull( pixels );
		ArgumentNullException.ThrowIfNull( filePath );

		using Stream output = _fileSystem.File.Create( filePath );
		DoWriteImage( output, pixels );
	}

	private static void DoWriteImage<T>(
		Stream file,
		IBuffer<T> pixels
	) {
		int width = pixels.Columns;
		int height = pixels.Rows;

		file.Write( _signature );

		PngChunkWriter.WriteHeader( file, width, height );
		byte[] compressed = PngChunkWriter.CompressFrame( pixels, width, height );
		PngChunkWriter.WriteChunk( file, "IDAT", compressed );
		PngChunkWriter.WriteChunk( file, "IEND", [] );
	}
}
