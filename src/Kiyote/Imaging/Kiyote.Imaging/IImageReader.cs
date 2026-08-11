using Kiyote.Buffers;

namespace Kiyote.Imaging;

public interface IImageReader {

	IBuffer<T> ReadImage<T>(
		string filePath
	);

}
