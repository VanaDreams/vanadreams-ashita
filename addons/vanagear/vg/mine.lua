--[[
    vanagear: the player's own commands. /vg <word> runs the lines saved under that word, in order.

    They live in config\addons\vanagear\commands\<JOB>.lua (and ALL.lua for every job), the same
    folder style LuAshitacast profiles use, so a player can write them by hand. The /vg window and
    /vg add write the same files. A line is any game command (/ja "Reward" <me>, /vg call fizzy);
    "wait 2" pauses two seconds before the next line.

    Your own sets live in config\addons\vanagear\sets\<JOB>.lua, made in the window slot by slot,
    with /vg set <name> <slot> <item>, or from what you wear with /vg save <name>. One named like a
    built set (Idle, Tp_Default, Reward, a weaponskill or spell) replaces the slots it names in that
    set wherever it goes on; any other name is a set of its own for /vg wear <name>, your commands,
    or /gs equip. /vg look <name> locks one as your appearance (/lockstyle).
]]--

local mine = {};

local running = nil;   -- { lines, index, at }

local function say(text)
    print(chat.header('vanagear') .. chat.message(text));
end

mine.Folder = function(kind)
    return string.format('%sconfig\\addons\\vanagear\\%s', AshitaCore:GetInstallPath(), kind or 'commands');
end

local function path(file, kind)
    return string.format('%s\\%s.lua', mine.Folder(kind), file);
end

local function job()
    return gVana.Who and gVana.Who.JobName or '';
end

local cache = {};   -- file -> { word -> { line, ... } }
local setCache = nil;   -- { job, sets = name -> { slot = item name } }

local function load(file)
    if (cache[file] ~= nil) then
        return cache[file];
    end
    local words = {};
    local chunk, err = loadfile(path(file));
    if (chunk ~= nil) then
        local ok, saved = pcall(chunk);
        if (ok) and (type(saved) == 'table') then
            for word, lines in pairs(saved) do
                if (type(lines) == 'string') then lines = { lines }; end
                if (type(word) == 'string') and (type(lines) == 'table') then
                    words[string.lower(word)] = lines;
                end
            end
        else
            say(string.format('%s.lua did not load: %s', file, tostring(saved)));
        end
    elseif (err ~= nil) and (not string.find(err, 'cannot open')) then
        say(string.format('%s.lua did not load: %s', file, err));
    end
    cache[file] = words;
    return words;
end

