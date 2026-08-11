using System.IO.Abstractions;
using Kiyote.Buffers;
using Kiyote.Imaging.Png;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Imaging;

public static class ExtensionMethods {

	public static IServiceCollection AddPngImaging(
		this IServiceCollection services
	) {
		ArgumentNullException.ThrowIfNull( services );

		return services
			.AddBuffers()
			.AddSingleton<IFileSystem, FileSystem>()
			.AddSingleton<IImageWriter, PngWriter>()
			.AddSingleton<IImageReader, PngReader>();
	}
}
