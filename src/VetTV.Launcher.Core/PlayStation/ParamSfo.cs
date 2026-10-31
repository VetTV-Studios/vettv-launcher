using System.Buffers.Binary;
using System.Text;
namespace VetTV.Launcher.Core.PlayStation;
/// Reads PS3 PARAM.SFO (PSF) key/values: TITLE, TITLE_ID, CATEGORY. Metadata only; no decryption.
public static class ParamSfo
{
    public static Dictionary<string, string> Parse(ReadOnlySpan<byte> d)
    {
        if (d.Length < 20 || d[0] != 0 || d[1] != (byte)'P' || d[2] != (byte)'S' || d[3] != (byte)'F') throw new InvalidDataException("Not a PARAM.SFO");
        int keyTable = BinaryPrimitives.ReadInt32LittleEndian(d[8..]);
        int dataTable = BinaryPrimitives.ReadInt32LittleEndian(d[12..]);
        int count = BinaryPrimitives.ReadInt32LittleEndian(d[16..]);
        if (count is < 0 or > 1024) throw new InvalidDataException("Bad entry count");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            var e = d.Slice(20 + i * 16, 16);
            int keyOff = BinaryPrimitives.ReadUInt16LittleEndian(e);
            ushort fmt = BinaryPrimitives.ReadUInt16LittleEndian(e[2..]);
            int len = BinaryPrimitives.ReadInt32LittleEndian(e[4..]);
            int dataOff = BinaryPrimitives.ReadInt32LittleEndian(e[12..]);
            var keySpan = d[(keyTable + keyOff)..]; var key = Encoding.UTF8.GetString(keySpan[..keySpan.IndexOf((byte)0)]);
            var val = d.Slice(dataTable + dataOff, len);
            result[key] = fmt == 0x0404 ? BinaryPrimitives.ReadUInt32LittleEndian(val).ToString() : Encoding.UTF8.GetString(val).TrimEnd('\0');
        }
        return result;
    }
}
