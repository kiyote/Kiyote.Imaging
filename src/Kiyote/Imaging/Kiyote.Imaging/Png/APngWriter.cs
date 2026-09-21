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

		Stream stream = _fileSystem.File.Create( filePath );
		return StartAnimation( stream, frameDelay, loopCount, ownsStream: true );
	}

	IAnimationBuilder IAnimationWriter.StartAnimation(
		Stream stream,
		TimeSpan frameDelay,
		int loopCount
	) {
		return StartAnimation( stream, frameDelay, loopCount, ownsStream: false );
	}

	private static IAnimationBuilder StartAnimation(
		Stream stream,
		TimeSpan frameDelay,
		int loopCount,
		bool ownsStream
	) {
		ArgumentNullException.ThrowIfNull( stream );
		ArgumentOutOfRangeException.ThrowIfNegative( loopCount );

		return new APngAnimationBuilder( stream, frameDelay, loopCount, ownsStream );
	}
}
