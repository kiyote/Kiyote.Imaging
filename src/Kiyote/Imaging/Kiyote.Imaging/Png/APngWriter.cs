using System.IO.Abstractions;

namespace Kiyote.Imaging.Png;

public sealed class APngWriter : IAnimationWriter {

	private readonly IFileSystem _fileSystem;

	public APngWriter(
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

		return new APngAnimationBuilder( _fileSystem, filePath, frameDelay, loopCount );
	}
}
