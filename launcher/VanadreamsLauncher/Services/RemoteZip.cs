using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vanadreams.Services
{
    /// <summary>One file inside a zip on the server, from its central directory.</summary>
    public sealed class RemoteEntry
    {
        public string Name { get; set; }          // as stored, '/' separated
        public uint Crc { get; set; }
        public long Compressed { get; set; }
        public long Size { get; set; }
        public int Method { get; set; }           // 0 stored, 8 deflate
        public long Offset { get; set; }          // of its local header
    }

    /// <summary>
    /// A zip on the server read by byte ranges: its file list from the central directory, and any of its files without
    /// downloading the rest. Used to put back single game files out of the client package's base zips.
    /// </summary>
    public sealed class RemoteZip
    {
        private readonly HttpClient _http;
        public string Url { get; }
        public long Length { get; private set; }
        public List<RemoteEntry> Entries { get; } = new List<RemoteEntry>();

        private RemoteZip(HttpClient http, string url) { _http = http; Url = url; }

        public static async Task<RemoteZip> OpenAsync(HttpClient http, string url, CancellationToken ct)
        {
            var z = new RemoteZip(http, url);
            using (var head = new HttpRequestMessage(HttpMethod.Head, url))
            using (var r = await http.SendAsync(head, ct).ConfigureAwait(false))
            {
                r.EnsureSuccessStatusCode();
                z.Length = r.Content.Headers.ContentLength ?? throw new IOException("No length for " + url);
            }
            var tailLen = (int)Math.Min(z.Length, 66000);
            var tail = await z.RangeAsync(z.Length - tailLen, tailLen, ct).ConfigureAwait(false);
            var eocd = LastIndexOf(tail, new byte[] { 0x50, 0x4B, 0x05, 0x06 });
            if (eocd < 0) throw new InvalidDataException("Not a zip: " + url);
            long cdSize = BitConverter.ToUInt32(tail, eocd + 12), cdOffset = BitConverter.ToUInt32(tail, eocd + 16);
            if (cdOffset == 0xFFFFFFFF || cdSize == 0xFFFFFFFF)
            {
                var z64 = LastIndexOf(tail, new byte[] { 0x50, 0x4B, 0x06, 0x06 });
                if (z64 < 0) throw new InvalidDataException("Zip64 record missing: " + url);
                cdSize = (long)BitConverter.ToUInt64(tail, z64 + 40);
                cdOffset = (long)BitConverter.ToUInt64(tail, z64 + 48);
            }
            var cd = await z.RangeAsync(cdOffset, (int)cdSize, ct).ConfigureAwait(false);
            for (var p = 0; p + 46 <= cd.Length && BitConverter.ToUInt32(cd, p) == 0x02014B50;)
            {
                var e = new RemoteEntry
                {
                    Method = BitConverter.ToUInt16(cd, p + 10),
                    Crc = BitConverter.ToUInt32(cd, p + 16),
                    Compressed = BitConverter.ToUInt32(cd, p + 20),
                    Size = BitConverter.ToUInt32(cd, p + 24),
                    Offset = BitConverter.ToUInt32(cd, p + 42),
                };
                int n = BitConverter.ToUInt16(cd, p + 28), x = BitConverter.ToUInt16(cd, p + 30), c = BitConverter.ToUInt16(cd, p + 32);
                e.Name = Encoding.UTF8.GetString(cd, p + 46, n).Replace('\\', '/');
                ReadZip64(cd, p + 46 + n, x, e);
                if (!e.Name.EndsWith("/")) z.Entries.Add(e);
                p += 46 + n + x + c;
            }
            return z;
        }

        /// <summary>Sizes and offset that did not fit 32 bits are in the zip64 extra field, in this order.</summary>
        private static void ReadZip64(byte[] cd, int at, int length, RemoteEntry e)
        {
            for (var p = at; p + 4 <= at + length;)
            {
                int id = BitConverter.ToUInt16(cd, p), size = BitConverter.ToUInt16(cd, p + 2);
                if (id == 1)
                {
                    var q = p + 4;
                    if (e.Size == 0xFFFFFFFF) { e.Size = (long)BitConverter.ToUInt64(cd, q); q += 8; }
                    if (e.Compressed == 0xFFFFFFFF) { e.Compressed = (long)BitConverter.ToUInt64(cd, q); q += 8; }
                    if (e.Offset == 0xFFFFFFFF) e.Offset = (long)BitConverter.ToUInt64(cd, q);
                }
                p += 4 + size;
            }
        }

        private async Task<byte[]> RangeAsync(long from, int count, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, Url))
            {
                req.Headers.Range = new RangeHeaderValue(from, from + count - 1);
                using (var r = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
                {
                    r.EnsureSuccessStatusCode();
                    var bytes = await r.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (r.StatusCode != System.Net.HttpStatusCode.PartialContent || bytes.Length != count)
                        throw new IOException("The server did not send the part of " + Path.GetFileName(new Uri(Url).LocalPath) + " asked for.");
                    return bytes;
                }
            }
        }

        /// <summary>
        /// Fetch these files, a few requests for many files: entries close together in the zip come down in one range.
        /// Each is unpacked and checked against its CRC before it is handed over; a mismatch throws.
        /// </summary>
        public async Task FetchAsync(IEnumerable<RemoteEntry> wanted, Action<RemoteEntry, byte[]> each, CancellationToken ct, long maxSpan = 32L * 1024 * 1024)
        {
            var list = wanted.OrderBy(e => e.Offset).ToList();
            var i = 0;
            while (i < list.Count)
            {
                ct.ThrowIfCancellationRequested();
                var start = list[i].Offset;
                var end = EndOf(list[i]);
                var j = i + 1;
                while (j < list.Count && EndOf(list[j]) - start <= maxSpan && list[j].Offset - end < 1024 * 1024) { end = Math.Max(end, EndOf(list[j])); j++; }
                end = Math.Min(end, Length);
                var span = await RangeAsync(start, (int)(end - start), ct).ConfigureAwait(false);
                for (var k = i; k < j; k++) each(list[k], Unpack(list[k], span, (int)(list[k].Offset - start)));
                i = j;
            }
        }

        /// <summary>Room for the local header's name and extra field, which can differ from the central directory's.</summary>
        private static long EndOf(RemoteEntry e) => e.Offset + 30 + 1024 + e.Compressed;

        private static byte[] Unpack(RemoteEntry e, byte[] span, int at)
        {
            if (BitConverter.ToUInt32(span, at) != 0x04034B50) throw new InvalidDataException("Bad local header for " + e.Name);
            var data = at + 30 + BitConverter.ToUInt16(span, at + 26) + BitConverter.ToUInt16(span, at + 28);
            byte[] bytes;
            if (e.Method == 0) { bytes = new byte[e.Size]; Buffer.BlockCopy(span, data, bytes, 0, (int)e.Size); }
            else if (e.Method == 8)
            {
                using (var src = new MemoryStream(span, data, (int)e.Compressed))
                using (var inflate = new DeflateStream(src, CompressionMode.Decompress))
                using (var dst = new MemoryStream((int)e.Size)) { inflate.CopyTo(dst); bytes = dst.ToArray(); }
            }
            else throw new InvalidDataException(e.Name + " uses a compression the launcher cannot read (" + e.Method + ")");
            if (bytes.Length != e.Size || Crc32.Of(bytes) != e.Crc) throw new InvalidDataException(e.Name + " came down damaged.");
            return bytes;
        }

        private static int LastIndexOf(byte[] hay, byte[] needle)
        {
            for (var i = hay.Length - needle.Length; i >= 0; i--)
                if (hay[i] == needle[0] && hay[i + 1] == needle[1] && hay[i + 2] == needle[2] && hay[i + 3] == needle[3]) return i;
            return -1;
        }
    }

    /// <summary>The zip CRC-32, for checking a file on disk against the one in a zip.</summary>
    public static class Crc32
    {
        private static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
        {
            var c = (uint)n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            return c;
        }).ToArray();

        public static uint Of(byte[] data) => Update(0xFFFFFFFF, data, 0, data.Length) ^ 0xFFFFFFFF;

        public static uint Of(string path)
        {
            var crc = 0xFFFFFFFF;
            var buf = new byte[1 << 16];
            using (var f = File.OpenRead(path))
                for (int n; (n = f.Read(buf, 0, buf.Length)) > 0;) crc = Update(crc, buf, 0, n);
            return crc ^ 0xFFFFFFFF;
        }

        private static uint Update(uint crc, byte[] data, int at, int count)
        {
            for (var i = at; i < at + count; i++) crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
