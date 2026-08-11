namespace Kiyote.Imaging.Png;

internal static class PngCrc {

	private static readonly uint[] _crcTable = BuildCrcTable();

	public static uint Update(
		uint crc,
		ReadOnlySpan<byte> data
	) {
		for( int i = 0; i < data.Length; i++ ) {
			crc = _crcTable[( crc ^ data[i] ) & 0xFF] ^ ( crc >> 8 );
		}
		return crc;
	}

	public static uint Compute(
		ReadOnlySpan<byte> data
	) {
		return Update( 0xFFFFFFFFU, data ) ^ 0xFFFFFFFFU;
	}

	private static uint[] BuildCrcTable() {
		uint[] table = new uint[256];
		for( uint n = 0; n < 256; n++ ) {
			uint c = n;
			for( int k = 0; k < 8; k++ ) {
				c = ( c & 1 ) == 1 ? 0xEDB88320U ^ ( c >> 1 ) : c >> 1;
			}
			table[n] = c;
		}
		return table;
	}
}
