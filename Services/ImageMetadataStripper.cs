namespace web.Services;

// Yüklenen görsellerden konum (GPS), cihaz ve yazılım bilgisi içeren üstverileri (EXIF/XMP/IPTC/metin parçaları) çıkarır.
// Piksel verisi yeniden kodlanmaz; JPEG'de yalnızca görüntü yönü (Orientation) korunur ki fotoğraflar yan dönmesin.
public static class ImageMetadataStripper
{
    // Dosya bozuksa null döner.
    public static byte[]? Strip(byte[] data, string ext) => ext switch
    {
        ".jpg" => TryRun(() => StripJpeg(data)),
        ".png" => TryRun(() => StripPng(data)),
        ".webp" => TryRun(() => StripWebp(data)),
        _ => null
    };

    private static byte[]? TryRun(Func<byte[]?> action)
    {
        try { return action(); }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or InvalidOperationException) { return null; }
    }

    private static byte[]? StripJpeg(byte[] d)
    {
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) return null;

        var output = new MemoryStream(d.Length);
        output.Write(d, 0, 2);
        ushort orientation = 1;
        var pos = 2;
        var reachedScan = false;

        while (pos + 4 <= d.Length)
        {
            if (d[pos] != 0xFF) return null;
            var marker = d[pos + 1];
            if (marker == 0xFF) { pos++; continue; } // dolgu baytı

            // Parametresiz işaretler
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                output.Write(d, pos, 2);
                pos += 2;
                continue;
            }
            if (marker == 0xD9) { output.Write(d, pos, 2); pos += 2; break; }

            var len = (d[pos + 2] << 8) | d[pos + 3];
            if (len < 2 || pos + 2 + len > d.Length) return null;
            var segmentEnd = pos + 2 + len;

            if (marker == 0xDA)
            {
                // Tarama verisi: sonuna kadar olduğu gibi kopyala.
                output.Write(d, pos, d.Length - pos);
                reachedScan = true;
                break;
            }

            if (marker == 0xE1 && len >= 8 && IsExifHeader(d, pos + 4))
            {
                if (orientation == 1) orientation = ReadOrientation(d, pos + 10, segmentEnd);
            }

            // APP1 (EXIF/XMP), APP12-15 (Ducky, IPTC/Photoshop vb.) ve yorumlar atılır; ICC profili (APP2) ve diğerleri kalır.
            var drop = marker is 0xE1 or 0xEC or 0xED or 0xEE or 0xEF or 0xFE;
            if (!drop) output.Write(d, pos, segmentEnd - pos);
            pos = segmentEnd;
        }

        if (!reachedScan && output.Length <= 2) return null;

        var result = output.ToArray();
        if (orientation is > 1 and <= 8) result = InsertOrientation(result, orientation);
        return result;
    }

    private static bool IsExifHeader(byte[] d, int p)
        => p + 6 <= d.Length && d[p] == 'E' && d[p + 1] == 'x' && d[p + 2] == 'i' && d[p + 3] == 'f' && d[p + 4] == 0 && d[p + 5] == 0;

    // tiffStart: "Exif\0\0" sonrasındaki TIFF başlığı.
    private static ushort ReadOrientation(byte[] d, int tiffStart, int end)
    {
        if (tiffStart + 8 > end) return 1;
        var little = d[tiffStart] == 'I' && d[tiffStart + 1] == 'I';
        if (!little && !(d[tiffStart] == 'M' && d[tiffStart + 1] == 'M')) return 1;

        ushort U16(int o) => little ? (ushort)(d[o] | (d[o + 1] << 8)) : (ushort)((d[o] << 8) | d[o + 1]);
        uint U32(int o) => little
            ? (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24))
            : (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);

        var ifd = tiffStart + (int)U32(tiffStart + 4);
        if (ifd < tiffStart || ifd + 2 > end) return 1;
        var count = U16(ifd);
        for (var i = 0; i < count; i++)
        {
            var entry = ifd + 2 + i * 12;
            if (entry + 12 > end) break;
            if (U16(entry) == 0x0112)
            {
                var value = U16(entry + 8);
                return value is >= 1 and <= 8 ? value : (ushort)1;
            }
        }
        return 1;
    }

    // Yalnızca Orientation içeren asgari bir EXIF bloğu SOI'nin hemen ardına eklenir.
    private static byte[] InsertOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] segment =
        {
            0xFF, 0xE1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // TIFF başlığı, IFD0 ofseti 8
            0x00, 0x01,                                              // 1 giriş
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,          // Orientation, SHORT, adet 1
            (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00                                   // sonraki IFD yok
        };
        var result = new byte[jpeg.Length + segment.Length];
        Buffer.BlockCopy(jpeg, 0, result, 0, 2);
        Buffer.BlockCopy(segment, 0, result, 2, segment.Length);
        Buffer.BlockCopy(jpeg, 2, result, 2 + segment.Length, jpeg.Length - 2);
        return result;
    }

    private static byte[]? StripPng(byte[] d)
    {
        byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (d.Length < 8 || !d.AsSpan(0, 8).SequenceEqual(sig)) return null;

        var output = new MemoryStream(d.Length);
        output.Write(d, 0, 8);
        var pos = 8;
        var sawEnd = false;

        while (pos + 12 <= d.Length)
        {
            var len = (int)(((uint)d[pos] << 24) | ((uint)d[pos + 1] << 16) | ((uint)d[pos + 2] << 8) | d[pos + 3]);
            if (len < 0 || pos + 12L + len > d.Length) return null;
            var type = System.Text.Encoding.ASCII.GetString(d, pos + 4, 4);
            var total = 12 + len;

            var drop = type is "eXIf" or "tEXt" or "zTXt" or "iTXt" or "tIME";
            if (!drop) output.Write(d, pos, total);
            pos += total;
            if (type == "IEND") { sawEnd = true; break; }
        }

        return sawEnd ? output.ToArray() : null;
    }

    private static byte[]? StripWebp(byte[] d)
    {
        if (d.Length < 12 || d[0] != 'R' || d[1] != 'I' || d[2] != 'F' || d[3] != 'F' || d[8] != 'W' || d[9] != 'E' || d[10] != 'B' || d[11] != 'P') return null;

        var output = new MemoryStream(d.Length);
        output.Write(d, 0, 12);
        var pos = 12;

        while (pos + 8 <= d.Length)
        {
            var tag = System.Text.Encoding.ASCII.GetString(d, pos, 4);
            var size = (long)((uint)d[pos + 4] | ((uint)d[pos + 5] << 8) | ((uint)d[pos + 6] << 16) | ((uint)d[pos + 7] << 24));
            var padded = size + (size & 1);
            if (pos + 8 + size > d.Length) return null;
            var chunkLen = (int)Math.Min(8 + padded, d.Length - pos);

            if (tag is "EXIF" or "XMP ")
            {
                pos += chunkLen;
                continue;
            }

            var start = (int)output.Position;
            output.Write(d, pos, chunkLen);
            if (tag == "VP8X" && size >= 1)
            {
                // Bayrak baytında EXIF (0x08) ve XMP (0x04) bitlerini temizle.
                var buf = output.GetBuffer();
                buf[start + 8] &= 0xF3;
            }
            pos += chunkLen;
        }

        var result = output.ToArray();
        var riffSize = result.Length - 8;
        result[4] = (byte)riffSize;
        result[5] = (byte)(riffSize >> 8);
        result[6] = (byte)(riffSize >> 16);
        result[7] = (byte)(riffSize >> 24);
        return result;
    }
}
