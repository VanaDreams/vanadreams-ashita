#!/usr/bin/env python3
"""Copy the shared Vanadreams window style (addons/_shared/vdstyle.lua) into every addon that uses it.

    python scripts/sync-vdstyle.py                 # copy into this repo's addons
    python scripts/sync-vdstyle.py ../vanatunes/vanatunes ../grampsshop/grampsshop   # and other repos' addon folders
    python scripts/sync-vdstyle.py --check [...]   # exit 1 if any copy differs (for CI or before a release)

The launcher installs each addon folder on its own, so each one carries its own copy.
"""
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'addons' / '_shared' / 'vdstyle.lua'
ADDONS = ['vanagear', 'crafty', 'vanafish', 'dreamyah']


def main(argv):
    check = '--check' in argv
    targets = [ROOT / 'addons' / a for a in ADDONS] + [pathlib.Path(p).resolve() for p in argv if p != '--check']
    want = SOURCE.read_bytes()
    stale = []
    for folder in targets:
        dest = folder / 'vdstyle.lua'
        if dest.exists() and dest.read_bytes() == want:
            continue
        if check:
            stale.append(dest)
        else:
            dest.write_bytes(want)
            print('updated', dest)
    for dest in stale:
        print('differs from addons/_shared/vdstyle.lua:', dest)
    return 1 if stale else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