local function save(file)
    local folder = mine.Folder();
    if (not ashita.fs.exists(folder)) then
        ashita.fs.create_directory(folder);
    end
    local words = cache[file] or {};
    local keys = {};
    for word in pairs(words) do keys[#keys + 1] = word; end
    table.sort(keys);
    local out = {
        string.format('-- vanagear commands for %s. /vg <word> runs the lines in order.', (file == 'ALL') and 'every job' or file),
        '-- A line is any game command; "wait 2" pauses two seconds. Edit here or in the /vg window.',
        'return {',
    };
    for _, word in ipairs(keys) do
        local quoted = {};
        for _, line in ipairs(words[word]) do quoted[#quoted + 1] = string.format('%q', line); end
        out[#out + 1] = string.format('    [%q] = { %s },', word, table.concat(quoted, ', '));
    end
    out[#out + 1] = '};';
    local f = io.open(path(file), 'w');
    if (f == nil) then
        say('Could not write ' .. path(file));
        return;
    end
    f:write(table.concat(out, '\n') .. '\n');
    f:close();
end

-- Forget what was read, so hand edits are picked up.
mine.Reload = function()
    cache = {};
    setCache = nil;
    require('vg.core').ApplyOwn();
end

-- word -> { lines, file } for this job, the job's own file winning over ALL.lua
mine.List = function()
    local list = {};
    for word, lines in pairs(load('ALL')) do list[word] = { lines = lines, file = 'ALL' }; end
    if (job() ~= '') then
        for word, lines in pairs(load(job())) do list[word] = { lines = lines, file = job() }; end
    end
    return list;
end

-- Lines typed as one string, split on ';' or new lines.
mine.Split = function(text)
    local lines = {};
    for part in string.gmatch(text or '', '[^;\n]+') do
        local line = string.match(part, '^%s*(.-)%s*$');
        if (line ~= '') then lines[#lines + 1] = line; end
    end
    return lines;
end

mine.Add = function(word, lines, everyJob)
    word = string.lower(string.gsub(word or '', '[^%w_%-]', ''));
    if (word == '') or (#lines == 0) then
        return false;
    end
    local file = (everyJob or job() == '') and 'ALL' or job();
    load(file)[word] = lines;
    save(file);
    return true, word, file;
end

mine.Remove = function(word)
    word = string.lower(word or '');
    local entry = mine.List()[word];
    if (entry == nil) then
        return false;
    end
    load(entry.file)[word] = nil;
    save(entry.file);
    return true, entry.file;
end

-- Open config\addons\vanagear in Windows Explorer: commands\ and sets\ are in it, made first
-- with this job's file so there is something to edit.
mine.OpenFolder = function()
    for _, kind in ipairs({ 'commands', 'sets' }) do
        if (not ashita.fs.exists(mine.Folder(kind))) then
            ashita.fs.create_directory(mine.Folder(kind));
        end
    end
    local existing = (job() ~= '') and io.open(path(job()), 'r');
    if (existing) then
        existing:close();
    elseif (job() ~= '') then
        load(job());
        save(job());
    end
    local folder = string.format('%sconfig\\addons\\vanagear', AshitaCore:GetInstallPath());
    os.execute(string.format('start "" explorer "%s"', folder));
    return folder;
end

mine.Run = function(word)
    local entry = mine.List()[string.lower(word or '')];
    if (entry == nil) then
        return false;
    end
    if (running ~= nil) then
        say('Your command ' .. running.word .. ' is still going.');
        return true;
    end
    local lines = {};
    for i, l in ipairs(entry.lines) do lines[i] = l; end
    running = { word = string.lower(word), lines = lines, index = 1, at = 0 };
    return true;
end

mine.Stop = function()
    if (running ~= nil) then
        say('Your command ' .. running.word .. ' stopped.');
        running = nil;
    end
end

-- Chains (M402): a line "then <command>" goes once the action the line before it sent has
-- finished, plus the gap the server wants between links; "burst <command>" goes the moment it
-- finishes. The watching is what Lee's BST profile does by hand (chainTick): see the step start
-- in gData.GetAction (or GetPetAction for a /pet line), see it end, then send. A step that never
-- starts, or a target that dies or changes, stops the whole command.
mine.LinkGap = 3.3;        -- seconds after a link finishes before the next may land (battleutils, 3 s rule)
mine.StepTimeout = 8;      -- seconds to see a step start before giving up

-- the action a line sends, if it is one 'then' can wait on: /ws, /ja, /ma or /pet "Name" ...
local function actionOf(line)
    local cmd, name = string.match(line, '^/(%a+)%s+"([^"]+)"');
    if (cmd == 'ws') or (cmd == 'ja') or (cmd == 'ma') or (cmd == 'pet') then
        return { kind = cmd, name = string.lower(name) };
    end
    return nil;
end

local function currentAction(kind)
    local ok, a = pcall(function()
        return (kind == 'pet') and gData.GetPetAction() or gData.GetAction();
    end);
    return ok and a or nil;
end

local function targetGone(w)
    local ok, t = pcall(gData.GetTarget);
    if (not ok) then return false; end
    return (t == nil) or (t.Id ~= w.targetId) or ((t.HPP or 1) <= 0);
end

-- One look at the action a 'then' or 'burst' line is waiting on. Looked at the moment the watch
-- is set as well as every frame after, so a quick ability is not missed between two frames.
local function watch(r, now)
    local w = r.watch;
    if (targetGone(w)) then
        say('Your command ' .. r.word .. ': target lost, stopped.');
        running = nil;
        return;
    end
    local a = currentAction(w.kind);
    if (w.phase == 'starting') then
        if (a ~= nil) and (string.lower(a.Name or '') == w.name) then
            w.phase = 'running';
        elseif (now - w.sentAt > mine.StepTimeout) then
            say('Your command ' .. r.word .. ': ' .. w.name .. ' never started, stopped.');
            running = nil;
        end
        return;
    end
    if (a ~= nil) then
        return;
    end
    r.watch = nil;
    r.at = now + w.gap;
end

-- Called every frame: send the next line once any wait is over.
mine.Tick = function()
    local r = running;
    if (r == nil) or (os.clock() < r.at) then
        return;
    end
    local now = os.clock();
    -- a 'then' or 'burst' line is waiting for the action before it
    if (r.watch ~= nil) then
        watch(r, now);
        return;
    end
    local line = r.lines[r.index];
    if (line == nil) then
        running = nil;
        return;
    end
    local rest, gap = string.match(line, '^[Tt][Hh][Ee][Nn]%s+(.+)$'), mine.LinkGap;
    if (rest == nil) then
        rest, gap = string.match(line, '^[Bb][Uu][Rr][Ss][Tt]%s+(.+)$'), 0;
    end
    if (rest ~= nil) then
        if (r.last ~= nil) then
            -- wait for the last action, then come back to this line without its prefix
            r.watch = { kind = r.last.kind, name = r.last.name, sentAt = r.last.sentAt, targetId = r.last.targetId, phase = 'starting', gap = gap };
            r.last = nil;
            r.lines[r.index] = rest;
            watch(r, now);
            return;
        end
        line = rest;   -- nothing before it to wait for: it just goes
    end
    r.index = r.index + 1;
    local secs = tonumber(string.match(line, '^[Ww][Aa][Ii][Tt]%s+([%d%.]+)$'));
    if (secs ~= nil) then
        r.at = os.clock() + secs;
        return;
    end
    -- remember an action line so a 'then' after it has something to wait for
    local a = actionOf(line);
    if (a ~= nil) then
        local ok, t = pcall(gData.GetTarget);
        a.sentAt = now;
        a.targetId = (ok and t ~= nil) and t.Id or nil;
    end
    r.last = a;
    local first = string.lower(string.match(line, '^(%S+)') or '');
    if (first == '/vg') or (first == '/vanagear') or (first == '/gs') or (first == '/gearswap') then
        -- vanagear's own commands run straight away, never back through the chat queue
        local args = {};
        for w in string.gmatch(line, '%S+') do args[#args + 1] = (string.gsub(w, '^"(.*)"$', '%1')); end
        table.remove(args, 1);
        local commands = require('vg.commands');
        commands.Raw = line;
        if (first == '/gs') or (first == '/gearswap') then commands.Gs(args); else commands.Vg(args); end
        commands.Raw = nil;
        r.at = os.clock() + 0.3;
        return;
    end
    AshitaCore:GetChatManager():QueueCommand(1, line);
    r.at = os.clock() + 0.3;
end

-- ---------------------------------------------------------------- your own sets

local function setFile()
    return path((job() ~= '') and job() or 'ALL', 'sets');
end

local function loadSets()
    if (setCache ~= nil) and (setCache.job == job()) then
        return setCache.sets;
    end
    local sets = {};
    local chunk = loadfile(setFile());
    if (chunk ~= nil) then
        local ok, saved = pcall(chunk);
        if (ok) and (type(saved) == 'table') then
            for name, set in pairs(saved) do
                if (type(name) == 'string') and (type(set) == 'table') then sets[name] = set; end
            end
        else
            say('Your sets file did not load: ' .. tostring(saved));
        end
    end
    setCache = { job = job(), sets = sets };
    return sets;
end

local function saveSets()
    local folder = mine.Folder('sets');
    if (not ashita.fs.exists(folder)) then
        ashita.fs.create_directory(folder);
    end
    local sets = loadSets();
    local names = {};
    for name in pairs(sets) do names[#names + 1] = name; end
    table.sort(names);
    local out = {
        string.format('-- vanagear: your own sets for %s. /vg wear <name>, /vg look <name>. Edit here or in the /vg window.', job()),
        'return {',
    };
    for _, name in ipairs(names) do
        out[#out + 1] = string.format('    [%q] = {', name);
        for _, slot in ipairs(gData.Constants.EquipSlotNames) do
            local piece = sets[name][slot];
            if (piece ~= nil) then
                out[#out + 1] = string.format('        %s = %q,', slot, piece);
            end
        end
        out[#out + 1] = '    },';
    end
    out[#out + 1] = '};';
    local f = io.open(setFile(), 'w');
    if (f == nil) then
        say('Could not write ' .. setFile());
        return;
    end
    f:write(table.concat(out, '\n') .. '\n');
    f:close();
end

mine.Sets = function()
    return loadSets();
end

-- the saved set by name, any case
mine.FindSet = function(name)
    local sets = loadSets();
    local want = string.lower(name or '');
    for k, set in pairs(sets) do
        if (string.lower(k) == want) then return set, k; end
    end
    return nil;
end

-- Names players know from other swappers, for the built set that does that job. "Engaged" is
-- what LuAshitacast and GearSwap profiles call the fighting set.
mine.Aliases = { engaged = 'Tp_Default', tp = 'Tp_Default', melee = 'Tp_Default', engagedhybrid = 'Tp_Hybrid',
    hybrid = 'Tp_Hybrid', engagedacc = 'Tp_Acc', acc = 'Tp_Acc', ws = 'Ws_Default', weaponskill = 'Ws_Default',
    fastcast = 'Precast', midcast = 'Interim', rest = 'Resting', dt = 'Dt', pdt = 'Dt', kite = 'Movement', idledt = 'Dt' };

-- The name a window list shows a built set under.
mine.Label = function(name)
    if (name == 'Tp_Default') then return 'Engaged'; end
    if (name == 'Tp_Hybrid') then return 'Engaged (hybrid)'; end
    if (name == 'Tp_Acc') then return 'Engaged (accuracy)'; end
    return name;
end

-- A set's name as the handlers look it up: letters, digits and _ only, so "Call Beast" is the
-- set that goes on for Call Beast, the same as the built ones.
mine.SetName = function(name)
    name = string.gsub(name or '', '[^%w_]', '');
    name = mine.Aliases[string.lower(name)] or name;
    for k in pairs(gVana.Built or {}) do
        if (string.lower(k) == string.lower(name)) then return k; end
    end
    local _, existing = mine.FindSet(name);
    return existing or name;
end

local function changed()
    saveSets();
    require('vg.core').ApplyOwn();
end

-- /vg save <name>: what you are wearing now, slot by slot.
mine.SaveSet = function(name)
    name = mine.SetName(name);
    if (name == '') then
        return false;
    end
    local set = {};
    local count = 0;
    local weapons = { Main = true, Sub = true, Range = true };
    for slot, worn in pairs(gData.GetEquipment()) do
        -- weapons only when vanagear is choosing them (/vg weapons on): swapping costs TP
        if (gVana.Prefs.Weapons) or (not weapons[slot]) then
            set[slot] = worn.Name;
            count = count + 1;
        end
    end
    loadSets()[name] = set;
    changed();
    if (mine.Dressing) then mine.Dress(false); end
    return true, name, count;
end

-- /vg dress: vanagear stops swapping so the player can put on what they like from the equipment
-- menu; /vg save puts swapping back on.
mine.Dressing = false;
mine.Dress = function(on)
    mine.Dressing = on;
    for i = 1, 16 do gState.Disabled[i] = on; end
end

-- One slot of one of your sets; nil gives the slot back to the built set.
mine.SetSlot = function(name, slot, piece)
    name = mine.SetName(name);
    if (name == '') then
        return false;
    end
    local sets = loadSets();
    sets[name] = sets[name] or {};
    sets[name][slot] = piece;
    changed();
    return true, name;
end

mine.RemoveSet = function(name)
    local _, found = mine.FindSet(name);
    if (found == nil) then
        return false;
    end
    loadSets()[found] = nil;
    changed();
    return true, found;
end

-- The built sets with yours laid over them: a set of yours named like a built one (Idle,
-- Tp_Default, Reward, a weaponskill or spell) replaces the slots it names and keeps the rest;
-- any other name is a set of its own.
mine.Overlay = function(built)
    local out, own = {}, {};
    for name, set in pairs(built) do out[name] = set; end
    for name, set in pairs(loadSets()) do
        local key = mine.Aliases[string.lower(name)] or name;
        for k in pairs(built) do
            if (string.lower(k) == string.lower(key)) then key = k; end
        end
        local merged = {};
        for slot, piece in pairs(built[key] or {}) do merged[slot] = piece; end
        own[key] = {};
        for slot, piece in pairs(set) do
            merged[slot] = piece;
            own[key][slot] = true;
        end
        out[key] = merged;
    end
    return out, own;
end

-- Copies a LuAshitacast profile's sets into your own vanagear sets for this job, under the
-- names vanagear wears them by (Engaged -> Tp_Default, precast.FC -> Precast; any other name
-- stays a set of its own). Pieces the profile names with augments keep just the item name.
-- Returns how many sets were copied.
mine.ImportProfile = function(profileSets)
    local gsnames = require('vg.gsnames');
    local sets = loadSets();
    local count = 0;
    for name, set in pairs(profileSets or {}) do
        if (type(name) == 'string') and (type(set) == 'table') then
            local bare = string.gsub(name, '[^%w_]', '');
            local key = mine.Aliases[string.lower(bare)] or gsnames.ToVanagear(name) or bare;
            local copy = {};
            for _, slot in ipairs(gData.Constants.EquipSlotNames) do
                local piece = set[slot];
                if (type(piece) == 'table') then piece = piece.Name; end
                if (type(piece) == 'string') and (piece ~= '') then
                    copy[slot] = (string.lower(piece) == 'displaced') and 'remove' or piece;
                end
            end
            if (key ~= '') and (next(copy) ~= nil) then
                sets[key] = copy;
                count = count + 1;
            end
        end
    end
    changed();
    return count;
end

mine.ReloadSets = function()
    setCache = nil;
    require('vg.core').ApplyOwn();
end

return mine;
