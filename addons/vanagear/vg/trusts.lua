--[[
    vanagear: gear for your trusts.

    Every trust you have called gets gear sets built from your bags for its job and level, the
    same way your own sets are built: Idle, Engaged (damage taken and enmity for a tank), a
    Weaponskill set and one for each weaponskill it uses, a set for each kind of spell it casts
    (Cure, Nuke, Enhancing...) and an Ability set for a tank. Every slot of every set can be
    changed in the trust's tab of the /vg window, and you can add a set named after anything the
    trust uses (Flash, Savage Blade, Cure IV).

    The trust never wears anything and your gear never leaves your bags: the server gives the
    trust the stats of the pieces while you carry them in your inventory or a wardrobe (Ferrin,
    18 Oct 2026: "we dont need the trusts to wear the gear, just use their stats"). It swaps them
    the way your own sets swap: Idle or Engaged, and an action's own set (by its name, else its
    kind) laid over that while the action goes off.

    Sets go to the server as !trustgear <trust> <set> <16 item ids>, by themselves when a set
    changes and from the Send button. The server keeps them, so nothing is sent again for the
    next summon unless a set changed. !trustgear <trust> info tells the tab the trust's name,
    jobs, level and what it uses; the server answers only for your own trusts, and those lines
    are hidden from chat. Each trust has a profile file, config\addons\vanagear\profiles\<Name>_<id>\trusts\
    <Trust>.lua (vg\profile.lua), the same layout as a job's: built, from the band of its job template
    for its level, and sets, your picks; what is sent is sets over built, read out of that file.
]]--

local bags = require('vg.bags');
local plan = require('vg.plan');
local score = require('vg.score');
local vdata = require('vg.data');

local trusts = {};

local DT = { PDT = -50, MDT = -50 };

-- What a trust's Engaged set values, by its job.
trusts.ModeFor = {
    WAR = 'Tp_Default', MNK = 'Tp_Default', THF = 'Tp_Default', DRK = 'Tp_Default', BST = 'Tp_Default',
    SAM = 'Tp_Default', NIN = 'Tp_Default', DRG = 'Tp_Default', DNC = 'Tp_Default', BLU = 'Tp_Default',
    PUP = 'Tp_Default', PLD = 'Tank', RUN = 'Tank', RNG = 'Midshot', COR = 'Midshot',
    WHM = 'Idle_Refresh', BLM = 'Idle_Refresh', SCH = 'Idle_Refresh', RDM = 'Tp_Default', BRD = 'Idle_Refresh',
    GEO = 'Idle_Refresh', SMN = 'Idle_Refresh',
};

-- Jobs that hold hate: their Engaged set is a tank's and they get an Ability set for enmity.
trusts.Tanks = { PLD = true, RUN = true };

-- Jobs that stand back and cast: no Weaponskill set unless they use one.
local casters = { WHM = true, BLM = true, SCH = true, BRD = true, GEO = true, SMN = true };

-- Trust-only modes, laid out like roles.lua's.
local modes = {
    Tank = {
        Stats = { PDT = -6, MDT = -5, HP = 0.04, ENMITY = 1, DEF = 0.03, MEVA = 0.05, ACC = 0.3, STORETP = 0.6,
                  DOUBLE_ATTACK = 0.8, HASTE_GEAR = 1.5 },
        Caps = { PDT = -50, MDT = -50, HASTE_GEAR = 25 },
    },
    Enmity = { Stats = { ENMITY = 3, PDT = -1.5, MDT = -1, HP = 0.03 }, Caps = DT },
};

-- The server's set kinds (trustgearutils::SpellKind), what each set is called here, and the
-- vanagear mode it is built from.
trusts.Kinds = {
    { Key = 'cure', Name = 'Cure', Mode = 'Cure' },
    { Key = 'nuke', Name = 'Nuke', Mode = 'Nuke' },
    { Key = 'enfeebling', Name = 'Enfeebling', Mode = 'Enfeebling' },
    { Key = 'enhancing', Name = 'Enhancing', Mode = 'Enhancing' },
    { Key = 'dark', Name = 'Dark', Mode = 'Dark' },
    { Key = 'divine', Name = 'Divine', Mode = 'Divine' },
    { Key = 'song', Name = 'Song', Mode = 'Song' },
    { Key = 'ninjutsu', Name = 'Ninjutsu', Mode = 'Ninjutsu' },
    { Key = 'bluemagic', Name = 'Blue Magic', Mode = 'BluePhysical' },
    { Key = 'geomancy', Name = 'Geomancy', Mode = 'Geomancy' },
};
local kindByKey = {};
for _, k in ipairs(trusts.Kinds) do kindByKey[k.Key] = k; end

