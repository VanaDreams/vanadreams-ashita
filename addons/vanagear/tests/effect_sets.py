r"""Off-game test of the sets vg\plan.lua adds for the job templates (M419): the self, skill and
precast enhancing sets and Cure_Self with the magic that uses them; Saboteur, Sneak Attack, Trick
Attack and Afflatus Solace by job and level, main or support job; Enspell and Helix when a spell of
theirs is known; the obi sets (waist only) for anyone who casts; none of them for a job that casts
nothing.

    python addons/vanagear/tests/effect_sets.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
bit = require('bit')
local traits = dofile(addon_path .. '/data/traits.lua')
local gifts = dofile(addon_path .. '/data/gifts.lua')
local mods = dofile(addon_path .. '/data/mods.lua')
package.loaded['vg.data'] = {
    Abilities = function() return {} end, Weaponskills = function() return {} end, Items = function() return {} end,
    ModId = function() return 1 end, PetTypes = {},
    Traits = function() return traits end, Gifts = function() return gifts end,
    ModName = function(id) return mods[id] end,
    WsElement = function() return nil end, Enfeeble = function() return nil end,
}
package.loaded['vg.wsguide'] = {}
function T(t) return t end
package.preload['encoding'] = function() return {} end
AshitaCore = {
    GetInstallPath = function() return addon_path .. '\\..\\..\\' end,
    GetResourceManager = function() return { GetAbilityById = function() return nil end } end,
    GetMemoryManager = function() return { GetPlayer = function() return {} end } end,
}
local plan = require('vg.plan')
local spells = {}
plan.Spells = function() return spells end

local ids = { WAR=1, MNK=2, WHM=3, BLM=4, RDM=5, THF=6, PLD=7, DRK=8, BST=9, BRD=10, RNG=11, SAM=12, NIN=13, DRG=14,
    SMN=15, BLU=16, COR=17, PUP=18, DNC=19, SCH=20, GEO=21, RUN=22 }
local lastModes = {}
local function names(job, level, sub, subLevel, known, jp)
    spells = known
    local list = plan.For({ JobName = job, Level = level, SubName = sub, SubLevel = subLevel, Job = ids[job], Sub = ids[sub] or 0,
        JobPoints = jp or 0 }, {}, { Weapons = false })
    local out, slots = {}, {}
    lastModes = {}
    for _, e in ipairs(list) do out[e.Name] = true; slots[e.Name] = e.Slots; lastModes[e.Name] = e.Mode end
    return out, slots
end

local out = {}
local function check(label, ok) out[#out + 1] = (ok and 'ok   ' or 'FAIL ') .. label end

local rdm, rdmSlots = names('RDM', 99, 'SCH', 49, {
    { Name = 'Cure IV', Skill = 'Healing Magic' }, { Name = 'Refresh II', Skill = 'Enhancing Magic' },
    { Name = 'Enfire', Skill = 'Enhancing Magic' }, { Name = 'Pyrohelix', Skill = 'Elemental Magic' },
    { Name = 'Slow II', Skill = 'Enfeebling Magic' } })
check('RDM: Cure_Self', rdm.Cure_Self)
check('RDM: Enhancing_Self, Refresh_Self, Enhancing_Skill, Enhancing_Precast',
    rdm.Enhancing_Self and rdm.Refresh_Self and rdm.Enhancing_Skill and rdm.Enhancing_Precast)
check('RDM 99: Saboteur', rdm.Saboteur)
check('RDM: Enspell (knows Enfire)', rdm.Enspell)
check('RDM/SCH: Helix (knows Pyrohelix)', rdm.Helix)
check('RDM: the obis', rdm.Obi and rdm.Obi_Fire and rdm.Obi_Dark)
check('obis are waist only', #rdmSlots.Obi == 1 and rdmSlots.Obi[1] == 11)
check('RDM: no Sneak Attack', not rdm.SneakAttack)

local rdm80 = names('RDM', 80, nil, 0, { { Name = 'Cure', Skill = 'Healing Magic' } })
check('RDM 80: no Saboteur yet', not rdm80.Saboteur)

local thf = names('THF', 99, 'NIN', 49, {})
check('THF: Sneak Attack and Trick Attack', thf.SneakAttack and thf.TrickAttack)
check('THF casting nothing: no obi, no enhancing sets', not thf.Obi and not thf.Enhancing_Self)

local war = names('WAR', 99, 'THF', 49, {})
check('WAR/THF: Sneak Attack (15) and Trick Attack (30) from the support job', war.SneakAttack and war.TrickAttack)

local blm = names('BLM', 99, 'WHM', 49, { { Name = 'Cure III', Skill = 'Healing Magic' } })
check('BLM/WHM: Cure_Solace from the support job (40)', blm.Cure_Solace)

local whm = names('WHM', 30, nil, 0, { { Name = 'Cure', Skill = 'Healing Magic' } })
check('WHM 30: no Cure_Solace yet', not whm.Cure_Solace)

-- caps less what the job's traits and gifts already give
local b = plan.Bonus({ Job = 5, Level = 99, Sub = 0, SubLevel = 0, JobPoints = 0 })
out[#out + 1] = 'RDM 99 Fast Cast from traits: ' .. tostring(b.FASTCAST)
names('RDM', 99, nil, 0, { { Name = 'Cure', Skill = 'Healing Magic' } })
check('RDM 99: Precast cap 80 less the trait', lastModes.Precast.Caps.FASTCAST == 80 - (b.FASTCAST or 0) and (b.FASTCAST or 0) > 0)
names('RDM', 10, nil, 0, { { Name = 'Cure', Skill = 'Healing Magic' } })
check('RDM 10: no trait yet, cap 80', lastModes.Precast.Caps.FASTCAST == 80)
local gb = plan.Bonus({ Job = 17, Level = 99, Sub = 0, SubLevel = 0, JobPoints = 2100 })
local g0 = plan.Bonus({ Job = 17, Level = 99, Sub = 0, SubLevel = 0, JobPoints = 0 })
out[#out + 1] = 'COR Snapshot from gifts at 2100 JP: ' .. tostring(gb.SNAPSHOT) .. ', at 0 JP: ' .. tostring(g0.SNAPSHOT)
names('COR', 99, nil, 0, {}, 2100)
check('COR 2100 JP: Preshot snapshot cap 70 less the gifts', lastModes.Preshot.Caps.SNAPSHOT == 70 - (gb.SNAPSHOT or 0))
local nin = plan.Bonus({ Job = 13, Level = 99, Sub = 0, SubLevel = 0 })
local war = plan.Bonus({ Job = 1, Level = 99, Sub = 13, SubLevel = 49 })
out[#out + 1] = 'Dual Wield: NIN 99 ' .. tostring(nin.DUAL_WIELD) .. ', WAR/NIN 49 ' .. tostring(war.DUAL_WIELD)
check('support job traits count at the support level', (war.DUAL_WIELD or 0) > 0 and (war.DUAL_WIELD or 0) < (nin.DUAL_WIELD or 0))

return table.concat(out, '\n')
""", str(ADDON))
print(result)
print('PASS' if 'FAIL' not in result else 'FAILED')
