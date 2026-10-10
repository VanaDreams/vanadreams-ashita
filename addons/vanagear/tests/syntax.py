"""Compiles every Lua file in the addon under LuaJIT, without running it.

    python addons/vanagear/tests/syntax.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime()
check = L.eval('function(path) local f, err = loadfile(path) return err end')
bad = 0
for path in sorted(ADDON.rglob('*.lua')):
    err = check(str(path))
    if err:
        bad += 1
        print('FAIL', path.relative_to(ADDON), err)
print('all compile' if bad == 0 else f'{bad} failed')