-- The spell kinds a job's trusts cast, until the server says what this one uses.
local jobKinds = {
    WHM = { 'cure', 'enhancing', 'divine' }, RDM = { 'enfeebling', 'enhancing', 'cure', 'nuke' },
    BLM = { 'nuke', 'enfeebling', 'dark' }, SCH = { 'nuke', 'cure', 'enhancing' }, PLD = { 'cure', 'divine' },
    DRK = { 'dark' }, NIN = { 'ninjutsu' }, BLU = { 'bluemagic' }, BRD = { 'song' }, GEO = { 'geomancy', 'nuke' },
    RUN = { 'enhancing' },
};

local own = nil;            -- trust name -> set name -> { slot name -> item name | 'remove' }
local fileSets = {};        -- trust name -> what its profile file sends: its sets over its built ones
local fileSig = {};         -- trust name -> the built sets last written, so the file is written only on a change
local sentSig = nil;        -- trust name -> set key -> the ids last sent (kept on disk: the server keeps the sets)
local built = {};           -- trust name -> { at, job, level, uses, sets = { set name -> { [slot] = piece } } }
local info = {};            -- trust server id -> { Name, Job, Sub, Level, Uses = { { Name, Kind } }, Ours }
local asked = {};           -- trust server id -> true once info was asked for
local sendQueue, sendAt = {}, 0;

local function say(text)
    print(chat.header('vanagear') .. chat.message(text));
end

-- How the server keeps set names: "Savage Blade" -> "savageblade".
trusts.Key = function(name)
    return string.sub(string.lower((string.gsub(name or '', '%W', ''))), 1, 32);
end

