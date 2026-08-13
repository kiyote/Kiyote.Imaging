using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;
using Moq;

namespace Kiyote.Imaging.UnitTests;

[ExcludeFromCodeCoverage]
internal sealed class FakeBuffer<T> : IBuffer<T> {

	private readonly T[] _values;

	public FakeBuffer(
		int columns,
		int rows,
		T initialValue
	) {
		Columns = columns;
		Rows = rows;
		_values = new T[columns * rows];
		Array.Fill( _values, initialValue );
	}

	public int Columns { get; }

	public int Rows { get; }

	public T this[int x, int y] {
		get => _values[( y * Columns ) + x];
		set => _values[( y * Columns ) + x] = value;
	}

	public Span<T> GetRowSpan(
		int row
	) {
		return _values.AsSpan( row * Columns, Columns );
	}
}

[ExcludeFromCodeCoverage]
internal static class MockBuffer {

	public static IBuffer<T> Create<T>(
		int width,
		int height
	) {
		return new FakeBuffer<T>( width, height, default! );
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
			.Returns( ( int width, int height, T initialValue ) => new FakeBuffer<T>( width, height, initialValue ) );
	}
}
