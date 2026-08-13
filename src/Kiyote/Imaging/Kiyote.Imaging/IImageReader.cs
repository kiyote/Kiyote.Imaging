using System.IO.Abstractions;
using Kiyote.Buffers;
using Kiyote.Imaging.Png;

namespace Kiyote.Imaging;

public interface IImageReader {

	static IImageReader CreatePng() => new PngReader( new ArrayBufferFactory(), new FileSystem() );

	IBuffer<T> ReadImage<T>(
		string filePath
	);

}
