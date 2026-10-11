--[[
    vanagear: the player's own commands. /vg <word> runs the lines saved under that word, in order.

    They live in config\addons\vanagear\commands\<JOB>.lua (and ALL.lua for every job), the same
    folder style LuAshitacast profiles use, so a player can write them by hand. The /vg window and
    /vg add write the same files. A line is any game command (/ja "Reward" <me>, /vg call fizzy);
    "wait 2" pauses two seconds before the next line.

    Your own sets work the same way, in config\addons\vanagear\sets\<JOB>.lua: /vg save <name>
    keeps what you are wearing under that name, /vg wear <name> puts it on and keeps it on, and
    /vg look <name> puts it on and locks it as your look (/lockstyle) while the sets go on swapping.
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
    running = { word = string.lower(word), lines = entry.lines, index = 1, at = 0 };
    return true;
end

-- Called every frame: send the next line once any wait is over.
mine.Tick = function()
    local r = running;
    if (r == nil) or (os.clock() < r.at) then
        return;
    end
    local line = r.lines[r.index];
    if (line == nil) then
        running = nil;
        return;
    end
    r.index = r.index + 1;
    local secs = tonumber(string.match(line, '^[Ww][Aa][Ii][Tt]%s+([%d%.]+)$'));
    if (secs ~= nil) then
        r.at = os.clock() + secs;
        return;
    end
    local first = string.lower(string.match(line, '^(%S+)') or '');
    if (first == '/vg') or (first == '/vanagear') or (first == '/gs') or (first == '/gearswap') then
        -- vanagear's own commands run straight away, never back through the chat queue
        local args = {};
        for w in string.gmatch(line, '%S+') do args[#args + 1] = (string.gsub(w, '^"(.*)"$', '%1')); end
        table.remove(args, 1);
        local commands = require('vg.commands');
        if (first == '/gs') or (first == '/gearswap') then commands.Gs(args); else commands.Vg(args); end
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

-- /vg save <name>: what you are wearing now, slot by slot.
mine.SaveSet = function(name)
    name = string.match(name or '', '^%s*(.-)%s*$');
    if (name == '') then
        return false;
    end
    local _, existing = mine.FindSet(name);
    name = existing or name;
    local set = {};
    local count = 0;
    for slot, worn in pairs(gData.GetEquipment()) do
        set[slot] = worn.Name;
        count = count + 1;
    end
    loadSets()[name] = set;
    saveSets();
    return true, name, count;
end

mine.RemoveSet = function(name)
    local _, found = mine.FindSet(name);
    if (found == nil) then
        return false;
    end
    loadSets()[found] = nil;
    saveSets();
    return true, found;
end

mine.ReloadSets = function()
    setCache = nil;
end

return mine;
