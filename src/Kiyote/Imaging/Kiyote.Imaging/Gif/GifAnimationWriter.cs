using System.IO.Abstractions;

namespace Kiyote.Imaging.Gif;

public sealed class GifAnimationWriter : IAnimationWriter {

	private readonly IFileSystem _fileSystem;

	public GifAnimationWriter(
		IFileSystem fileSystem
	) {
		ArgumentNullException.ThrowIfNull( fileSystem );
		_fileSystem = fileSystem;
	}

	IAnimationBuilder IAnimationWriter.StartAnimation(
		string filePath,
		TimeSpan frameDelay,
		int loopCount
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( filePath );
		ArgumentOutOfRangeException.ThrowIfNegative( loopCount );

		return new GifAnimationBuilder( _fileSystem, filePath, frameDelay, loopCount );
	}
}
