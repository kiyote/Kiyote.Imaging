using Kiyote.Buffers;

namespace Kiyote.Imaging;

public interface IImageWriter {

	void WriteImage<T>(
		string filePath,
		IBuffer<T> pixels
	);

}
