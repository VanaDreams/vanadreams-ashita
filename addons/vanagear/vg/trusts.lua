--[[
    vanagear: gear for your trusts.

    Every trust you have called gets a set built from your bags for its job and level, the same
    way your own sets are built, and every slot can be changed in its tab of the /vg window. The
    trust never wears anything and your gear never leaves your bags: the server gives the trust
    the stats of the pieces in its set for as long as you carry them in your inventory or a
    wardrobe (Ferrin, 18 Oct 2026: "we dont need the trusts to wear the gear, just use their
    stats").

    The set goes to the server as !trustgear <trust> <16 item ids>, by itself when the trust is
    called or its set changes, and from the Send button. The server keeps it for that trust, so
    it is there again on the next summon. Your changes are kept by trust name in
    config\addons\vanagear\trusts.lua.
]]--

local bags = require('vg.bags');
local plan = require('vg.plan');
local score = require('vg.score');

local trusts = {};

-- What a trust's one set values, by its job. A trust doesn't swap, so this is the set for what
-- that job's trusts spend their time doing.
trusts.ModeFor = {
    WAR = 'Tp_Default', MNK = 'Tp_Default', THF = 'Tp_Default', DRK = 'Tp_Default', BST = 'Tp_Default',
    SAM = 'Tp_Default', NIN = 'Tp_Default', DRG = 'Tp_Default', DNC = 'Tp_Default', BLU = 'Tp_Default',
    PUP = 'Tp_Default', PLD = 'Tp_Hybrid', RUN = 'Tp_Hybrid', RNG = 'Midshot', COR = 'Midshot',
    WHM = 'Cure', BLM = 'Nuke', SCH = 'Nuke', RDM = 'Enfeebling', BRD = 'Song', GEO = 'Geomancy',
    SMN = 'Idle_Refresh',
};

local own = nil;            -- trust name -> { slot name -> item name | 'remove' }
local built = {};           -- trust name -> { at, job, level, set = { [slot] = piece } }
local sent = {};            -- trust server id -> the line last sent
local sendQueue, sendAt = {}, 0;

local function say(text)
    print(chat.header('vanagear') .. chat.message(text));
end

local function path()
    return string.format('%sconfig\\addons\\vanagear\\trusts.lua', AshitaCore:GetInstallPath());
end

local function loadOwn()
    if (own ~= nil) then return own; end
    own = {};
    local chunk = loadfile(path());
    if (chunk ~= nil) then
        local ok, saved = pcall(chunk);
        if (ok) and (type(saved) == 'table') then own = saved; end
    end
    return own;
end

local function saveOwn()
    local folder = string.format('%sconfig\\addons\\vanagear', AshitaCore:GetInstallPath());
    if (not ashita.fs.exists(folder)) then
        ashita.fs.create_directory(folder);
    end
    local names = {};
    for name in pairs(own) do names[#names + 1] = name; end
    table.sort(names);
    local out = { '-- vanagear: your picks for each trust, by trust name. Written by the addon.', 'return {' };
    for _, name in ipairs(names) do
        local slots = {};
        for slot, item in pairs(own[name]) do
            slots[#slots + 1] = string.format('[%q] = %q', slot, item);
        end
        table.sort(slots);
        if (#slots > 0) then
            out[#out + 1] = string.format('    [%q] = { %s },', name, table.concat(slots, ', '));
        end
    end
    out[#out + 1] = '};';
    local f = io.open(path(), 'w');
    if (f ~= nil) then
        f:write(table.concat(out, '\n') .. '\n');
        f:close();
    end
end

-- The trusts in your party right now: { Id, Index, Name, Job, JobName, Level, Sub, SubName }.
-- Players' server ids are below 0x01000000; trusts are zone entities above it. A trust someone
-- else called shows here too; the server only takes sets for your own.
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

-- The trust as the set builder and bags.CanWear need it. Trusts have no race limit.
trusts.Who = function(t)
    return { Job = t.Job, JobName = t.JobName, Level = t.Level, Sub = t.Sub, SubName = t.SubName, SubLevel = 0, Race = 0 };
end

-- The set built from your bags for this trust (rebuilt when your bags change or the trust levels).
trusts.Built = function(t)
    local b = built[t.Name];
    if (b ~= nil) and (b.at == gVana.BuiltAt) and (b.job == t.Job) and (b.level == t.Level) then
        return b.set;
    end
    local job = plan.Job(t.JobName);
    local mode = score.Compile(plan.Mode(trusts.ModeFor[t.JobName] or 'Tp_Default', job.Tweaks or {}));
    local slots = {};
    for i = 1, 16 do slots[i] = i; end
    local set = score.Build(mode, gVana.Pieces or {}, trusts.Who(t), slots, { 0 });
    built[t.Name] = { at = gVana.BuiltAt, job = t.Job, level = t.Level, set = set };
    return set;
end

-- Your picks for this trust, slot name -> item name or 'remove'.
trusts.Own = function(t)
    return loadOwn()[t.Name] or {};
end

trusts.SetSlot = function(t, slotName, item)
    loadOwn();
    own[t.Name] = own[t.Name] or {};
    own[t.Name][slotName] = item;
    if (next(own[t.Name]) == nil) then own[t.Name] = nil; end
    saveOwn();
    trusts.Send(t);
end

trusts.Reset = function(t)
    loadOwn();
    own[t.Name] = nil;
    saveOwn();
    trusts.Send(t);
end

-- The piece in each slot (1-16) for this trust: your pick, else the built one, else nothing.
trusts.Final = function(t)
    local mine = trusts.Own(t);
    local set = trusts.Built(t);
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

-- The line that gives this trust its set. Slots go in the server's order, which is the slot
-- index order here less one (Main 0 ... Back 15).
trusts.Line = function(t)
    local final = trusts.Final(t);
    local ids = {};
    for slot = 1, 16 do
        ids[slot] = final[slot] and idOf(final[slot]) or 0;
    end
    return string.format('/say !trustgear %d %s', t.Id, table.concat(ids, ' '));
end

-- Sends the set now (well, within a second: one line at a time so chat isn't flooded).
trusts.Send = function(t)
    local line = trusts.Line(t);
    sent[t.Id] = line;
    sendQueue[#sendQueue + 1] = line;
end

-- Called every frame once the zone wait is over: newly called trusts, and trusts whose set
-- changed because your bags did, get their set sent.
local lastLook = 0;
trusts.Tick = function()
    local now = os.clock();
    if (#sendQueue > 0) and (now > sendAt) then
        AshitaCore:GetChatManager():QueueCommand(-1, table.remove(sendQueue, 1));
        sendAt = now + 1.5;
    end
    if (gVana.Who == nil) or (gVana.Prefs.TrustGear == false) or (now - lastLook < 3) then
        return;
    end
    lastLook = now;
    local seen = {};
    for _, t in ipairs(trusts.Called()) do
        seen[t.Id] = true;
        if (t.Level ~= nil) and (t.Level > 0) and (sent[t.Id] ~= trusts.Line(t)) then
            trusts.Send(t);
        end
    end
    -- a trust that left (or a zone) is sent again when it comes back
    for id in pairs(sent) do
        if (not seen[id]) then sent[id] = nil; end
    end
end

trusts.Say = say;

return trusts;
