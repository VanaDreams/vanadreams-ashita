r"""Append an extra zip (e.g. the hair colour pack) to an existing client manifest.

    python add_chunk.py <manifest.json> <zip> [--dest FOLDER]

Computes size, sha256, file count and unpacked size from the zip and adds (or replaces) its entry in
the manifest's chunks, so a pack made after the client was packed reaches every player on their next
Install/Patch without re-packing the whole game. Upload the zip next to manifest.json afterwards.
"""

import argparse
import hashlib
import json
import os
import zipfile


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("manifest")
    ap.add_argument("zip")
    ap.add_argument("--dest", help="folder under the game install (default: the game folder itself)")
    a = ap.parse_args()

    with zipfile.ZipFile(a.zip) as z:
        infos = [i for i in z.infolist() if not i.is_dir()]
    h = hashlib.sha256()
    with open(a.zip, "rb") as f:
        while block := f.read(1 << 20):
            h.update(block)
    chunk = {
        "name": os.path.basename(a.zip),
        "size": os.path.getsize(a.zip),
        "sha256": h.hexdigest(),
        "files": len(infos),
        "unpacked": sum(i.file_size for i in infos),
    }
    if a.dest:
        chunk["dest"] = a.dest

    with open(a.manifest, encoding="utf-8") as f:
        m = json.load(f)
    m["chunks"] = [c for c in m["chunks"] if c["name"] != chunk["name"]] + [chunk]
    m["total_size"] = sum(c["size"] for c in m["chunks"])
    m["total_unpacked"] = sum(c["unpacked"] for c in m["chunks"])
    with open(a.manifest, "w", encoding="utf-8") as f:
        json.dump(m, f, indent=2)
    print(json.dumps(chunk))


if __name__ == "__main__":
    main()
