using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;

namespace Kiyote.Imaging;

[ExcludeFromCodeCoverage]
internal sealed class ArrayBufferFactory : IBufferFactory {

	IBuffer<T> IBufferFactory.Create<T>(
		int columns,
		int rows,
		T initialValue
	) {
		return new ArrayBuffer<T>( columns, rows, initialValue );
	}

}
