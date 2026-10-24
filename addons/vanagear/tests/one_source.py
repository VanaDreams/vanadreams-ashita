r"""Off-game test that the profile file is the one source of what vanagear swaps (Lee, 24 Oct 2026: "lets
make sure that its no longer going to conflict with itself because it was saving stuff in memory to swap
and the written luas would conflict and cause glitching"):
- the window's Wear it puts the set on once, read from the profile, and nothing holds it after: the
  profile's next swap is the profile's own;
- /vg wear is the explicit hold, and /vg wear off ends it;
- a set changed in the profile is the one Wear it puts on (no copy kept from before).

    python addons/vanagear/tests/one_source.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
bit = require('bit')
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
ashita = { fs = { exists = function() return true end, create_directory = function() end } }
package.preload['encoding'] = function() return { ShiftJIS_To_UTF8 = function(_, s) return s end } end
AshitaCore = { GetInstallPath = function() return addon_path .. '/' end,
               GetChatManager = function() return { QueueCommand = function() end } end }
gState = { Disabled = {} }
gData = { Constants = { EquipSlotNames = {} }, GetEquipment = function() return {} end }
local forced, held = {}, {}
gFunc = {
    ForceEquipSet = function(set) forced[#forced + 1] = set end,
    EquipSet = function(set) held[#held + 1] = set end,
    ForceEquip = function() end, Equip = function() end,
}
require('vg.core')
local use = require('vg.use')

local out = {}
local function check(label, ok) out[#out + 1] = (ok and 'ok   ' or 'FAIL ') .. label end

-- the running profile's sets, as vanagear's Refresh fills them from the file
gProfile = { Sets = { Idle = { Body = 'File Body' }, Tp_Default = { Body = 'Fighting Body' } } }

check('Wear it puts the set on', use.WearOnce('Idle', { Body = 'Stale Copy' }) and forced[1] == gProfile.Sets.Idle)
check('Wear it reads the profile, not a copy handed in', forced[1].Body == 'File Body')
use.Tick('HandleDefault'); use.Tick('HandleAbility'); use.Tick('HandleItem')
check('nothing held after Wear it', #held == 0 and use.Worn() == nil)

-- the file changes (a drop-down, Save it): Refresh puts the new table in place
gProfile.Sets.Idle = { Body = 'New File Body' }
use.WearOnce('Idle')
check('Wear it after a change wears the changed set', forced[2].Body == 'New File Body')

-- /vg wear: the explicit hold, and off
use.Wear('Idle', gProfile.Sets.Idle)
use.Tick('HandleDefault')
check('/vg wear holds over the next pass', #held == 1 and held[1].Body == 'New File Body')
use.Wear(nil)
use.Tick('HandleDefault')
check('/vg wear off: nothing held', #held == 1 and use.Worn() == nil)

return table.concat(out, '\n')
""", str(ADDON))
print(result)
assert 'FAIL' not in result
print('PASS')
