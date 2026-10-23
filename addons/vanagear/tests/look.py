r"""Off-game test of the look lock (Lee, 22 Oct 2026, "a box to style lock the gear set, and stop all
the animations"):
- SetLook(name) remembers the set per job, saves prefs and lockstyles that set;
- a rebuild puts the look back;
- SetLook(nil) forgets it and sends /lockstyle off.

    python addons/vanagear/tests/look.py
"""
import tempfile
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent
TMP = tempfile.mkdtemp()
# the addon asks Ashita to make this folder; the stand-in does not
(Path(TMP) / 'config' / 'addons' / 'vanagear').mkdir(parents=True)

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path, tmp = ...
package.path = addon_path .. '/?.lua;' .. package.path
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
ashita = { fs = { exists = function() return true end, create_directory = function() end } }
package.loaded['vg.data'] = { ModId = function() return nil end, SlotBit = {}, Items = function() return {} end, PetTypes = {} }
package.preload['encoding'] = function() return { ShiftJIS_To_UTF8 = function(_, s) return s end } end
local sent, locked = {}, {}
AshitaCore = {
    GetInstallPath = function() return tmp .. '/' end,
    GetChatManager = function() return { QueueCommand = function(_, _, line) sent[#sent + 1] = line end } end,
}
gFunc = { LockStyle = function(set) locked[#locked + 1] = set.Feet end }
gData = { Constants = { EquipSlotNames = { 'Main', 'Sub', 'Range', 'Ammo', 'Head', 'Body', 'Hands', 'Legs', 'Feet',
                                           'Neck', 'Waist', 'Ear1', 'Ear2', 'Ring1', 'Ring2', 'Back' } } }
gState = { Disabled = {} }
local core = require('vg.core')
gVana.Who = { JobName = 'DNC' }
gVana.Sets = { Idle = { Feet = 'Idle Boots' }, Movement = { Feet = 'Sprinter\'s Shoes' } }
local out = {}
out[#out + 1] = 'set ' .. tostring(core.SetLook('Idle')) .. ' ' .. tostring(core.LookSet()) .. ' ' .. tostring(locked[#locked])
-- the prefs file carries it
local chunk = loadfile(tmp .. '/config\\addons\\vanagear\\prefs.lua')
local saved = chunk and chunk()
out[#out + 1] = 'saved ' .. tostring(saved and saved.Look and saved.Look.DNC)
-- a rebuild puts it back
gVana.Sets.Idle = { Feet = 'New Idle Boots' }
core.ApplyLook()
out[#out + 1] = 'reapplied ' .. tostring(locked[#locked])
-- off
core.SetLook(nil)
out[#out + 1] = 'off ' .. tostring(core.LookSet()) .. ' ' .. tostring(sent[#sent])
-- a set that does not exist cannot be the look
out[#out + 1] = 'missing ' .. tostring(core.SetLook('NoSuchSet'))
return table.concat(out, '\n')
""", str(ADDON), TMP)
print(result)
expect = [
    'set true Idle Idle Boots',
    'saved Idle',
    'reapplied New Idle Boots',
    'off nil /lockstyle off',
    'missing false',
]
lines = result.split('\n')
assert lines == expect, '\n'.join(f'{a!r} vs {b!r}' for a, b in zip(lines, expect) if a != b)
print('PASS')
