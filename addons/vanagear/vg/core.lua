--[[
    vanagear: the state the builder keeps, the zone wait, and when sets get rebuilt.

    gVana.Sets holds the built sets by name, ready for gFunc.EquipSet. They are rebuilt when the
    job, subjob, level or weapons change, when gear comes into or leaves the equippable bags, and
    on /vg build. A rebuild is spread over several frames so the game never stalls on it.

    The zone wait: Ferrin, 10 Oct 2026, "luashitacast will crash my server if its set to load in
    right away, it needs a good delay after zones". After every zone-in, nothing here touches the
    game until the client has sent its first position from the new zone and ZoneWait more
    seconds have passed: no profile is loaded, no gear is equipped, no sets are built, no command
    is sent.
]]--

local bags = require('vg.bags');
local plan = require('vg.plan');
local score = require('vg.score');
local vdata = require('vg.data');

local core = {};

gVana = {
    Sets = {},
    Explain = {},
    Totals = {},
    FastCast = 0,
    Held = {},
    Who = nil,
    BuiltAt = nil,
    Toggles = { DT = false, Kite = false, TH = false, Tp = 'Default', Idle = 'auto', Nuke = 'power', Burst = false,
                Stage = false, Pet = 'tp' },
    Prefs = { ZoneWait = 10, Weapons = false, WeaponPick = {}, Locked = {}, Underlay = {}, MoteKeys = false },
    Pieces = {},
    Built = {},
    Own = {},
};

-- ---------------------------------------------------------------- preferences on disk

local function prefsPath()
    return string.format('%sconfig\\addons\\vanagear\\prefs.lua', AshitaCore:GetInstallPath());
end

