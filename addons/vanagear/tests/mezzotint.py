r"""Off-game test of mezzotint (Delve) augment scoring in vg\bags.lua, against the server's own rules
(src/map/items/item_equipment.cpp ApplyMezzotint): value shown is the stored value plus one, an _II
index adds 256, damage taken is in hundredths, the pet ones go to every pet.

    python addons/vanagear/tests/mezzotint.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
bit = require('bit')
function T(t) return t end
struct = { unpack = function(fmt, s, pos) return s:byte(pos) end }
package.preload['encoding'] = function() return {} end
local mods = dofile(addon_path .. '/data/mods.lua')
local byName = {}
for id, name in pairs(mods) do byName[name] = id end
package.loaded['vg.data'] = { ModId = function(n) return byName[n] end, Augments = function() return {} end }
local bags = require('vg.bags')

-- kind 2, mezzotint flag, rank, padding, RP (2 bytes), then three slots of index and value
local extra = string.char(2, 0x23, 5, 0, 0, 0, 12, 9, 188, 4, 131, 14, 0, 0, 0, 0)
local r = bags.MezzotintMods(extra)
local out = {}
local function check(label, ok) out[#out + 1] = (ok and 'ok   ' or 'FAIL ') .. label end
check('Accuracy+10 (stored 9)', r.mods[byName.ACC] == 10)
check('Physical damage taken -5% (stored 4, in hundredths)', r.mods[byName.DMGPHYS] == -500)
check('Pet: Accuracy+15 (stored 14) for every pet', r.pet[0] and r.pet[0][byName.ACC] == 15)
local two = bags.MezzotintMods(string.char(2, 0x23, 5, 0, 0, 0, 3, 9, 0, 0, 0, 0, 0, 0, 0, 0))
check('HP II: 256 + 10', two.mods[byName.HP] == 266)
local none = bags.MezzotintMods(string.char(2, 0x23, 5, 0, 0, 0, 250, 9, 0, 0, 0, 0, 0, 0, 0, 0))
check('an index the server has no stat for adds nothing', next(none.mods) == nil)
return table.concat(out, '\n')
""", str(ADDON))
print(result)
print('PASS' if 'FAIL' not in result else 'FAILED')
