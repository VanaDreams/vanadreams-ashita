r"""Off-game test that the profile's lua is the only thing that swaps (Lee, 24 Oct 2026: "so now it tries to
read the memory and the lua rather than jsut the lua"; "its called lua lua shitacast"):
- on every event only the profile's own handler fills the equip buffer; vanagear's built-in rules
  (vg\stock.lua) never run, with built sets on or off, template or not;
- what goes on is exactly what the handler equipped, with no fallback piece from memory in a slot.

    python addons/vanagear/tests/lua_only.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
chat = setmetatable({}, { __index = function() return function(...) return table.concat({ ... }, ' ') end end })
gSettings = { SafeCall = true, HorizonMode = false }
local stockRan, processed, fallbackSet = 0, {}, {}
gVanaStock = setmetatable({}, { __index = function() return function() stockRan = stockRan + 1 end end })
local underlay = true
gVanaCore = { StandingDown = function() return false end, PureLac = function() return not underlay end,
    Underlay = function() return underlay end }
gVanaUse = { Tick = function() end }
gVana = { Sets = { Idle = { Body = 'Built Body' } } }
local buffer = {}
gEquip = {
    ClearBuffer = function() buffer = {} end,
    GetBuffer = function() return buffer end,
    SetFallback = function(set) fallbackSet[#fallbackSet + 1] = set or false end,
    ProcessBuffer = function() local c = {}; for k, v in pairs(buffer) do c[k] = v end; processed[#processed + 1] = c end,
    ProcessImmediateBuffer = function() end,
}
gState = require('state')
gState.PlayerAction = { Block = false }
gState.DelayedEquip = {}

local out = {}
local function check(label, ok) out[#out + 1] = (ok and 'ok   ' or 'FAIL ') .. label end

local function run(template)
    gProfile = {
        VgTemplate = template,
        HandleDefault = function() buffer.Body = 'Lua Body' end,
        HandleAbility = function() buffer.Hands = 'Lua Hands' end,
    }
    stockRan = 0
    processed = {}
    gState.HandleEquipEvent('HandleDefault', 'auto')
    gState.HandleEquipEvent('HandleAbility', 'auto')
end

for _, case in ipairs({ { true, false }, { true, true }, { false, false }, { false, true } }) do
    underlay = case[1]
    run(case[2])
    local label = (underlay and 'built sets on' or 'built sets off') .. (case[2] and ', template' or ', own profile')
    check(label .. ': built-in rules never run', stockRan == 0)
    check(label .. ': idle wears exactly what the lua equipped', processed[1] and processed[1].Body == 'Lua Body' and next(processed[1], next(processed[1])) == nil)
    check(label .. ': ability wears exactly what the lua equipped', processed[2] and processed[2].Hands == 'Lua Hands' and processed[2].Body == nil)
end
local anyFallback = false
for _, s in ipairs(fallbackSet) do if s then anyFallback = true end end
check('no fallback pieces from memory', not anyFallback)

local bad = 0
for _, line in ipairs(out) do if line:sub(1, 4) == 'FAIL' then bad = bad + 1 end end
out[#out + 1] = bad == 0 and 'all passed' or (bad .. ' failed')
return table.concat(out, '\n')
""", str(ADDON).replace('\\', '/'))
print(result)
