r"""Off-game run of every job template (templates\<JOB>.lua) the way vanagear runs a profile: the file is
loaded, every set it asks for has gear, and each handler is called in many states (standing, fighting,
resting, moving, a pet fighting or moving, low HP and MP, every buff off and every buff on, each /vg tp
mode) with abilities, spells, shots, weapon skills, items and commands from every job. Any Lua error is
reported with the job and the call. Lee, 24 Oct 2026: the templates had never been run, and called an
include that did not exist.

    python addons/vanagear/tests/templates_run.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent
JOBS = sorted(p.stem for p in (ADDON / 'templates').glob('*.lua'))

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path, jobs = ...
package.path = addon_path .. '/?.lua;' .. package.path
bit = require('bit')
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
ashita = { fs = { exists = function() return true end, create_directory = function() end } }
package.preload['encoding'] = function() return { ShiftJIS_To_UTF8 = function(_, s) return s end } end
debug.setmetatable(function() end, { __index = { once = function() end } })
local queued = {}
AshitaCore = {
    GetInstallPath = function() return addon_path .. '/../../' end,
    GetChatManager = function() return { QueueCommand = function(_, _, line) queued[#queued + 1] = line end } end,
}
gSettings = {}
gState = { Disabled = {}, PlayerName = 'Test' }

-- the world the handlers read, changed for each case
local world = {}
local equipped = 0
gFunc = {
    EquipSet = function(set) if type(set) ~= 'table' then error('EquipSet given ' .. type(set)) end equipped = equipped + 1 end,
    Equip = function(slot, item) if type(slot) ~= 'string' or type(item) ~= 'string' then error('Equip given ' .. type(slot)) end equipped = equipped + 1 end,
    CancelAction = function() world.cancelled = true end,
    Message = function() end,
    InterimEquipSet = function(set) if type(set) ~= 'table' then error('InterimEquipSet given ' .. type(set)) end end,
    SetMidDelay = function(d) if type(d) ~= 'number' then error('SetMidDelay given ' .. type(d)) end end,
}
gData = {
    GetPlayer = function() return world.player end,
    GetPet = function() return world.pet end,
    GetPetAction = function() return world.petAction end,
    GetAction = function() return world.action end,
    GetActionTarget = function() return world.target end,
    GetBuffCount = function(b) return world.buffs end,
    GetEnvironment = function() return { Area = world.area, DayElement = 'Fire', WeatherElement = 'Light', Time = world.hour } end,
    GetEquipment = function() return {} end,
}

local core = require('vg.core')
gVanaStock = require('vg.stock')
gVanaInclude = require('vg.include')
gVana.Carries = function() return true end
gVana.Own = {}

-- every set has gear, so every wear() goes through
local function allSets()
    return setmetatable({}, { __index = function(t, k) local s = { Body = 'Test ' .. k }; rawset(t, k, s); return s end })
end

local function player(status, extra)
    local p = { Name = 'Test', Status = status, HPP = 100, MPP = 100, MaxMP = 1000, TP = 2000, IsMoving = false, MainJob = 'WAR' }
    for k, v in pairs(extra or {}) do p[k] = v end
    return p
end

local actions = {
    ability = { 'Provoke', 'Berserk', 'Chaos Roll', 'Fire Shot', 'Light Shot', 'Angon', 'Jump', 'Repair', 'Fire Maneuver',
                'Ignis', 'Vallation', 'Climactic Flourish', 'Fire IV', 'Hastega II', 'Call Beast', 'Sic', 'Third Eye',
                'Overdrive', 'Eagle Eye Shot', 'Mijin Gakure', 'Spectral Jig' },
    spell = { { Name = 'Cure IV', Skill = 'Healing Magic', Element = 'Light' }, { Name = 'Utsusemi: Ichi', Skill = 'Ninjutsu', Element = 'Wind' },
              { Name = 'Katon: San', Skill = 'Ninjutsu', Element = 'Fire' }, { Name = 'Fire IV', Skill = 'Elemental Magic', Element = 'Fire' },
              { Name = 'Paralyze', Skill = 'Enfeebling Magic', Element = 'Ice' }, { Name = 'Victory March', Skill = 'Singing', Element = 'Light' },
              { Name = 'Horde Lullaby II', Skill = 'Singing', Element = 'Light' }, { Name = 'Regen II', Skill = 'Enhancing Magic', Element = 'Light' },
              { Name = 'Indi-Haste', Skill = 'Geomancy', Element = 'Wind' }, { Name = 'Head Butt', Skill = 'Blue Magic', Element = 'Dark' },
              { Name = 'Carbuncle', Skill = 'Summoning Magic', Element = 'Light' }, { Name = 'Stoneskin', Skill = 'Enhancing Magic', Element = 'Earth' },
              { Name = 'Death', Skill = 'Elemental Magic', Element = 'Dark' }, { Name = 'Firestorm', Skill = 'Enhancing Magic', Element = 'Fire' },
              { Name = 'Flash', Skill = 'Divine Magic', Element = 'Light' }, { Name = 'Absorb-STR', Skill = 'Dark Magic', Element = 'Dark' },
              { Name = 'Pyrohelix', Skill = 'Elemental Magic', Element = 'Fire' }, { Name = 'Enfire', Skill = 'Enhancing Magic', Element = 'Fire' } },
    ws = { 'Savage Blade', 'Trueflight', 'Evisceration', 'Tachi: Jinpu' },
    petAction = { 'Fire IV', 'Hastega II', 'Sleepga', 'Punch', 'Healing Breath', 'Arcuballista', 'Sheep Song', 'Foot Kick' },
    item = { 'Holy Water', 'Echo Drops' },
    command = { { 'death' }, { 'tank' }, { 'fight' }, { 'harp' }, { 'pup', 'ranger' }, { 'nothing' } },
}

local errors, calls = {}, 0
local function call(job, profile, name, label)
    calls = calls + 1
    local f = profile[name]
    if f == nil then return end
    local ok, err = pcall(f, world.args)
    if not ok then errors[#errors + 1] = job .. ' ' .. name .. ' (' .. label .. '): ' .. tostring(err) end
end

for _, job in ipairs(jobs) do
    local chunk, err = loadfile(addon_path .. '/templates/' .. job .. '.lua')
    if chunk == nil then
        errors[#errors + 1] = job .. ' load: ' .. tostring(err)
    else
        local ok, profile = pcall(chunk)
        if not ok then
            errors[#errors + 1] = job .. ' run: ' .. tostring(profile)
        else
            -- what vanagear's Refresh does: the profile's sets table filled in place
            local filled = allSets()
            for k, v in pairs(profile.Sets) do rawset(filled, k, v) end
            setmetatable(profile.Sets, getmetatable(filled))
            gProfile = profile
            if not profile.VgTemplate then errors[#errors + 1] = job .. ': no VgTemplate mark' end
            call(job, profile, 'OnLoad', 'load')
            for _, buffs in ipairs({ 0, 1 }) do
                for _, tp in ipairs({ 'Default', 'Acc', 'Proc' }) do
                    gVana.Toggles.Tp = tp
                    gVana.Toggles.TH = (buffs == 1)
                    gVana.Toggles.DT = (buffs == 1) and ({ true, 'pdt', 'mdt' })[(tp == 'Default') and 1 or ((tp == 'Acc') and 2 or 3)] or false
                    gVana.Toggles.Stage = (buffs == 1)
                    world.buffs = buffs
                    world.area = (buffs == 1) and 'Port Jeuno' or 'West Ronfaure'
                    world.target = { Name = 'Mob', Distance = 3 }
                    local states = {
                        { 'idle', player('Idle'), nil }, { 'engaged', player('Engaged'), { Status = 'Engaged', HPP = 30, TP = 990 } },
                        { 'resting', player('Resting', { MPP = 20 }), nil }, { 'moving', player('Idle', { IsMoving = true, HPP = 30, MPP = 30 }), { Status = 'Idle', HPP = 100, TP = 0 } },
                    }
                    for _, s in ipairs(states) do
                        world.player, world.pet, world.petAction = s[2], s[3], nil
                        world.hour = (buffs == 1) and 20 or 12
                        call(job, profile, 'HandleDefault', s[1] .. ' ' .. tp .. ' buffs ' .. buffs)
                    end
                    world.player = player('Engaged')
                    world.pet = { Status = 'Engaged', HPP = 80, TP = 1000 }
                    for _, a in ipairs(actions.petAction) do
                        world.petAction = { Name = a }
                        call(job, profile, 'HandleDefault', 'pet ' .. a)
                    end
                    world.petAction = nil
                    for _, a in ipairs(actions.ability) do
                        world.action = { Name = a, Type = (a == 'Fire IV') and 'Blood Pact: Rage' or 'Job Ability' }
                        call(job, profile, 'HandleAbility', a)
                    end
                    for _, sp in ipairs(actions.spell) do
                        world.action = setmetatable({ CastTime = 3000 }, { __index = sp })
                        world.target = (sp.Skill == 'Healing Magic') and { Name = 'Test', Distance = 0 } or { Name = 'Mob', Distance = 10 }
                        call(job, profile, 'HandlePrecast', sp.Name)
                        call(job, profile, 'HandleMidcast', sp.Name)
                    end
                    world.target = { Name = 'Mob', Distance = 3 }
                    call(job, profile, 'HandlePreshot', 'shot')
                    call(job, profile, 'HandleMidshot', 'shot')
                    for _, w in ipairs(actions.ws) do
                        world.action = { Name = w }
                        world.player = player('Engaged', { TP = (buffs == 1) and 3000 or 1500 })
                        call(job, profile, 'HandleWeaponskill', w)
                    end
                    for _, it in ipairs(actions.item) do
                        world.action = { Name = it }
                        call(job, profile, 'HandleItem', it)
                    end
                end
            end
            for _, args in ipairs(actions.command) do
                world.args = args
                call(job, profile, 'HandleCommand', table.concat(args, ' '))
            end
            world.args = nil
            call(job, profile, 'OnUnload', 'unload')
        end
    end
end
return table.concat(errors, '\n') .. '\n--\n' .. calls .. ' calls, ' .. equipped .. ' equips, ' .. #errors .. ' errors'
""", str(ADDON), L.table_from(JOBS))
print(result)
tail = result.rsplit('\n', 1)[-1]
assert tail.endswith(' 0 errors'), 'template errors'
assert len(JOBS) == 22, f'{len(JOBS)} templates'
print('PASS')
