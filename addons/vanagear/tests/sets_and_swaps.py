r"""Off-game test of three vanagear behaviours (Ferrin, 18 Oct 2026):
- a slot you change from the equipment menu is left alone until the next zone;
- the fighting set answers to "Engaged" (and the window lists it so);
- /vg takeover copies a LuAshitacast profile's sets into your own vanagear sets.

    python addons/vanagear/tests/sets_and_swaps.py
"""
import tempfile
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent
TMP = tempfile.mkdtemp()

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path, tmp = ...
package.path = addon_path .. '/?.lua;' .. package.path
bit = require('bit')
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
ashita = { fs = { exists = function() return true end, create_directory = function() end } }
package.loaded['vg.data'] = { ModId = function() return nil end, SlotBit = {}, Items = function() return {} end, PetTypes = {} }
package.preload['encoding'] = function() return { ShiftJIS_To_UTF8 = function(_, s) return s end } end
AshitaCore = { GetInstallPath = function() return tmp .. '/' end }
gData = { Constants = { EquipSlotNames = { 'Main', 'Sub', 'Range', 'Ammo', 'Head', 'Body', 'Hands', 'Legs', 'Feet',
                                           'Neck', 'Waist', 'Ear1', 'Ear2', 'Ring1', 'Ring2', 'Back' } } }
gState = { Disabled = {} }

local core = require('vg.core')
gVana.Who = { JobName = 'SAM' }
local mine = require('vg.mine')
local out = {}

-- the menu puts a cape on (slot 16), then the player zones
core.HoldByHand(16)
out[#out + 1] = 'held ' .. tostring(gState.Disabled[16])
-- a slot the player locked with /lac disable stays locked through the zone
gState.Disabled[5] = true
core.HoldByHand(5)
core.ZoneIn()
out[#out + 1] = 'after zone ' .. tostring(gState.Disabled[16]) .. ' ' .. tostring(gState.Disabled[5])

out[#out + 1] = 'names ' .. mine.SetName('Engaged') .. ' ' .. mine.SetName('TP') .. ' ' .. mine.Label('Tp_Default') .. ' ' .. mine.SetName('Seigan')

local count = mine.ImportProfile({
    Engaged = { Head = 'Flamma Zucchetto +2', Back = { Name = "Takaha Mantle", Augment = 'STR+5' } },
    ['Idle'] = { Body = 'Sacro Breastplate' },
    ['Tachi: Fudo'] = { Neck = 'Samurai\'s Nodowa +2' },
    Empty = {},
})
local sets = mine.Sets()
out[#out + 1] = 'imported ' .. count .. ' ' .. tostring(sets.Tp_Default and sets.Tp_Default.Head) .. ' / '
    .. tostring(sets.Tp_Default and sets.Tp_Default.Back) .. ' / ' .. tostring(sets.Idle and sets.Idle.Body) .. ' / '
    .. tostring(sets.TachiFudo and sets.TachiFudo.Neck) .. ' / ' .. tostring(sets.Empty)
-- and the built Tp_Default gets them laid over it
local merged = mine.Overlay({ Tp_Default = { Head = 'Built Helm', Body = 'Built Body' } })
out[#out + 1] = 'overlay ' .. merged.Tp_Default.Head .. ' / ' .. merged.Tp_Default.Body
return table.concat(out, '\n')
""", str(ADDON), TMP)
print(result)
expect = [
    'held true',
    'after zone false true',
    'names Tp_Default Tp_Default Engaged Seigan',
    "imported 3 Flamma Zucchetto +2 / Takaha Mantle / Sacro Breastplate / Samurai's Nodowa +2 / nil",
    'overlay Flamma Zucchetto +2 / Built Body',
]
lines = result.split('\n')
assert lines == expect, '\n'.join(f'{a!r} vs {b!r}' for a, b in zip(lines, expect) if a != b)
print('PASS')
