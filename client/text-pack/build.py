r"""Build the Vanadreams text pack: corrections to retail dialog lines, as an XIPivot overlay.

    python build.py <untouched FINAL FANTASY XI folder> <out folder>

fixes.json beside this script lists each correction: the dialog file id (fid), the line number in it,
and the exact text to replace and its replacement. The script finds each file through the client's
VTABLE/FTABLE, changes only those lines, and checks that every other line stays byte for byte as it was.
A line whose old text is not there (a retail update changed it) stops the build.

Writes <out>/vanadreams-text-NN.zip (the changed ROM files) and its manifest entry
<out>/vanadreams-text-NN.chunk.json, whose dest is the vanadreams-text overlay folder.

Dialog DAT: u32 (data size ^ 0x10000000), then the data with every byte XOR 0x80. The data starts with a
u32 offset table (first offset / 4 = line count), then the lines. Events point at lines by number, not by
offset, so a line may change length: the offsets after it move by the difference.

The built DATs hold Square Enix text, so they are not committed here; only the script and the fixes are.
Bump PACK for every new pack, upload the zip and append its .chunk.json entry as the last chunk.
"""

import hashlib
import json
import os
import struct
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
PACK = 1  # bump for every rebuilt pack, so the launcher sees a new chunk
DEST = "{ashita}/polplugins/DATs/vanadreams-text"


def rom_path(game, fid):
    """The ROM file for a file id, as 'ROM/25/41.DAT' (VTABLE says which ROM, FTABLE where in it)."""
    for n in range(1, 10):
        rom = "ROM" if n == 1 else "ROM%d" % n
        v = os.path.join(game, "VTABLE.DAT") if n == 1 else os.path.join(game, rom, "VTABLE%d.DAT" % n)
        f = os.path.join(game, "FTABLE.DAT") if n == 1 else os.path.join(game, rom, "FTABLE%d.DAT" % n)
        if not os.path.exists(v):
            continue
        vt = open(v, "rb").read()
        if fid < len(vt) and vt[fid]:
            x = struct.unpack_from("<H", open(f, "rb").read(), fid * 2)[0]
            return "%s/%d/%d.DAT" % (rom, x >> 7, x & 0x7F)
    sys.exit("file id %d is in no VTABLE" % fid)


def read_dialog(raw):
    if struct.unpack_from("<I", raw, 0)[0] != (len(raw) - 4) ^ 0x10000000:
        sys.exit("not a dialog DAT (size header does not match)")
    d = bytes(b ^ 0x80 for b in raw[4:])
    n = struct.unpack_from("<I", d, 0)[0] // 4
    offs = list(struct.unpack_from("<%dI" % n, d, 0)) + [len(d)]
    return [d[offs[i]:offs[i + 1]] for i in range(n)]


def write_dialog(lines):
    table, body, at = [], [], 4 * len(lines)
    for s in lines:
        table.append(at)
        body.append(s)
        at += len(s)
    d = struct.pack("<%dI" % len(lines), *table) + b"".join(body)
    return struct.pack("<I", len(d) ^ 0x10000000) + bytes(b ^ 0x80 for b in d)


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    game, out = sys.argv[1], sys.argv[2]
    fixes = json.load(open(os.path.join(HERE, "fixes.json"), encoding="utf-8"))["fixes"]
    files = {}
    for fx in fixes:
        rel = rom_path(game, fx["fid"])
        if rel not in files:
            raw = open(os.path.join(game, *rel.split("/")), "rb").read()
            if write_dialog(read_dialog(raw)) != raw:
                sys.exit(rel + " does not read back byte for byte; not touching it")
            files[rel] = (read_dialog(raw), list(read_dialog(raw)))
        orig, lines = files[rel]
        old, new = fx["old"].encode("ascii"), fx["new"].encode("ascii")
        line = lines[fx["line"]]
        if line.count(old) != 1:
            sys.exit("%s line %d no longer holds %r (a retail update?)" % (rel, fx["line"], fx["old"]))
        lines[fx["line"]] = line.replace(old, new)
        print("%s line %d: %s" % (rel, fx["line"], fx["new"]))

    os.makedirs(out, exist_ok=True)
    name = "vanadreams-text-%02d.zip" % PACK
    zpath = os.path.join(out, name)
    unpacked = 0
    with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for rel in sorted(files):
            orig, lines = files[rel]
            changed = {i for i in range(len(orig)) if orig[i] != lines[i]}
            blob = write_dialog(lines)
            again = read_dialog(blob)
            if len(again) != len(orig) or any(again[i] != orig[i] for i in range(len(orig)) if i not in changed):
                sys.exit(rel + ": a line outside the fixes changed")
            info = zipfile.ZipInfo(rel, date_time=(2026, 10, 16, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, blob)
            unpacked += len(blob)
    blob = open(zpath, "rb").read()
    entry = {"name": name, "size": len(blob), "sha256": hashlib.sha256(blob).hexdigest(), "files": len(files),
             "unpacked": unpacked, "dest": DEST}
    json.dump(entry, open(os.path.join(out, name.replace(".zip", ".chunk.json")), "w"))
    print(json.dumps(entry))


if __name__ == "__main__":
    main()
