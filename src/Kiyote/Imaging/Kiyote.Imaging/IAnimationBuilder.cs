using Kiyote.Buffers;

namespace Kiyote.Imaging;

public interface IAnimationBuilder : IDisposable {

	void AddFrame<T>(
		IBuffer<T> frame
	);

	void FinishAnimation();

}
