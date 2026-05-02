namespace DIndex.Core.Storage.Serialization;

public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint c = i;

            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;

            table[i] = c;
        }

        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFF_FFFFu;

        foreach (byte b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFF_FFFFu;
    }
}
