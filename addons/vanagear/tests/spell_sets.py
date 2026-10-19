r"""Off-game test: vanagear finds the spells you know and gives each one a set (Ferrin, 19 Oct
2026: "it also needs to detect spells on ninja, and the other spelled jobs").

- only spells you have learned and can cast at your main or support job's level count;
- each spell gets the mode for what it does (Utsusemi, elemental or debuff ninjutsu, cures...);
- at midcast the built spell set goes on under the burst toggle, and your own set for the spell
  goes on over everything.

    python addons/vanagear/tests/spell_sets.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
-- spell id -> name, skill id, { [job id] = level }, learned
local spells = {
    [338] = { 'Utsusemi: Ichi', 40, { [13] = 12 }, true },
    [339] = { 'Utsusemi: Ni', 40, { [13] = 37 }, true },
    [340] = { 'Utsusemi: San', 40, { [13] = 75 }, true },
    [320] = { 'Katon: Ichi', 40, { [13] = 15 }, true },
    [321] = { 'Katon: Ni', 40, { [13] = 40 }, false },
    [347] = { 'Kurayami: Ichi', 40, { [13] = 19 }, true },
    [1] = { 'Cure', 34, { [3] = 1, [5] = 3 }, true },
    [144] = { 'Fire', 37, { [4] = 1 }, true },
}
package.loaded['vg.data'] = { Items = function() return {} end, Abilities = function() return {} end,
    Weaponskills = function() return {} end, ModId = function() return nil end }
AshitaCore = {
    GetResourceManager = function() return {
        GetSpellById = function(_, id)
            local s = spells[id]
            if s == nil then return nil end
            local lv = {}
            for j = 1, 24 do lv[j] = s[3][j - 1] or -1 end
            return { Name = { s[1] }, Skill = s[2], LevelRequired = lv }
        end,
    } end,
    GetMemoryManager = function() return { GetPlayer = function() return {
        HasSpell = function(_, id) return spells[id] ~= nil and spells[id][4] end,
    } end } end,
}
local plan = require('vg.plan')
local out = {}
local function names(who)
    local t = {}
    for _, s in ipairs(plan.Spells(who)) do t[#t + 1] = s.Name .. '=' .. tostring(plan.SpellMode(s.Skill, s.Name)) end
    return table.concat(t, ', ')
end
out[#out + 1] = 'NIN49/DNC49: ' .. names({ Job = 13, Level = 49, Sub = 19, SubLevel = 49 })
out[#out + 1] = 'WAR49/NIN49: ' .. names({ Job = 1, Level = 49, Sub = 13, SubLevel = 49 })
out[#out + 1] = 'RDM20/WHM10: ' .. names({ Job = 5, Level = 20, Sub = 3, SubLevel = 10 })
out[#out + 1] = 'modes ' .. plan.SpellMode('Enhancing Magic', 'Stoneskin') .. ' ' .. plan.SpellMode('Dark Magic', 'Aspir II')
    .. ' ' .. tostring(plan.SpellMode('Healing Magic', 'Raise')) .. ' ' .. plan.SpellMode('Ninjutsu', 'Monomi: Ichi')

-- midcast: Katon with burst on, then with a set of your own for it
local worn = {}
gVana = { Toggles = { Burst = true }, Sets = { Ninjutsu = 1, KatonIchi = 2, Burst = 3 }, Own = {} }
gFunc = { EquipSet = function(set) for k, v in pairs(gVana.Sets) do if v == set then worn[#worn + 1] = k end end end,
    Equip = function() end }
gData = { GetAction = function() return { Name = 'Katon: Ichi', Skill = 'Ninjutsu' } end,
    GetEnvironment = function() return {} end }
local stock = require('vg.stock')
stock.HandleMidcast()
out[#out + 1] = 'built: ' .. table.concat(worn, ' > ')
worn = {}
gVana.Own = { KatonIchi = { Head = true } }
stock.HandleMidcast()
out[#out + 1] = 'yours: ' .. table.concat(worn, ' > ')
-- Fire with burst on: the spell's set under Burst
worn = {}
gVana.Sets = { Nuke = 1, Fire = 2, Burst = 3 }
gVana.Own = {}
gData.GetAction = function() return { Name = 'Fire', Skill = 'Elemental Magic', Element = 0 } end
stock.HandleMidcast()
out[#out + 1] = 'fire: ' .. table.concat(worn, ' > ')
return table.concat(out, '\n')
""", str(ADDON))
print(result)
expect = [
    'NIN49/DNC49: Katon: Ichi=Ninjutsu, Kurayami: Ichi=NinjutsuDebuff, Utsusemi: Ichi=Utsusemi, Utsusemi: Ni=Utsusemi',
    'WAR49/NIN49: Katon: Ichi=Ninjutsu, Kurayami: Ichi=NinjutsuDebuff, Utsusemi: Ichi=Utsusemi, Utsusemi: Ni=Utsusemi',
    'RDM20/WHM10: Cure=Cure',
    'modes Stoneskin Drain nil NinjutsuBuff',
    'built: Ninjutsu > KatonIchi',
    'yours: Ninjutsu > KatonIchi',
    'fire: Nuke > Fire > Burst',
]
lines = result.split('\n')
assert lines == expect, '\n'.join(f'{a!r} vs {b!r}' for a, b in zip(lines, expect) if a != b)
print('PASS')
