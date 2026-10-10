--[[
    vanagear: /vg, and /gs for players coming from Windower.

    Both are caught by the addon's own command event: no /alias is ever registered, because
    queuing aliases while a character logs in is what hung Ferrin's login on 28 Sept 2026.
]]--

local core = require('vg.core');
local gsnames = require('vg.gsnames');
local use = require('vg.use');

local commands = {};

local function say(text)
    print(chat.header('vanagear') .. chat.message(text));
end

local function onOff(word, current)
    if (word == 'on') then return true; end
    if (word == 'off') then return false; end
    return not current;
end

local function job()
    return gVana.Who and gVana.Who.JobName or '';
end

local slotNames = {};
for i, name in ipairs(gData.Constants.EquipSlotNames) do
    slotNames[string.lower(name)] = i;
end

-- What /vg help lists: only the lines that mean something for the job, subjob and gear now.
commands.Lines = function()
    local s = gVana.Sets;
    local lines = {};
    local function line(cmd, text) lines[#lines + 1] = { cmd, text }; end
    line('/vg', 'open the window: your sets, the piece in each slot and why');
    line('/vg build', 'rebuild the sets now (also happens on job change, level up and new gear)');
    line('/vg dt', 'damage taken gear over everything else, on or off');
    line('/vg kite', 'movement gear over everything else, on or off');
    if (s.TH ~= nil) then line('/vg th', 'treasure hunter gear over everything else, on or off'); end
    if (s.Tp_Default ~= nil) then line('/vg tp default | hybrid | acc', 'which fighting set you wear'); end
    if (s.Idle_Refresh ~= nil) then
        line('/vg idle auto | regen | refresh | speed', 'standing still: auto puts regen on below 60% HP and refresh below 70% MP');
    else
        line('/vg idle auto | regen | speed', 'standing still: auto puts regen on below 60% HP');
    end
    if (s.Nuke ~= nil) or (s.Enfeebling ~= nil) then line('/vg nuke power | acc', 'damage or accuracy for nukes and enfeebles'); end
    if (s.Burst ~= nil) then line('/vg burst', 'magic burst gear on nukes, on or off'); end
    if (s.Interim ~= nil) then line('/vg stage', 'precast, then safety gear while casting, then midcast gear just before the spell lands'); end
    if (s.PetTP ~= nil) then line('/vg pet tp | dt | acc', 'what your gear does for your pet while it fights'); end
    line('/vg weapons on | off', 'let vanagear choose your weapons (swapping them costs TP)');
    line('/vg lock <slot>, /vg unlock <slot>', 'keep a slot as you have it');
    line('/vg why <set> <slot>', 'the piece that won and the three behind it');
    line('/vg upgrades', 'pieces in storage that would win a slot');
    line('/vg underlay on | off', 'built sets under your own profile\'s sets for this job');
    for word, p in pairs(use.Words()) do
        line('/vg ' .. word, 'equip ' .. p.Name .. ', wait it out, use it, give the slot back');
    end
    line('/vg keys mote | off', 'Motenten\'s F9-F12 keys, as GearSwap players have them');
    line('/gs c <word>', 'the same as /vg <word>, so GearSwap macros keep working');
    line('/gs equip <set>', 'put on a set by its GearSwap or vanagear name');
    return lines;
end

local function help()
    say(string.format('Commands for %s%s/%s:', job(), gVana.Who and gVana.Who.Level or '', gVana.Who and gVana.Who.SubName or ''));
    for _, l in ipairs(commands.Lines()) do
        print(chat.header('vanagear') .. chat.color1(2, l[1]) .. chat.message('  ' .. l[2]));
    end
end

-- the set a name means: a built one, the profile's own, or a GearSwap name for either
local function findSet(name)
    if (gVana.Sets[name] ~= nil) then return gVana.Sets[name], name; end
    if (gProfile ~= nil) and (type(gProfile.Sets) == 'table') and (gProfile.Sets[name] ~= nil) then
        return gProfile.Sets[name], name;
    end
    local mapped = gsnames.ToVanagear(name);
    if (mapped ~= nil) then
        if (gProfile ~= nil) and (type(gProfile.Sets) == 'table') then
            for k, set in pairs(gProfile.Sets) do
                if (gsnames.ToVanagear(k) == mapped) or (k == mapped) then return set, k; end
            end
        end
        if (gVana.Sets[mapped] ~= nil) then return gVana.Sets[mapped], mapped; end
    end
    local lower = string.lower(name);
    for k, set in pairs(gVana.Sets) do
        if (string.lower(k) == lower) then return set, k; end
    end
    return nil;
end

local function why(setName, slotName)
    local set, name = findSet(setName or '');
    local ex = name and gVana.Explain[name];
    local slot = slotNames[string.lower(slotName or '')];
    if (ex == nil) or (slot == nil) then
        say('Usage: /vg why <set> <slot>, for example /vg why Tp_Default Ring1');
        return;
    end
    if (gVana.Held[slot] ~= nil) then
        say(gData.Constants.EquipSlotNames[slot] .. ': ' .. gVana.Held[slot] .. '.');
        return;
    end
    local ranks = ex.ranks[slot] or {};
    if (#ranks == 0) then
        say(name .. ' ' .. gData.Constants.EquipSlotNames[slot] .. ': nothing you carry adds to this set in this slot.');
        return;
    end
    say(string.format('%s %s:', name, gData.Constants.EquipSlotNames[slot]));
    for i = 1, math.min(4, #ranks) do
        local r = ranks[i];
        print(chat.header('vanagear') .. chat.message(string.format('  %d. %s  +%.1f', i, r.c.piece.Name, r.gain)));
    end
end

local function upgrades()
    local bags = require('vg.bags');
    local score = require('vg.score');
    local stored = bags.ReadStorage();
    if (#stored == 0) then
        say('Nothing to compare: the Mog Safe and Storage can only be read in the Mog House or near a Nomad Moogle.');
        return;
    end
    local found = 0;
    for name, ex in pairs(gVana.Explain) do
        for slot, ranks in pairs(ex.ranks) do
            local bestGain = (ranks[1] ~= nil) and ranks[1].gain or 0;
            for _, p in ipairs(stored) do
                if (bags.CanWear(p, slot, gVana.Who)) then
                    local v = score.Vector(p, slot, ex.mode, require('vg.data').PetTypes[job()] or { 0 });
                    local alone = score.Total(ex.mode, v);
                    if (alone > bestGain + 0.5) then
                        found = found + 1;
                        if (found <= 15) then
                            say(string.format('%s (bag %d) would go in %s %s.', p.Name, p.Bag, name, gData.Constants.EquipSlotNames[slot]));
                        end
                        break;
                    end
                end
            end
        end
    end
    if (found == 0) then
        say('Nothing in storage beats what you carry.');
    elseif (found > 15) then
        say(string.format('...and %d more. Move them to a wardrobe and the sets pick them up.', found - 15));
    end
end

local function save()
    core.SavePrefs();
end

-- /gs validate: pieces the profile's sets name that are not in a bag you can equip from.
commands.Validate = function()
    if (gProfile == nil) or (type(gProfile.Sets) ~= 'table') or (gProfile.IsVanagearStock) then
        say('Your sets are built from what you carry, so every piece is there.');
        return;
    end
    local have = {};
    for _, p in ipairs(gVana.Pieces) do have[string.lower(p.Name)] = true; end
    local missing = {};
    for setName, set in pairs(gProfile.Sets) do
        if (type(set) == 'table') then
            for slot, entry in pairs(set) do
                local name = (type(entry) == 'table') and entry.Name or entry;
                if (type(name) == 'string') and (name ~= 'remove') and (name ~= 'displaced') and (not have[string.lower(name)]) then
                    missing[name] = missing[name] or {};
                    table.insert(missing[name], setName);
                end
            end
        end
    end
    local count = 0;
    for name, where in pairs(missing) do
        count = count + 1;
        say(string.format('%s: not in your inventory or wardrobes (%s)', name, table.concat(where, ', ')));
    end
    if (count == 0) then
        say('Every piece your sets name is in a bag you can equip from.');
    end
end

-- Motenten's F9-F12 keys, bound to the same /gs c commands GearSwap players already use
-- (Mote-Globals.lua binds). Off until the player asks; bound once, after the zone wait, one
-- command at a time.
commands.MoteKeys = {
    { 'F9', '/gs c cycle OffenseMode' },
    { '^F9', '/gs c cycle HybridMode' },
    { '!F9', '/gs c cycle RangedMode' },
    { '@F9', '/gs c cycle WeaponskillMode' },
    { 'F10', '/gs c set DefenseMode Physical' },
    { '^F10', '/gs c cycle PhysicalDefenseMode' },
    { '!F10', '/gs c toggle Kiting' },
    { 'F11', '/gs c set DefenseMode Magical' },
    { '^F11', '/gs c cycle CastingMode' },
    { 'F12', '/gs c update user' },
    { '^F12', '/gs c cycle IdleMode' },
    { '!F12', '/gs c reset DefenseMode' },
};

commands.Vg = function(args)
    local t = gVana.Toggles;
    local p = gVana.Prefs;
    local cmd = string.lower(args[1] or '');
    local arg = string.lower(args[2] or '');

    if (cmd == '') then
        gVana.Window.Open = not gVana.Window.Open;
    elseif (cmd == 'help') or (cmd == 'commands') then
        help();
    elseif (cmd == 'build') or (cmd == 'reload') then
        core.Request();
        say('Rebuilding.');
    elseif (cmd == 'dt') then
        t.DT = onOff(arg, t.DT); say('Damage taken gear ' .. (t.DT and 'on' or 'off') .. '.');
    elseif (cmd == 'kite') then
        t.Kite = onOff(arg, t.Kite); say('Movement gear ' .. (t.Kite and 'on' or 'off') .. '.');
    elseif (cmd == 'th') then
        t.TH = onOff(arg, t.TH); say('Treasure hunter gear ' .. (t.TH and 'on' or 'off') .. '.');
    elseif (cmd == 'burst') then
        t.Burst = onOff(arg, t.Burst); say('Magic burst gear ' .. (t.Burst and 'on' or 'off') .. '.');
    elseif (cmd == 'stage') then
        t.Stage = onOff(arg, t.Stage); say('Staged casting ' .. (t.Stage and 'on' or 'off') .. '.');
    elseif (cmd == 'tp') then
        local modes = { default = 'Default', normal = 'Default', hybrid = 'Hybrid', acc = 'Acc' };
        if (modes[arg] ~= nil) then t.Tp = modes[arg];
        else
            local order = { Default = 'Hybrid', Hybrid = 'Acc', Acc = 'Default' };
            t.Tp = order[t.Tp] or 'Default';
        end
        say('Fighting set: Tp_' .. t.Tp .. '.');
    elseif (cmd == 'idle') then
        local modes = { auto = true, regen = true, refresh = true, speed = true };
        t.Idle = modes[arg] and arg or 'auto';
        say('Idle: ' .. t.Idle .. '.');
    elseif (cmd == 'nuke') then
        t.Nuke = (arg == 'acc') and 'acc' or ((arg == 'power') and 'power' or ((t.Nuke == 'acc') and 'power' or 'acc'));
        say('Nukes and enfeebles: ' .. t.Nuke .. '.');
    elseif (cmd == 'pet') then
        local modes = { tp = true, dt = true, acc = true };
        t.Pet = modes[arg] and arg or 'tp';
        say('Pet gear: ' .. t.Pet .. '.');
    elseif (cmd == 'weapons') then
        p.Weapons = onOff(arg, p.Weapons); save(); core.Request();
        say('vanagear ' .. (p.Weapons and 'chooses your weapons' or 'leaves your weapons alone') .. '.');
    elseif (cmd == 'lock') or (cmd == 'unlock') then
        local slot = slotNames[arg];
        if (slot == nil) then say('Usage: /vg lock <slot>, for example /vg lock Neck'); return; end
        p.Locked[job()] = p.Locked[job()] or {};
        p.Locked[job()][slot] = (cmd == 'lock') and true or nil;
        save(); core.Request();
        say(gData.Constants.EquipSlotNames[slot] .. ((cmd == 'lock') and ' locked.' or ' unlocked.'));
    elseif (cmd == 'why') then
        why(args[2], args[3]);
    elseif (cmd == 'upgrades') then
        upgrades();
    elseif (cmd == 'underlay') then
        p.Underlay[job()] = onOff(arg, core.Underlay());
        save();
        say('Built sets under your own profile for ' .. job() .. ': ' .. (p.Underlay[job()] and 'on' or 'off') .. '.');
    elseif (cmd == 'keys') then
        p.MoteKeys = (arg == 'mote') or ((arg ~= 'off') and (not p.MoteKeys));
        save();
        core.QueueKeys(p.MoteKeys);
        say('Motenten\'s F9-F12 keys ' .. (p.MoteKeys and 'bound' or 'unbound') .. '.');
    elseif (cmd == 'wait') then
        local n = tonumber(args[2]);
        if (n ~= nil) and (n >= 0) then p.ZoneWait = n; save(); end
        say('Zone wait: ' .. tostring(p.ZoneWait) .. ' seconds after you are standing in the zone.');
    elseif (use.Start(cmd)) then
        -- a usable ring or earring
    elseif (gProfile ~= nil) then
        -- anything else is the profile's own command, as /lac fwd would send it
        gState.SafeCall('HandleCommand', args);
    end
end

-- /gs: GearSwap's commands.
commands.Gs = function(args)
    local cmd = string.lower(args[1] or '');
    if (cmd == 'c') then
        local rest = {};
        for i = 2, #args do rest[#rest + 1] = args[i]; end
        -- Mote's mode commands become vanagear's
        local first = string.lower(rest[1] or '');
        if (first == 'cycle') or (first == 'set') then
            local mode = string.lower(rest[2] or '');
            local value = rest[3];
            if (mode == 'offensemode') or (mode == 'hybridmode') then
                commands.Vg({ 'tp', value or '' });
            elseif (mode == 'idlemode') then
                commands.Vg({ 'idle', value or '' });
            elseif (mode == 'castingmode') then
                commands.Vg({ 'nuke', (value and string.lower(value) ~= 'normal') and 'acc' or '' });
            elseif (mode == 'weaponskillmode') or (mode == 'rangedmode') then
                commands.Vg({ 'tp', value or '' });
            elseif (mode == 'defensemode') or (mode == 'physicaldefensemode') or (mode == 'magicaldefensemode') then
                commands.Vg({ 'dt', ((value ~= nil) and (string.lower(value) == 'none')) and 'off' or 'on' });
            else
                commands.Vg(rest);
            end
            return;
        end
        if (first == 'toggle') and (string.lower(rest[2] or '') == 'kiting') then
            commands.Vg({ 'kite' });
            return;
        end
        if (first == 'reset') and (string.lower(rest[2] or '') == 'defensemode') then
            commands.Vg({ 'dt', 'off' });
            return;
        end
        if (first == 'update') then
            say('Sets are current.');
            return;
        end
        commands.Vg(rest);
    elseif (cmd == 'equip') then
        local name = table.concat(args, ' ', 2);
        local set, found = findSet(name);
        if (set == nil) then
            say('No set called ' .. name .. '.');
        else
            gFunc.ForceEquipSet(set);
            say('Wearing ' .. found .. '.');
        end
    elseif (cmd == 'validate') then
        commands.Validate();
    elseif (cmd == 'export') then
        local name = args[2] or 'Exported';
        gFunc.AddSet(name);
    elseif (cmd == 'reload') or (cmd == 'r') then
        core.Request();
        say('Rebuilding.');
    else
        commands.Vg(args);
    end
end

-- Called from the command event before LuAshitacast's own /lac handling. Returns true if the
-- command was ours.
commands.Handle = function(e)
    local args = e.command:args();
    if (#args == 0) then
        return false;
    end
    local word = string.lower(args[1]);
    if (word ~= '/vg') and (word ~= '/vanagear') and (word ~= '/gs') and (word ~= '/gearswap') then
        return false;
    end
    e.blocked = true;
    local rest = {};
    for i = 2, #args do rest[#rest + 1] = args[i]; end
    if (word == '/gs') or (word == '/gearswap') then
        commands.Gs(rest);
    else
        commands.Vg(rest);
    end
    return true;
end

return commands;