-- The game data's names read nicely: "cure_iv" -> "Cure IV", "savage_blade" -> "Savage Blade".
local roman = { i = true, ii = true, iii = true, iv = true, v = true, vi = true, vii = true, viii = true, ix = true, x = true };
trusts.Pretty = function(key)
    local words = {};
    for w in string.gmatch(key or '', '[^_%s]+') do
        if (roman[w]) then
            words[#words + 1] = string.upper(w);
        else
            words[#words + 1] = string.upper(string.sub(w, 1, 1)) .. string.sub(w, 2);
        end
    end
    return table.concat(words, ' ');
end

local function folder()
    return string.format('%sconfig\\addons\\vanagear', AshitaCore:GetInstallPath());
end

local function readTable(file)
    local chunk = loadfile(string.format('%s\\%s', folder(), file));
    if (chunk ~= nil) then
        local ok, saved = pcall(chunk);
        if (ok) and (type(saved) == 'table') then return saved; end
    end
    return {};
end

local function writeTable(file, header, t)
    if (not ashita.fs.exists(folder())) then
        ashita.fs.create_directory(folder());
    end
    local function sorted(tbl)
        local keys = {};
        for k in pairs(tbl) do keys[#keys + 1] = k; end
        table.sort(keys);
        return keys;
    end
    local out = { header, 'return {' };
    for _, name in ipairs(sorted(t)) do
        out[#out + 1] = string.format('    [%q] = {', name);
        for _, set in ipairs(sorted(t[name])) do
            local v = t[name][set];
            if (type(v) == 'table') then
                local slots = {};
                for _, slot in ipairs(sorted(v)) do
                    slots[#slots + 1] = string.format('[%q] = %q', slot, v[slot]);
                end
                out[#out + 1] = string.format('        [%q] = { %s },', set, table.concat(slots, ', '));
            else
                out[#out + 1] = string.format('        [%q] = %q,', set, tostring(v));
            end
        end
        out[#out + 1] = '    },';
    end
    out[#out + 1] = '};';
    local f = io.open(string.format('%s\\%s', folder(), file), 'w');
    if (f ~= nil) then
        f:write(table.concat(out, '\n') .. '\n');
        f:close();
    end
end

-- Your picks for a trust live in its profile file (profiles\<Name>_<id>\trusts\<Trust>.lua, vg\profile.lua),
-- in its sets table, as your own sets go into your job's profile. What the single trusts.lua held for the
-- trust (vanagear 0.1.20 and before) is moved in the first time.
local function saveOwn(name)
    if (not require('vg.profile').WriteTrustSets(name, own[name] or {})) then
        say('Could not write the sets for ' .. name .. ' into ' .. require('vg.profile').TrustPath(name));
    end
    fileSets[name] = nil;   -- read the file again before the next send
end

local function loadOwn(name)
    own = own or {};
    if (own[name] == nil) then
        local picks = require('vg.profile').ReadTrustSets(name);
        if (picks == nil) then
            local old = readTable('trusts.lua')[name];
            own[name] = old or {};
            if (old ~= nil) then saveOwn(name); end
        else
            own[name] = picks;
        end
    end
    return own[name];
end

local function loadSent()
    if (sentSig == nil) then sentSig = readTable('trusts_sent.lua'); end
    return sentSig;
end

local function saveSent()
    writeTable('trusts_sent.lua', '-- vanagear: the trust sets the server already has. Written by the addon.', sentSig);
end

-- The trusts in your party right now: { Id, Index, Name, Job, JobName, Level, Sub, SubName }.
-- Players' server ids are below 0x01000000; trusts are zone entities above it. A trust someone
-- else called shows here too; the server only answers for your own (trusts.Ours).
trusts.Called = function()
    local party = AshitaCore:GetMemoryManager():GetParty();
    local resources = AshitaCore:GetResourceManager();
    local function abbr(id)
        local s = resources:GetString('jobs.names_abbr', id);
        return (type(s) == 'string') and s:trimend('\x00') or '';
    end
    local list = {};
    for i = 1, 5 do
        local id = party:GetMemberServerId(i);
        if (party:GetMemberIsActive(i) == 1) and (id ~= nil) and (id >= 0x01000000) then
            local job = party:GetMemberMainJob(i);
            list[#list + 1] = {
                Id = id,
                Index = party:GetMemberTargetIndex(i),
                Name = party:GetMemberName(i),
                Job = job,
                JobName = abbr(job),
                Level = party:GetMemberMainJobLevel(i),
                Sub = party:GetMemberSubJob(i),
                SubName = abbr(party:GetMemberSubJob(i)),
            };
        end
    end
    return list;
end

-- What the server said about this trust, or nil until it answers (it never does for someone else's).
trusts.Info = function(t)
    return info[t.Id];
end

trusts.Ours = function(t)
    return info[t.Id] ~= nil;
end

-- The trust as the set builder and bags.CanWear need it. Trusts have no race limit.
trusts.Who = function(t)
    return { Job = t.Job, JobName = t.JobName, Level = t.Level, Sub = t.Sub, SubName = t.SubName, SubLevel = 0, Race = 0 };
end

-- The sets vanagear builds for this trust, in the order the tab lists them:
-- { Name, Key, Mode (a mode table), For (what it goes on for) }.
trusts.Plan = function(t)
    local job = plan.Job(t.JobName);
    -- the job template's band for the trust's level laid over its all-level tweaks, as your own sets are built
    local tweaks = plan.BandTweaks(job, t.Level or 99);
    local function named(name)
        if (modes[name] ~= nil) then
            local m = { Name = name, Stats = {}, Caps = {} };
            for k, v in pairs(modes[name].Stats) do m.Stats[k] = v; end
            for k, v in pairs(modes[name].Caps or {}) do m.Caps[k] = v; end
            return m;
        end
        return plan.Mode(name, tweaks);
    end

    local list, seen = {}, {};
    local function add(name, mode, forWhat)
        local key = trusts.Key(name);
        if (not seen[key]) then
            seen[key] = true;
            list[#list + 1] = { Name = name, Key = key, Mode = mode, For = forWhat };
        end
    end

    local tank = trusts.Tanks[t.JobName];
    add('Idle', named(casters[t.JobName] and 'Idle_Refresh' or 'Idle'), 'standing around');
    add('Engaged', named(trusts.ModeFor[t.JobName] or 'Tp_Default'), tank and 'fighting: damage taken down and enmity' or 'fighting');

    local i = info[t.Id];
    local kinds, usesWs = {}, false;
    if (i ~= nil) then
        for _, use in ipairs(i.Uses) do
            if (kindByKey[use.Kind] ~= nil) then kinds[use.Kind] = true; end
            if (use.Kind == 'weaponskill') then usesWs = true; end
        end
    else
        for _, k in ipairs(jobKinds[t.JobName] or {}) do kinds[k] = true; end
        usesWs = not casters[t.JobName];
    end

    if (usesWs) then
        add('Weaponskill', named('Ws_Default'), 'any weaponskill without a set of its own');
    end
    for _, k in ipairs(trusts.Kinds) do
        if (kinds[k.Key]) then
            add(k.Name, named(k.Mode), string.format('%s spells', string.lower(k.Name)));
        end
    end
    if (tank) then
        add('Ability', named('Enmity'), 'job abilities: enmity');
    end

    -- each weaponskill it uses that vanagear knows the stats of gets its own set, as yours do
    if (i ~= nil) then
        local byKey = {};
        for _, ws in pairs(vdata.Weaponskills()) do
            byKey[ws.key] = ws;
        end
        for _, use in ipairs(i.Uses) do
            local ws = byKey[use.Name];
            if (use.Kind == 'weaponskill') and (ws ~= nil) and (next(ws.wsc) ~= nil) then
                local display = trusts.Pretty(use.Name);
                add(display, plan.WsMode(ws, display), display);
            end
        end
    end

    return list;
end

-- The sets built from your bags for this trust (rebuilt when your bags change, the trust levels
-- or the server says what it uses): set name -> { [slot] = piece }.
trusts.Built = function(t)
    local i = info[t.Id];
    local uses = i and #i.Uses or -1;
    local b = built[t.Name];
    if (b ~= nil) and (b.at == gVana.BuiltAt) and (b.job == t.Job) and (b.level == t.Level) and (b.uses == uses) then
        return b.sets, b.plan;
    end
    local slots = {};
    for s = 1, 16 do slots[s] = s; end
    local who = trusts.Who(t);
    local sets = {};
    local planned = trusts.Plan(t);
    for _, p in ipairs(planned) do
        sets[p.Name] = score.Build(score.Compile(p.Mode), gVana.Pieces or {}, who, slots, { 0 });
    end
    built[t.Name] = { at = gVana.BuiltAt, job = t.Job, level = t.Level, uses = uses, sets = sets, plan = planned };
    return sets, planned;
end

-- Every set this trust has, built ones first, then yours: { Name, Key, For, Built (true/false) }.
trusts.Sets = function(t)
    local _, planned = trusts.Built(t);
    local list, seen = {}, {};
    for _, p in ipairs(planned) do
        seen[p.Key] = true;
        list[#list + 1] = { Name = p.Name, Key = p.Key, For = p.For, Built = true };
    end
    local mine = loadOwn(t.Name);
    local names = {};
    for name in pairs(mine) do
        if (not seen[trusts.Key(name)]) then names[#names + 1] = name; end
    end
    table.sort(names);
    for _, name in ipairs(names) do
        seen[trusts.Key(name)] = true;
        list[#list + 1] = { Name = name, Key = trusts.Key(name), For = name, Built = false };
    end
    return list;
end

-- Your picks for one of this trust's sets, slot name -> item name or 'remove'.
trusts.Own = function(t, setName)
    local mine = loadOwn(t.Name);
    return mine[setName] or {};
end

trusts.SetSlot = function(t, setName, slotName, item)
    local mine = loadOwn(t.Name);
    mine[setName] = mine[setName] or {};
    mine[setName][slotName] = item;
    saveOwn(t.Name);
end

-- A set of your own, named after something the trust uses ("Flash", "Savage Blade").
trusts.AddSet = function(t, setName)
    if (trusts.Key(setName) == '') then return nil; end
    for _, s in ipairs(trusts.Sets(t)) do
        if (s.Key == trusts.Key(setName)) then return s.Name; end
    end
    loadOwn(t.Name)[setName] = {};
    saveOwn(t.Name);
    return setName;
end

-- A built set goes back to what vanagear picks; a set of yours is deleted.
trusts.Reset = function(t, setName)
    loadOwn(t.Name)[setName] = nil;
    saveOwn(t.Name);
end

-- The piece in each slot (1-16) of one set: your pick, else the built one, else nothing.
trusts.Final = function(t, setName)
    local mine = trusts.Own(t, setName);
    local set = trusts.Built(t)[setName] or {};
    local out = {};
    for slot = 1, 16 do
        local slotName = gData.Constants.EquipSlotNames[slot];
        local pick = mine[slotName];
        if (pick == 'remove') then
            out[slot] = nil;
        elseif (pick ~= nil) then
            out[slot] = pick;
        elseif (set[slot] ~= nil) then
            out[slot] = set[slot].Name;
        end
    end
    return out;
end

local function idOf(name)
    local want = string.lower(name);
    for _, p in ipairs(gVana.Pieces or {}) do
        if (string.lower(p.Name) == want) then return p.Id; end
    end
    return 0;
end

-- The trust's built sets into its profile file's built table (config\addons\vanagear\profiles\<Name>_<id>\
-- trusts, vg\profile.lua), then the file read back, its sets over its built ones: what is sent to the server.
trusts.WriteProfile = function(t)
    local sets, planned = trusts.Built(t);
    local out, parts = {}, {};
    for _, p in ipairs(planned) do
        local set = {};
        parts[#parts + 1] = p.Name;
        for slot = 1, 16 do
            local piece = (sets[p.Name] or {})[slot];
            if (piece ~= nil) then set[gData.Constants.EquipSlotNames[slot]] = piece.Name; end
            parts[#parts + 1] = (piece ~= nil) and piece.Name or '';
        end
        out[p.Name] = set;
    end
    local sig = table.concat(parts, '|');
    if (fileSig[t.Name] ~= sig) then
        fileSets[t.Name] = require('vg.profile').WriteTrust(t.Name, out);
        fileSig[t.Name] = sig;
    elseif (fileSets[t.Name] == nil) then
        fileSets[t.Name] = require('vg.profile').WriteTrust(t.Name, nil);
    end
    return fileSets[t.Name];
end

-- The item ids of one set in the server's slot order, which is the slot index order here less
-- one (Main 0 ... Back 15), or 'clear' when it has nothing. From the trust's profile file once
-- it is written; until then from the sets worked out here.
trusts.Ids = function(t, setName)
    local file = fileSets[t.Name] and fileSets[t.Name][setName];
    local final = (file == nil) and trusts.Final(t, setName) or nil;
    local ids, any = {}, false;
    for slot = 1, 16 do
        local piece;
        if (file ~= nil) then
            piece = file[gData.Constants.EquipSlotNames[slot]];
            if (type(piece) == 'table') then piece = piece.Name; end
        else
            piece = final[slot];
        end
        ids[slot] = (type(piece) == 'string') and idOf(piece) or 0;
        any = any or (ids[slot] ~= 0);
    end
    return any and table.concat(ids, ' ') or 'clear';
end

trusts.Line = function(t, setName)
    return string.format('/say !trustgear %d %s %s', t.Id, trusts.Key(setName), trusts.Ids(t, setName));
end

-- One line at a time so chat isn't flooded; a newer line for the same set replaces a waiting one.
local function queue(key, line)
    for _, q in ipairs(sendQueue) do
        if (q.Key == key) then q.Line = line; return; end
    end
    sendQueue[#sendQueue + 1] = { Key = key, Line = line };
end

-- The server keeps sets per character, so what it has is kept per character too.
local function sentName(t)
    return string.format('%s/%s', AshitaCore:GetMemoryManager():GetParty():GetMemberName(0) or '', t.Name);
end

-- Sends what changed since the server last had this trust's sets; all of them with all = true.
trusts.Send = function(t, all)
    trusts.WriteProfile(t);
    loadSent();
    local had = sentSig[sentName(t)] or {};
    local now, changed, listed = {}, false, {};
    for _, s in ipairs(trusts.Sets(t)) do
        listed[s.Key] = true;
        local ids = trusts.Ids(t, s.Name);
        if (ids ~= 'clear') then now[s.Key] = ids; end
        if (all) or (had[s.Key] ~= now[s.Key]) then
            changed = true;
            queue(t.Id .. ' ' .. s.Key, string.format('/say !trustgear %d %s %s', t.Id, s.Key, ids));
        end
    end
    -- sets the server has that are gone here (a set of yours you deleted)
    for key in pairs(had) do
        if (not listed[key]) then
            changed = true;
            queue(t.Id .. ' ' .. key, string.format('/say !trustgear %d %s clear', t.Id, key));
        end
    end
    if (changed) then
        sentSig[sentName(t)] = (next(now) ~= nil) and now or nil;
        saveSent();
    end
end

-- The server's answer to !trustgear <trust> info, hidden from chat:
--   [VG trust] <id>|<name>|<main job>|<sub job>|<level>
--   [VG uses] <id>|<name>:<kind>,<name>:<kind>...
trusts.TextIn = function(e)
    local text = e.message_modified or e.message or '';
    local id, name, job, sub, level = string.match(text, '%[VG trust%] (%d+)|([^|]*)|(%d+)|(%d+)|(%d+)');
    if (id ~= nil) then
        info[tonumber(id)] = { Name = name, Job = tonumber(job), Sub = tonumber(sub), Level = tonumber(level), Uses = {} };
        e.blocked = true;
        return;
    end
    -- [VG off] <id>|<key>,<key>...: what the player has switched off for this trust (vanadreams' !trustuse)
    local offId, offList = string.match(text, '%[VG off%] (%d+)|([^\r\n]*)');
    if (offId ~= nil) then
        local i = info[tonumber(offId)];
        if (i ~= nil) then
            i.Off = i.Off or {};
            for key in string.gmatch(offList, '%w+') do
                i.Off[key] = true;
            end
        end
        e.blocked = true;
        return;
    end
    local usesId, list = string.match(text, '%[VG uses%] (%d+)|([^\r\n]*)');
    if (usesId ~= nil) then
        local i = info[tonumber(usesId)];
        if (i ~= nil) then
            for useName, kind in string.gmatch(list, '([%w_]+):(%a+)') do
                i.Uses[#i.Uses + 1] = { Name = useName, Kind = kind };
            end
        end
        e.blocked = true;
    end
end

-- Called every frame once the zone wait is over: each newly called trust is asked about once,
-- and once the server has said it is yours, any set that changed (your bags, your picks) is sent.
local lastLook = 0;
trusts.Tick = function()
    local now = os.clock();
    if (#sendQueue > 0) and (now > sendAt) then
        AshitaCore:GetChatManager():QueueCommand(-1, table.remove(sendQueue, 1).Line);
        sendAt = now + 1.2;
    end
    if (gVana.Who == nil) or (gVana.Prefs.TrustGear == false) or (now - lastLook < 3) then
        return;
    end
    lastLook = now;
    local seen = {};
    for _, t in ipairs(trusts.Called()) do
        seen[t.Id] = true;
        if (t.Level ~= nil) and (t.Level > 0) then
            if (not asked[t.Id]) then
                asked[t.Id] = true;
                queue(t.Id .. ' info', string.format('/say !trustgear %d info', t.Id));
                queue(t.Id .. ' off', string.format('/say !trustuse %d info', t.Id));
            elseif (info[t.Id] ~= nil) then
                trusts.Send(t);
            end
        end
    end
    -- a trust that left: its new entity id is asked about when it comes back
    for id in pairs(asked) do
        if (not seen[id]) then asked[id] = nil; info[id] = nil; end
    end
end

-- Is this action (or kind) switched off for this trust? Keys are the same as the server's: letters and digits of the name.
trusts.IsOff = function(t, use)
    local i = info[t.Id];
    return (i ~= nil) and (i.Off ~= nil) and (i.Off[trusts.Key(use.Name)] == true);
end

-- Switch an action off or on: shown at once, sent to the server as !trustuse (a newer line for the same action
-- replaces one still waiting), and the server keeps it for as long as the trust is out and every time it is called.
trusts.SetOff = function(t, use, off)
    local i = info[t.Id];
    if (i == nil) then return; end
    i.Off = i.Off or {};
    i.Off[trusts.Key(use.Name)] = off and true or nil;
    queue(t.Id .. ' use ' .. use.Name, string.format('/say !trustuse %d %s %s', t.Id, off and 'off' or 'on', use.Name));
end

trusts.Say = say;

return trusts;
