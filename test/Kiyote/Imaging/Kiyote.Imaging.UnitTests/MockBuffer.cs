using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;
using Moq;

namespace Kiyote.Imaging.UnitTests;

[ExcludeFromCodeCoverage]
internal static class MockBuffer {

	public static IBuffer<T> Create<T>(
		int width,
		int height
	) {
		T[] values = new T[width * height];
		Mock<IBuffer<T>> buffer = new Mock<IBuffer<T>>();
		_ = buffer.SetupGet( b => b.Columns ).Returns( width );
		_ = buffer.SetupGet( b => b.Rows ).Returns( height );
		_ = buffer
			.Setup( b => b[It.IsAny<int>(), It.IsAny<int>()] )
			.Returns( ( int x, int y ) => values[( y * width ) + x] );
		buffer
			.SetupSet( b => b[It.IsAny<int>(), It.IsAny<int>()] = It.IsAny<T>() )
			.Callback( ( int x, int y, T value ) => values[( y * width ) + x] = value );

		return buffer.Object;
	}
}

[ExcludeFromCodeCoverage]
internal static class MockBufferFactory {

	public static IBufferFactory Create() {
		Mock<IBufferFactory> factory = new Mock<IBufferFactory>();
		Setup<uint>( factory );
		Setup<int>( factory );
		Setup<bool>( factory );
		Setup<byte>( factory );
		return factory.Object;
	}

	private static void Setup<T>(
		Mock<IBufferFactory> factory
	) {
		_ = factory
			.Setup( f => f.Create( It.IsAny<int>(), It.IsAny<int>(), It.IsAny<T>() ) )
			.Returns( ( int width, int height, T initialValue ) => {
				IBuffer<T> buffer = MockBuffer.Create<T>( width, height );
				for( int y = 0; y < height; y++ ) {
					for( int x = 0; x < width; x++ ) {
						buffer[x, y] = initialValue;
					}
				}
				return buffer;
			} );
	}
}