local function serialize(v, indent)
    indent = indent or '';
    if (type(v) == 'table') then
        local parts = {};
        for k, x in pairs(v) do
            local key = (type(k) == 'number') and ('[' .. k .. ']') or ('[' .. string.format('%q', k) .. ']');
            parts[#parts + 1] = indent .. '    ' .. key .. ' = ' .. serialize(x, indent .. '    ') .. ',';
        end
        return '{\n' .. table.concat(parts, '\n') .. '\n' .. indent .. '}';
    elseif (type(v) == 'string') then
        return string.format('%q', v);
    end
    return tostring(v);
end

core.SavePrefs = function()
    local folder = string.format('%sconfig\\addons\\vanagear', AshitaCore:GetInstallPath());
    if (not ashita.fs.exists(folder)) then
        ashita.fs.create_directory(folder);
    end
    local f = io.open(prefsPath(), 'w');
    if (f ~= nil) then
        f:write('-- vanagear settings, written by the addon.\nreturn ' .. serialize(gVana.Prefs) .. ';\n');
        f:close();
    end
end

core.LoadPrefs = function()
    local chunk = loadfile(prefsPath());
    if (chunk ~= nil) then
        local ok, saved = pcall(chunk);
        if (ok) and (type(saved) == 'table') then
            for k, v in pairs(saved) do gVana.Prefs[k] = v; end
        end
    end
end

-- ---------------------------------------------------------------- the zone wait

local zone = { Holding = true, EnteredAt = os.clock(), InZoneAt = nil };

-- Slots you changed yourself from the equipment menu: vanagear leaves them as you put them
-- until the next zone (a Job Points cape stays on). slot index -> true.
gVana.HandHeld = {};

core.HoldByHand = function(slot)
    if (slot < 1) or (slot > 16) or (require('vg.mine').Dressing) then
        return;
    end
    if (gState.Disabled[slot] ~= true) then
        gVana.HandHeld[slot] = true;
        gState.Disabled[slot] = true;
    end
end

local function releaseHandHeld()
    for slot in pairs(gVana.HandHeld) do
        gState.Disabled[slot] = false;
    end
    gVana.HandHeld = {};
end

-- Called on the incoming zone packet (0x0A).
core.ZoneIn = function()
    releaseHandHeld();
    zone.Holding = true;
    zone.Leaving = false;
    zone.EnteredAt = os.clock();
    zone.InZoneAt = nil;
end

-- Called on the incoming zone-out packet (0x0B): from here to the next 0x0A the client has no
-- equipment or bags loaded, so the wait starts now and positions sent on the way out don't count.
core.ZoneOut = function()
    core.ZoneIn();
    zone.Leaving = true;
end

-- Called on every outgoing position packet (0x15): the client only sends these once it is
-- standing in the zone.
core.Moved = function()
    if (zone.Holding) and (not zone.Leaving) and (zone.InZoneAt == nil) then
        zone.InZoneAt = os.clock();
    end
end

-- True while the zone wait is running.
core.Holding = function()
    if (not zone.Holding) then
        return false;
    end
    local now = os.clock();
    local start = zone.InZoneAt;
    -- a client that never sends a position (standing still in a cutscene) is let go a minute in
    if (start == nil) and (now - zone.EnteredAt > 60) then
        start = zone.EnteredAt;
    end
    if (start ~= nil) and (now - start >= (gVana.Prefs.ZoneWait or 10)) then
        zone.Holding = false;
        core.OnSettled();
        return false;
    end
    return true;
end

-- ---------------------------------------------------------------- building

local queue = nil;   -- the rebuild in progress: { items = plan, i = next index, ... }
local wanted = false;
local loud = false;

-- loud: say in chat when the build is done (only for /vg build; every other rebuild is silent,
-- Ferrin 18 Oct 2026: "we should make the gear sets message silent").
core.Request = function(sayDone)
    wanted = true;
    if (sayDone) then loud = true; end
end

local function setFor(built)
    local set = {};
    for slot, piece in pairs(built) do
        set[gData.Constants.EquipSlotNames[slot]] = piece.Equip;
    end
    return set;
end

local function startBuild()
    local who = bags.Who();
    if (who.Job == 0) or (who.JobName == '') then
        return;
    end
    local equipment = gData.GetEquipment();
    local items, held = plan.For(who, equipment, { Weapons = gVana.Prefs.Weapons, Locked = gVana.Prefs.Locked[who.JobName] });
    queue = {
        who = who,
        items = items,
        held = held,
        pieces = bags.Read(),
        petTypes = vdata.PetTypes[who.JobName] or { 0 },
        i = 1,
        sets = {},
        explain = {},
        totals = {},
    };
end

local function stepBuild(budget)
    local q = queue;
    local stop = q.i + budget - 1;
    while (q.i <= #q.items) and (q.i <= stop) do
        local item = q.items[q.i];
        local mode = score.Compile(item.Mode);
        local built, ranks, total, totals = score.Build(mode, q.pieces, q.who, item.Slots, q.petTypes);
        if (next(built) ~= nil) then
            q.sets[item.Name] = setFor(built);
            q.explain[item.Name] = { ranks = ranks, total = total, mode = mode, weaponskill = item.Weaponskill };
            q.totals[item.Name] = totals;
        end
        q.i = q.i + 1;
    end
    if (q.i > #q.items) then
        -- swap in the new sets all at once, keeping the same table so profiles holding it see them
        gVana.Built = q.sets;
        gVana.Explain = q.explain;
        gVana.Totals = q.totals;
        gVana.Held = q.held;
        gVana.Who = q.who;
        gVana.Pieces = q.pieces;
        gVana.BuiltAt = os.time();
        core.ApplyOwn();
        local fc = vdata.ModId('FASTCAST');
        gVana.FastCast = (q.totals.Precast and fc) and (q.totals.Precast['m' .. fc] or 0) or 0;
        local count = 0;
        for _ in pairs(q.sets) do count = count + 1; end
        if (loud) then
            loud = false;
            print(chat.header('vanagear') .. chat.message(string.format('Built %d sets for %s%d/%s from your bags. /vg to see them.',
                count, q.who.JobName, q.who.Level, q.who.SubName)));
        end
        queue = nil;
    end
end

-- The sets the handlers wear: the built ones with the player's own laid over them (vg\mine.lua).
-- Filled in place, so profiles holding gVana.Sets see the change.
core.ApplyOwn = function()
    local sets, own = require('vg.mine').Overlay(gVana.Built or {});
    for k in pairs(gVana.Sets) do gVana.Sets[k] = nil; end
    for k, v in pairs(sets) do gVana.Sets[k] = v; end
    gVana.Own = own;
    -- Weapons: with /vg weapons off no set, built or your own, carries a Main, Sub or Range; the
    -- only weapons that go on are the ones picked in the window (or /vg weapon), worn in every set.
    local picks = {};
    if (not gVana.Prefs.Weapons) then
        local job = gVana.Who and gVana.Who.JobName or '';
        local chosen = (gVana.Prefs.WeaponPick or {})[job] or {};
        for name, set in pairs(gVana.Sets) do
            local copy = {};
            for slot, piece in pairs(set) do copy[slot] = piece; end
            for _, slot in ipairs({ 'Main', 'Sub', 'Range' }) do
                copy[slot] = chosen[slot];
                if (own[name] ~= nil) then own[name][slot] = nil; end
                if (chosen[slot] ~= nil) then picks[string.lower(chosen[slot])] = true; end
            end
            gVana.Sets[name] = copy;
        end
    end
    gVana.WeaponPicks = picks;
end

-- The weapon worn in a slot whatever set is on, or nil to leave the slot alone.
core.SetWeaponPick = function(slotName, piece)
    local job = gVana.Who and gVana.Who.JobName or '';
    if (job == '') then return; end
    gVana.Prefs.WeaponPick = gVana.Prefs.WeaponPick or {};
    gVana.Prefs.WeaponPick[job] = gVana.Prefs.WeaponPick[job] or {};
    gVana.Prefs.WeaponPick[job][slotName] = piece;
    core.SavePrefs();
    core.ApplyOwn();
end

-- ---------------------------------------------------------------- watching for changes

local lastLook = 0;
local signature = nil;

-- A string that changes whenever the job, level or weapons do. Gear coming into or leaving the
-- bags (a drop off a mob, the Mog House) waits for the next zone, which rebuilds anyway
-- (Ferrin 18 Oct 2026: "it should work on zones where it finds the best gear for the slot").
local function look()
    local who = bags.Who();
    local parts = { who.Job, who.Level, who.Sub, who.SubLevel };
    local equipment = gData.GetEquipment();
    for _, slot in ipairs({ 'Main', 'Sub', 'Range' }) do
        local e = equipment[slot];
        parts[#parts + 1] = (e and e.Item) and e.Item.Id or 0;
    end
    return table.concat(parts, ',');
end

-- Key binds waiting to be sent, one every half second.
local keyQueue, keyAt, keysBound = {}, 0, false;

core.QueueKeys = function(bind)
    for _, k in ipairs(require('vg.commands').MoteKeys) do
        keyQueue[#keyQueue + 1] = bind and ('/bind ' .. k[1] .. ' ' .. k[2]) or ('/unbind ' .. k[1]);
    end
    keysBound = bind;
end

-- Called every frame.
core.Tick = function()
    if (core.Holding()) then
        return;
    end
    if (gVana.Prefs.MoteKeys) and (not keysBound) then
        core.QueueKeys(true);
    end
    if (#keyQueue > 0) and (os.clock() > keyAt) then
        AshitaCore:GetChatManager():QueueCommand(-1, table.remove(keyQueue, 1));
        keyAt = os.clock() + 0.5;
    end
    if (gState.PendingAutoLoad) then
        gState.PendingAutoLoad = false;
        gState.AutoLoadProfile();
    end
    local now = os.clock();
    if (queue == nil) and (now - lastLook > 3) then
        lastLook = now;
        local s = look();
        if (s ~= signature) then
            signature = s;
            wanted = true;
        end
    end
    if (queue == nil) and (wanted) then
        wanted = false;
        startBuild();
    end
    if (queue ~= nil) then
        stepBuild(2);
    end
    require('vg.trusts').Tick();
end

core.OnSettled = function()
    wanted = true;
end

-- ---------------------------------------------------------------- helpers for the handlers

-- Does the player carry this piece in a bag they can equip from?
core.Carries = function(name)
    local want = string.lower(name);
    for _, p in ipairs(gVana.Pieces) do
        if (string.lower(p.Name) == want) then return true; end
    end
    return false;
end
gVana.Carries = core.Carries;

-- Does the built-set layer run under this profile? Always for vanagear's own profile and for
-- profiles the launcher made from GearSwap or that only have empty sets; for a profile with
-- hand-made sets only when the player turned it on with /vg underlay on.
core.Underlay = function()
    if (gProfile == nil) then
        return false;
    end
    if (gProfile.IsVanagearStock) then
        return true;
    end
    local job = gVana.Who and gVana.Who.JobName or '';
    local pref = gVana.Prefs.Underlay[job];
    if (pref ~= nil) then
        return pref;
    end
    if (gProfile.ImportedFromGearSwap) then
        return true;
    end
    for _, set in pairs(gProfile.Sets or {}) do
        if (type(set) == 'table') and (next(set) ~= nil) then
            return false;
        end
    end
    return true;
end

return core;
