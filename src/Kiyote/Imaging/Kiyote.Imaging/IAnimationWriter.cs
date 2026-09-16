namespace Kiyote.Imaging;

public interface IAnimationWriter {

	IAnimationBuilder StartAnimation(
		string filePath,
		TimeSpan frameDelay,
		int loopCount = 0
	);

}
