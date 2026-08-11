using System.IO.Abstractions;
using Kiyote.Buffers;
using Kiyote.Imaging.Png;

namespace Kiyote.Imaging;

public interface IImageWriter {

	static IImageWriter CreatePng() => new PngWriter( new FileSystem() );

	void WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	);

}
