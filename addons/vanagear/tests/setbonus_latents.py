r"""Off-game test of set bonuses and latent stats in vg\score.lua:
- five Usukane pieces (1% haste each, +5% for all five) beat five pieces of 1.5% each, and lose to
  five of 3% each (the server's gear_sets.lua bonus, counted before caps);
- four of the five give no bonus (Usukane needs all five);
- a piece whose haste works only with the weapon drawn counts in a fighting set, not a standing one;
- a piece that works only on one support job counts only on it.

    python addons/vanagear/tests/setbonus_latents.py
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
package.preload['encoding'] = function() return {} end
local HASTE = 384
local slotBit = {}
for i = 1, 16 do slotBit[i] = bit.lshift(1, i - 1) end
local latents = {}
package.loaded['vg.data'] = {
    ModId = function(n) return ({ HASTE_GEAR = HASTE })[n] end, SlotBit = slotBit, Items = function() return {} end,
    Augments = function() return {} end,
    GearSets = function() return { [1] = { items = { 16092, 14554, 14969, 15633, 15719 }, min = 5, mods = { { mod = HASTE, v = { 500 } } } } } end,
    Latents = function() return latents end,
}
local score = require('vg.score')

-- slots: Head 5, Body 6, Hands 7, Legs 8, Feet 9
local usukane = { [5] = 16092, [6] = 14554, [7] = 14969, [8] = 15633, [9] = 15719 }
local function piece(id, slot, haste)
    return { Id = id, Name = 'p' .. id, Equippable = true, Server = { slot = slotBit[slot], jobs = 0xFFFFFF, level = 1, mods = { [HASTE] = haste } } }
end
local who = { Job = 1, Level = 99, Sub = 13, SubLevel = 49, Race = 0, Zone = 0 }
local slots = { 5, 6, 7, 8, 9 }
local function build(others, drop, mode)
    local pieces = {}
    for slot, id in pairs(usukane) do if slot ~= drop then pieces[#pieces + 1] = piece(id, slot, 100) end end
    for slot = 5, 9 do pieces[#pieces + 1] = piece(50000 + slot, slot, others) end
    local m = score.Compile(mode or { Name = 'Tp_Default', Stats = { HASTE_GEAR = 1 }, Caps = { HASTE_GEAR = 25 }, Drawn = true })
    local set = score.Build(m, pieces, who, slots, { 0 })
    local n = 0
    for _, p in pairs(set) do if p.Id < 50000 then n = n + 1 end end
    return n
end

local out = {}
local function check(label, ok) out[#out + 1] = (ok and 'ok   ' or 'FAIL ') .. label end
check('all five Usukane (5% + 5% bonus) beat five at 1.5%', build(150) == 5)
check('five at 3% beat Usukane', build(300) == 0)
check('with one missing there is no bonus: the 1.5% pieces win', build(150, 9) == 0)

-- latents: a piece with +5% haste only with the weapon drawn (latent 10), and one only on /NIN (8, 13)
latents[60001] = { { latent = 10, param = 0, mod = HASTE, v = 500 } }
latents[60002] = { { latent = 8, param = 13, mod = HASTE, v = 500 } }
local function one(id, mode, w)
    local p = { Id = id, Name = 'l' .. id, Equippable = true, Server = { slot = slotBit[5], jobs = 0xFFFFFF, level = 1, mods = {} } }
    local plain = { Id = 60009, Name = 'plain', Equippable = true, Server = { slot = slotBit[5], jobs = 0xFFFFFF, level = 1, mods = { [HASTE] = 100 } } }
    local set = score.Build(score.Compile(mode), { p, plain }, w or who, { 5 }, { 0 })
    return set[5] and set[5].Id
end
local fight = { Name = 'Tp_Default', Stats = { HASTE_GEAR = 1 }, Drawn = true }
local stand = { Name = 'Idle', Stats = { HASTE_GEAR = 1 }, Drawn = false }
check('weapon drawn haste wins in a fighting set', one(60001, fight) == 60001)
check('weapon drawn haste counts for nothing standing', one(60001, stand) == 60009)
check('the /NIN piece counts on /NIN', one(60002, fight) == 60002)
check('the /NIN piece counts for nothing on /DNC', one(60002, fight, { Job = 1, Level = 99, Sub = 19, SubLevel = 49, Race = 0 }) == 60009)
return table.concat(out, '\n')
""", str(ADDON))
print(result)
print('PASS' if 'FAIL' not in result else 'FAILED')
