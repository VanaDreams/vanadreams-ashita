r"""Off-game test of the stock default handler's idle overlays (Lee, 22 Oct 2026, "i can see it
putting on movement gear underneath instead of the drop down i chose"):
- a slot the player picked in Idle holds against the Movement set that goes on while moving;
- unless the player also picked that slot in Movement;
- an explicit /vg idle speed still puts the Movement piece on.

    python addons/vanagear/tests/idle_picks.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
package.loaded['vg.lists'] = { BPBuff = {}, BPDebuff = {}, BPMagical = {}, EnfeebleReady = {}, MagicReady = {} }

-- what the handler equips: later EquipSet calls win slot by slot, as LuAshitacast's buffer does
local worn = {}
gFunc = { EquipSet = function(set) for slot, piece in pairs(set) do worn[slot] = piece end end }
local player = { Status = 'Idle', HPP = 100, MaxMP = 0, MPP = 100, IsMoving = false }
gData = { GetPlayer = function() return player end, GetPet = function() return nil end, GetPetAction = function() return nil end }
gVana = {
    Toggles = {},
    Sets = {
        Idle = { Feet = 'My Idle Boots', Head = 'Nyame Helm' },
        Movement = { Feet = 'Sprinter\'s Shoes' },
    },
    Own = { Idle = { Feet = true } },   -- the player picked the feet in Idle
}
local stock = require('vg.stock')
local out = {}
local function run(label)
    worn = {}
    stock.HandleDefault()
    out[#out + 1] = label .. ': ' .. tostring(worn.Feet)
end

run('standing')
player.IsMoving = true
run('moving, feet picked in Idle')
gVana.Own.Movement = { Feet = true }
run('moving, feet picked in both')
gVana.Own.Movement = nil
gVana.Toggles.Idle = 'speed'
run('/vg idle speed')
gVana.Toggles.Idle = nil
player.IsMoving = false
player.HPP = 40
gVana.Sets.Idle_Regen = { Feet = 'Regen Boots' }
run('low HP, feet picked in Idle')

-- resting: the built Resting set goes over Idle, the pick holds
player.HPP = 100
player.Status = 'Resting'
gVana.Sets.Resting = { Feet = 'Resting Boots' }
run('resting, feet picked in Idle')
player.Status = 'Idle'

-- an action's own set still goes on over everything (Lee: "they are supposed to flash in for
-- the abilities"): a weaponskill's built set beats a pick in Ws_Default
gVana.Sets.Ws_Default = { Neck = 'WS Neck', Feet = 'WS Boots' }
gVana.Sets.SavageBlade = { Neck = 'Savage Neck', Feet = 'Savage Boots' }
gVana.Own.Ws_Default = { Neck = true }
gData.GetAction = function() return { Name = 'Savage Blade' } end
worn = {}; stock.HandleWeaponskill()
out[#out + 1] = 'weaponskill: ' .. tostring(worn.Neck) .. ' / ' .. tostring(worn.Feet)
return table.concat(out, '\n')
""", str(ADDON))
print(result)
expect = [
    'standing: My Idle Boots',
    'moving, feet picked in Idle: My Idle Boots',
    "moving, feet picked in both: Sprinter's Shoes",
    "/vg idle speed: Sprinter's Shoes",
    'low HP, feet picked in Idle: My Idle Boots',
    'resting, feet picked in Idle: My Idle Boots',
    'weaponskill: Savage Neck / Savage Boots',
]
lines = result.split('\n')
assert lines == expect, '\n'.join(f'{a!r} vs {b!r}' for a, b in zip(lines, expect) if a != b)
print('PASS')
