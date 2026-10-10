"""Checks that every stat named in vg/roles.lua and jobs/*.lua is a server mod name.

    python addons/vanagear/tests/stat_names.py
"""
import re
from pathlib import Path

ADDON = Path(__file__).resolve().parent.parent
SERVER_ENUM = ADDON.parent.parent.parent / 'vanadreams' / 'scripts' / 'enum' / 'mod.codegen.lua'

names = set(re.findall(r'^\s*([A-Z0-9_]+)\s*=\s*\d+,', SERVER_ENUM.read_text(encoding='utf-8'), re.M))
names |= {'PDT', 'MDT'}
bad = 0
for f in [ADDON / 'vg' / 'roles.lua', *sorted((ADDON / 'jobs').glob('*.lua'))]:
    text = re.sub(r'--\[\[.*?\]\]--', '', f.read_text(encoding='utf-8'), flags=re.S)
    for word in sorted(set(re.findall(r'\b([A-Z][A-Z0-9_]{2,})\s*=', text))):
        if word not in names:
            bad += 1
            print(f'{f.name}: {word}')
print('all stat names known' if bad == 0 else f'{bad} unknown')
