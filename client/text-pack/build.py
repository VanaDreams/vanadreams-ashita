r"""Build the Vanadreams text pack: corrections to retail dialog lines, as an XIPivot overlay.

    python build.py <untouched FINAL FANTASY XI folder> <out folder> [<other pack .zip or overlay folder> ...]

Any further arguments are the other overlay packs (looks, title, hair, ...): the build stops if one of
them holds a file this pack holds, since only one overlay's copy of a file can win.

fixes.json beside this script lists each correction: the dialog file id (fid), the line number in it,
and the exact text to replace and its replacement. The script finds each file through the client's
VTABLE/FTABLE, changes only those lines, and checks that every other line stays byte for byte as it was.
A line whose old text is not there (a retail update changed it) stops the build.

fixes.json may also hold an "items" list: item strings (name, the two log names, description) in an item DAT.
Every byte of an item DAT is bit-rotated, so the build decodes the DAT, proves it re-encodes to the exact original
bytes, rewrites only the named strings in place (they must fit where the old ones sat, before the icon), and checks
that no other byte of the file changed.

The pack also carries the Rolanberry hangout home point (homepoint.py): the 67 zone event DATs whose home point
crystals list destinations gain "Rolanberry Fields #1", and zone 110's event DAT gains a home point event. That
part needs xi-tools; see homepoint.py.

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
PACK = 2  # bump for every rebuilt pack, so the launcher sees a new chunk
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


ITEM_ICON = 0x280  # the icon starts here in every record; strings must end before it
ROT_DEC = bytes(((b >> 5) | (b << 3)) & 0xFF for b in range(256))
ROT_ENC = bytes(((b << 5) | (b >> 3)) & 0xFF for b in range(256))


def edit_item(dec, fx):
    """Rewrite one item's strings inside the decoded item DAT (a bytearray). Returns the byte ranges it touched."""
    # records are not on a 0xC00 grid from the start of the file, so find the id, then confirm the string table
    sig, key = b"\x05\x00\x00\x00\x2c\x00\x00\x00", struct.pack("<I", fx["id"])
    hits, at = [], dec.find(key)
    while at >= 0:
        if dec.find(sig, at, at + 0x100) >= 0:
            hits.append(at)
        at = dec.find(key, at + 1)
    if len(hits) != 1:
        sys.exit("item %d: %d records carry that id" % (fx["id"], len(hits)))
    rec = hits[0]
    t = dec.find(sig, rec, rec + 0x100)
    offs = [struct.unpack_from("<I", dec, t + 4 + 8 * k)[0] for k in range(5)]
    # strings sit 0x1c bytes into their blocks: name, article flag, log singular, log plural, description
    starts = [t + o + 0x1C for o in offs]
    ends = [t + offs[k + 1] for k in range(4)] + [rec + ITEM_ICON]
    want = {0: (fx["old_name"], fx["new_name"]), 2: (fx["old_log"][0], fx["new_log"][0]),
            3: (fx["old_log"][1], fx["new_log"][1]), 4: (fx["old_desc"], fx["new_desc"])}
    touched = []
    for k, (old, new) in want.items():
        old, new = old.encode("ascii"), new.encode("ascii")
        a, b = starts[k], ends[k]
        if bytes(dec[a:b]).rstrip(b"\x00") != old:
            sys.exit("item %d string %d is %r, not %r (a retail update?)" % (fx["id"], k, bytes(dec[a:b]).rstrip(b"\x00"), old))
        if len(new) >= b - a:
            sys.exit("item %d string %d: %r needs %d bytes, only %d fit" % (fx["id"], k, new, len(new) + 1, b - a))
        dec[a:b] = new + b"\x00" * (b - a - len(new))
        touched.append((a, b))
    return touched


def pack_names(path):
    """The ROM paths another overlay pack holds: a zip's names, or the files under an overlay folder."""
    if os.path.isdir(path):
        out = set()
        for root, _, names in os.walk(path):
            for n in names:
                out.add(os.path.relpath(os.path.join(root, n), path).replace("\\", "/").upper())
        return out
    return {n.replace("\\", "/").upper() for n in zipfile.ZipFile(path).namelist() if not n.endswith("/")}


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    game, out, others = sys.argv[1], sys.argv[2], sys.argv[3:]
    both = json.load(open(os.path.join(HERE, "fixes.json"), encoding="utf-8"))
    fixes, item_fixes = both["fixes"], both.get("items", [])
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

    items = {}  # rel -> bytes of the rewritten item DAT
    for fx in item_fixes:
        rel = rom_path(game, fx["fid"])
        if rel not in items:
            raw = open(os.path.join(game, *rel.split("/")), "rb").read()
            if raw.translate(ROT_DEC).translate(ROT_ENC) != raw:
                sys.exit(rel + " does not re-encode byte for byte; not touching it")
            items[rel] = (raw, bytearray(raw.translate(ROT_DEC)), [])
        raw, dec, touched = items[rel]
        touched += edit_item(dec, fx)
        print("%s item %d: %s" % (rel, fx["id"], fx["new_name"]))
    for rel, (raw, dec, touched) in items.items():
        keep = bytearray(dec)
        orig = raw.translate(ROT_DEC)
        for a, b in touched:
            keep[a:b] = orig[a:b]
        if bytes(keep) != orig:
            sys.exit(rel + ": a byte outside the edited strings changed")
        items[rel] = bytes(dec.translate(ROT_ENC))

    # the Rolanberry hangout home point: whole event DATs, proven inside homepoint.build()
    import homepoint
    events, hp_report = homepoint.build(game)
    clash = set(events) & (set(files) | set(items))
    if clash:
        sys.exit("an event DAT is also a dialog or item file: %s" % sorted(clash))
    print("home point: %d event DATs, %d blocks patched, new block %s in %s"
          % (len(events), len(hp_report["blocks"]), hp_report["zone110"]["actor_id"], hp_report["zone110"]["rel"]))

    ours = {rel.upper() for rel in list(files) + list(items) + list(events)}
    for other in others:
        both = ours & pack_names(other)
        if both:
            sys.exit("%s also holds %s; only one overlay's copy would load" % (other, sorted(both)[:10]))
        print("no shared files with", other)

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
        for rel in sorted(items):
            info = zipfile.ZipInfo(rel, date_time=(2026, 10, 16, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, items[rel])
            unpacked += len(items[rel])
        for rel in sorted(events):
            info = zipfile.ZipInfo(rel, date_time=(2026, 10, 16, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, events[rel])
            unpacked += len(events[rel])
    blob = open(zpath, "rb").read()
    entry = {"name": name, "size": len(blob), "sha256": hashlib.sha256(blob).hexdigest(),
             "files": len(files) + len(items) + len(events),
             "unpacked": unpacked, "dest": DEST}
    json.dump(entry, open(os.path.join(out, name.replace(".zip", ".chunk.json")), "w"))
    print(json.dumps(entry))


if __name__ == "__main__":
    main()
