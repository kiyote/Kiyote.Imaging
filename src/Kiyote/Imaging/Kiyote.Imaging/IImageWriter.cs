using Kiyote.Buffers;

namespace Kiyote.Imaging;

public interface IImageWriter {

	void WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	);

	void WriteImage<T>(
		Stream file,
		IBuffer<T> pixels
	);

}
