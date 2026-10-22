r"""Off-game test of vg\core.lua's LuAshitacast mode and its watch for a second LuAshitacast.

LuAshitacast mode (PureLac) is on only for a profile of the player's own with sets of its own and the
underlay off. The watch stands vanagear down after injected equip packets it did not send, on three or
more slots, and never for its own packets or for vanafish's rod and bait swaps (range and ammo).

    python addons/vanagear/tests/lac_mode.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
printed = {}
print = function(s) printed[#printed + 1] = tostring(s) end
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
for _, m in ipairs({ 'vg.bags', 'vg.plan', 'vg.score', 'vg.data' }) do
    package.preload[m] = function() return {} end
end
AshitaCore = { GetInstallPath = function() return '' end }

local function fresh()
    package.loaded['vg.core'] = nil
    return require('vg.core')
end

local out = {}
local function check(name, got, want)
    if got ~= want then out[#out + 1] = name .. ': got ' .. tostring(got) .. ', want ' .. tostring(want) end
end

-- LuAshitacast mode
local core = fresh()
gVana.Who = { JobName = 'WAR' }
gProfile = nil
check('no profile', core.PureLac(), false)
gProfile = { IsVanagearStock = true, Sets = {} }
check('vanagear stock profile', core.PureLac(), false)
gProfile = { ImportedFromGearSwap = true, Sets = { Idle = { Head = 'x' } } }
check('GearSwap import', core.PureLac(), false)
gProfile = { Sets = { Idle = { Head = 'Nyame Helm' } } }
check('own profile with sets', core.PureLac(), true)
gVana.Prefs.Underlay.WAR = true
check('own profile, underlay on', core.PureLac(), false)
gVana.Prefs.Underlay.WAR = nil
gProfile = { Sets = { Idle = {} } }
check('own profile, empty sets', core.PureLac(), false)

-- vanagear's own packets never count
core = fresh()
for _ = 1, 4 do core.MarkOwnEquip() end
core.SawInjectedEquip({ 4 }); core.SawInjectedEquip({ 5 }); core.SawInjectedEquip({ 6 }); core.SawInjectedEquip({ 9 })
check('own packets', core.StandingDown(), false)

-- vanafish's rod and bait swaps never count
core = fresh()
core.SawInjectedEquip({ 2 }); core.SawInjectedEquip({ 3 }); core.SawInjectedEquip({ 2, 3 })
check('range and ammo only', core.StandingDown(), false)

-- another LuAshitacast: an equipset on three body slots
core = fresh()
printed = {}
core.SawInjectedEquip({ 4, 5, 6 })
check('second LuAshitacast', core.StandingDown(), true)
local warnings = #printed
core.SawInjectedEquip({ 7, 8, 9 })
check('warned once', #printed, warnings)
check('warned at all', warnings > 0, true)

-- two foreign slots are not enough
core = fresh()
core.SawInjectedEquip({ 4 }); core.SawInjectedEquip({ 5 })
check('two slots', core.StandingDown(), false)

return (#out == 0) and 'PASS' or table.concat(out, '\n')
""", str(ADDON))
print(result)
