--[[
    pktlog - records what the game tells your client, for rebuilding a zone.

    Read-only: it never changes, blocks or sends a packet, and it has NO chat command - Ashita
    loads and unloads it (binds in scripts\vanadreams.txt: Ctrl+Alt+F12 load, Ctrl+Alt+F11
    unload). It runs from load until unload, across as many zones as you walk.

    Six files per zone, under Ashita\logs\pktlog\<character>_<date>_<time>_zone<id>.*
        .txt         every packet, hex
        .csv         every entity seen: index, id, name, position, look
        _scan.csv    widescan results: index, level, type, dx, dz (offsets from the player), x, z (world), name
                     (Vanadreams sends no name in a widescan entry; retail does)
        _loot.csv    kills and what dropped from them
        _events.csv  event ids and parameters from NPCs you talk to, AND the option
                     number your own client sends back for each menu pick
        _items.csv   every item the server puts in your bags: id, quantity, bag, slot. The
                     'what' column separates a new item from a quantity change, and 'seconds'
                     tells you whether it arrived in the zone-in inventory sync or later.

    Field offsets are read from the server's own packet structs (src/map/packets/s2c).
--]]

addon.name    = 'pktlog';
addon.author  = 'Vanadreams';
addon.version = '3.4';
addon.desc    = 'Records entities, widescan, kills, drops, events, menu choices and items, per zone, until unloaded.';

require('common');

local log = { zone = 0, started = 0, count = 0, path = nil, seen = {}, names = {}, entities = 0, kills = 0, drops = 0, scans = 0, events = 0, items = 0 };
local files = { raw = nil, ent = nil, scan = nil, loot = nil, event = nil, item = nil };

local function say(msg)
    print(('\30\08[pktlog]\30\01 %s'):format(msg));
end

local function hex(data)
    return (data:gsub('.', function (c) return ('%02X'):format(c:byte()); end));
end

local function stamp()
    return ('%s %+9.3f'):format(os.date('%H:%M:%S'), os.clock() - log.started);
end

local function clock()
    return os.date('%H:%M:%S');
end

-- a fixed-length name field, cut at its first zero
local function readName(data, offset, length)
    local raw = data:sub(offset + 1, offset + length);
    return (raw:gsub('%z.*$', ''):gsub('[^%w%s\'-]', ''));
end

local function currentZone()
    local party = AshitaCore:GetMemoryManager():GetParty();
    return party and party:GetMemberZone(0) or 0;
end

local function closeFiles(why)
    if files.raw ~= nil then
        files.raw:write(('%s ---- closed (%s): %d packets, %d entities, %d widescan, %d kills, %d drops, %d events, %d items\n')
            :format(stamp(), why, log.count, log.entities, log.scans, log.kills, log.drops, log.events, log.items));
    end
    for _, f in pairs(files) do
        if f ~= nil then
            f:close();
        end
    end
    if log.path ~= nil then
        say(('zone %d done (%s): %d entities, %d widescan, %d kills, %d drops, %d events, %d items -> %s')
            :format(log.zone, why, log.entities, log.scans, log.kills, log.drops, log.events, log.items, log.path));
    end
    files = { raw = nil, ent = nil, scan = nil, loot = nil, event = nil, item = nil };
    log.path, log.count, log.entities, log.scans, log.kills, log.drops, log.events, log.items = nil, 0, 0, 0, 0, 0, 0, 0;
    log.seen, log.names = {}, {};
    slotItem, slotQty = {}, {};
end

local function openFiles()
    local dir = ('%slogs\\pktlog\\'):format(AshitaCore:GetInstallPath());
    ashita.fs.create_directory(dir);

    local name = AshitaCore:GetMemoryManager():GetParty():GetMemberName(0);
    if name == nil or name == '' then
        name = 'unknown';
    end

    local base = ('%s%s_%s_zone%d'):format(dir, name, os.date('%Y-%m-%d_%H%M%S'), log.zone);
    log.path   = base .. '.txt';
    files.raw   = io.open(base .. '.txt', 'w');
    files.ent   = io.open(base .. '.csv', 'w');
    files.scan  = io.open(base .. '_scan.csv', 'w');
    files.loot  = io.open(base .. '_loot.csv', 'w');
    files.event = io.open(base .. '_events.csv', 'w');
    files.item  = io.open(base .. '_items.csv', 'w');
    for _, f in pairs(files) do
        if f == nil then
            say('could not open the files under ' .. dir);
            closeFiles('could not open the files');
            return;
        end
        f:setvbuf('line');
    end

    log.started = os.clock();
    files.raw:write(('# pktlog %s  character %s  zone %d  columns: time  seconds  IN/OUT  id  size  data(hex)\n')
        :format(os.date('%Y-%m-%d'), name, log.zone));
    files.ent:write('time,index,serverId,name,x,y,z,rotation,look\n');
    files.scan:write('time,index,level,type,dx,dz,x,z,name\n');
    files.loot:write('time,what,mobId,mobName,itemId,gil\n');
    files.event:write('time,direction,npcId,npcName,eventId,zone,mode,option,numbers\n');
    files.item:write('time,seconds,what,itemId,quantity,bag,slot,price\n');
    say(('logging zone %d to %s'):format(log.zone, log.path));
end

-- 0x00E: an entity the client is told about. 0x04 id, 0x08 index, 0x0B rotation,
-- 0x0C x, 0x10 y, 0x14 z, 0x30 look. Where the name sits depends on the look's first byte:
-- 0x00 (a model id) puts it at 0x34; anything else carries a 24-byte look and the name, when
-- the server sends one at all, starts at 0x48. Reading 0x34 regardless printed look bytes as
-- names for every equipment-look NPC (the Adoulin coalition staff came out as " 0P"), so an
-- entity with no name is now written with a blank name and its id, which resolves against
-- the server's npcs.yaml. (Ferrin's Western Adoulin capture, 28 Sept 2026.)
local function readEntity(e)
    local serverId = struct.unpack('I4', e.data, 0x04 + 1);
    local index    = struct.unpack('H',  e.data, 0x08 + 1);
    if index == 0 then
        return;
    end

    local lookType   = (e.size > 0x30) and struct.unpack('B', e.data, 0x30 + 1) or 0;
    local nameOffset = (lookType == 0) and 0x34 or 0x48;
    local name = (e.size > nameOffset) and readName(e.data, nameOffset, e.size - nameOffset) or '';
    if name ~= '' then
        log.names[serverId] = name;
    end

    -- one row when first seen, and one more if a name arrives for an entity first seen without one
    local known = log.seen[index];
    if known == nil or (known == '' and name ~= '') then
        if known == nil then
            log.entities = log.entities + 1;
        end
        log.seen[index] = name;
        files.ent:write(('%s,%d,%d,"%s",%.3f,%.3f,%.3f,%d,%s\n'):format(
            clock(), index, serverId, name,
            struct.unpack('f', e.data, 0x0C + 1),
            struct.unpack('f', e.data, 0x10 + 1),
            struct.unpack('f', e.data, 0x14 + 1),
            struct.unpack('B', e.data, 0x0B + 1),
            hex(e.data:sub(0x30 + 1, math.min(0x34, e.size)))));
    end
end

-- 0x0F4 widescan entry: 0x04 index, 0x06 level, 0x07 type, 0x08 x, 0x0A z, 0x0C name[16].
-- The x and z in the packet are the entity's distance from the player, not world coordinates
-- (the server's own constructor, s2c/0x0f4_tracking_list.cpp: "Difference in x-value between
-- character and object coordinates"; Ferrin's Ceizak capture of 28 Sept 2026 read (11, 31) for an
-- entity whose 0x00E said (364.5, 188.0)). Both are written: the offsets, and the world position
-- made by adding the player's own position at the moment of the scan.
local function readWidescan(e)
    if e.size < 0x1C then
        return;
    end
    local dx = struct.unpack('h', e.data, 0x08 + 1);
    local dz = struct.unpack('h', e.data, 0x0A + 1);
    local px, pz = 0, 0;
    local me = AshitaCore:GetMemoryManager():GetParty():GetMemberTargetIndex(0);
    if me ~= nil and me > 0 then
        local ent = AshitaCore:GetMemoryManager():GetEntity();
        px = ent:GetLocalPositionX(me) or 0;
        pz = ent:GetLocalPositionZ(me) or 0;
    end
    log.scans = log.scans + 1;
    files.scan:write(('%s,%d,%d,%d,%d,%d,%.1f,%.1f,"%s"\n'):format(
        clock(),
        struct.unpack('H', e.data, 0x04 + 1),
        struct.unpack('B', e.data, 0x06 + 1),
        struct.unpack('B', e.data, 0x07 + 1) % 8,
        dx, dz, px + dx, pz + dz,
        readName(e.data, 0x0C, 16)));
end

-- Battle message, 0x029 and 0x02D alike: 0x08 the target, 0x18 the message number.
-- 6 = "defeats <target>", 20 = "<target> falls to the ground". Confirmed against a capture:
-- a 0x029 with message 6 naming 17260568 (Sylvestre), seconds before that mob's drops.
local function readBattleMessage(e)
    if e.size < 0x1C then
        return;
    end
    local message = struct.unpack('H', e.data, 0x18 + 1);
    if message ~= 6 and message ~= 20 then
        return;
    end
    local mobId = struct.unpack('I4', e.data, 0x08 + 1);
    log.kills = log.kills + 1;
    files.loot:write(('%s,kill,%d,"%s",,\n'):format(clock(), mobId, log.names[mobId] or ''));
end

-- 0x0D2 treasure pool entry: 0x08 the mob it came from, 0x0C gil, 0x10 item id.
local function readTreasure(e)
    if e.size < 0x14 then
        return;
    end
    local mobId  = struct.unpack('I4', e.data, 0x08 + 1);
    local gil    = struct.unpack('H',  e.data, 0x0C + 1);
    local itemId = struct.unpack('H',  e.data, 0x10 + 1);

    -- The pool's empty slots arrive as 0x0D2 with nothing in them, usually on zoning in. Writing
    -- those as drops filled the file with rows of mob 0 and item 0 and taught us nothing.
    if itemId == 0 and gil == 0 then
        return;
    end

    log.drops = log.drops + 1;
    files.loot:write(('%s,drop,%d,"%s",%d,%d\n'):format(clock(), mobId, log.names[mobId] or '', itemId, gil));
end

-- 0x032 event: 0x04 npc id, 0x08 index, then EventNum, EventPara, Mode.
-- 0x034 event with numbers: 0x04 npc id, 0x08 num[8], 0x28 index, then the same three.
-- NOTE, from the server's own constructor (0x034_eventnum.cpp): EventNum carries the ZONE and
-- EventPara carries the event id the scripts use. Proven: a Survival Guide in Bibiki Bay sent
-- EventNum 4 (the zone) and EventPara 8500, which is startEvent(8500) in survival_guide.lua.
local function readEvent(e, withNumbers)
    local base    = withNumbers and 0x28 or 0x08;
    if e.size < base + 8 then
        return;
    end
    local npcId   = struct.unpack('I4', e.data, 0x04 + 1);
    local zoneOf  = struct.unpack('H',  e.data, base + 2 + 1);
    local eventId = struct.unpack('H',  e.data, base + 4 + 1);
    local mode    = struct.unpack('H',  e.data, base + 6 + 1);

    local numbers = '';
    if withNumbers then
        local parts = {};
        for i = 0, 7 do
            parts[#parts + 1] = tostring(struct.unpack('i4', e.data, 0x08 + (i * 4) + 1));
        end
        numbers = table.concat(parts, ' ');
    end

    log.events = log.events + 1;
    files.event:write(('%s,in,%d,"%s",%d,%d,%d,,"%s"\n')
        :format(clock(), npcId, log.names[npcId] or '', eventId, zoneOf, mode, numbers));
end

-- IN 0x05C, the server answering a mid-event choice with fresh numbers for the running event:
-- 0x04 num[8], nothing else. Written as direction 'upd' so the numbers a menu option changed
-- sit in the same file as the option (Bastingart's counter, Western Adoulin, 28 Sept 2026).
local function readEventUpdate(e)
    if e.size < 0x24 then
        return;
    end
    local parts = {};
    for i = 0, 7 do
        parts[#parts + 1] = tostring(struct.unpack('i4', e.data, 0x04 + (i * 4) + 1));
    end
    log.events = log.events + 1;
    files.event:write(('%s,upd,,,,%d,,,"%s"\n'):format(clock(), log.zone, table.concat(parts, ' ')));
end

-- OUT 0x05B, the client ending or updating an event. From the server's own struct
-- (c2s/0x05b_eventend.h): 0x04 UniqueNo, 0x08 EndPara, 0x0C ActIndex, 0x0E Mode, 0x10 EventNum,
-- 0x12 EventPara. EndPara is the option the player picked, and EventPara is the event id - the
-- server reads exactly these two as the `csid` and `option` a script sees in onEventFinish.
-- Mode 0 ends the event, 1 is an update mid-event. This is the half that says which menu line
-- a number means, e.g. Mog_Dinghy option 2 -> Eastern Adoulin.
local function readChoice(e)
    if e.size < 0x14 then
        return;
    end
    local npcId = struct.unpack('I4', e.data, 0x04 + 1);
    log.events = log.events + 1;
    files.event:write(('%s,out,%d,"%s",%d,%d,%d,%d,\n'):format(
        clock(), npcId, log.names[npcId] or '',
        struct.unpack('H',  e.data, 0x12 + 1),
        struct.unpack('H',  e.data, 0x10 + 1),
        struct.unpack('H',  e.data, 0x0E + 1),
        struct.unpack('i4', e.data, 0x08 + 1)));
end

-- What sits in each bag and slot. 0x01E says a slot's quantity changed but NOT which item it is,
-- so the only way to read one is to remember what 0x020 put there.
local slotItem = {};
local slotQty  = {};

local function slotKey(bag, slot)
    return (bag * 256) + slot;
end

-- 0x020 item attributes, sent when an item lands in your bags. From s2c/0x020_item_attr.h:
-- 0x04 ItemNum (the quantity), 0x08 Price, 0x0C ItemNo (the item id), 0x0E Category (the bag,
-- one byte), 0x0F ItemIndex (the slot). This is what records what a harvest, a chest or a
-- quest reward actually gave.
local function readItem(e)
    if e.size < 0x10 then
        return;
    end

    local itemId = struct.unpack('H', e.data, 0x0C + 1);
    if itemId == 0 then
        return;
    end

    local quantity = struct.unpack('I4', e.data, 0x04 + 1);
    local bag      = struct.unpack('B',  e.data, 0x0E + 1);
    local slot     = struct.unpack('B',  e.data, 0x0F + 1);
    local key      = slotKey(bag, slot);

    local what = 'item';
    if slotItem[key] == itemId and slotQty[key] ~= nil and quantity > slotQty[key] then
        what = 'more';
    end

    slotItem[key] = itemId;
    slotQty[key]  = quantity;

    log.items = log.items + 1;
    files.item:write(('%s,%.3f,%s,%d,%d,%d,%d,%d\n'):format(
        clock(), os.clock() - log.started, what, itemId, quantity, bag, slot,
        struct.unpack('I4', e.data, 0x08 + 1)));
end

-- 0x01E, a slot's quantity changing. From s2c/0x01e_item_num.h: 0x04 ItemNum (the NEW quantity),
-- 0x08 Category (the bag), 0x09 ItemIndex (the slot). It does not name the item, so it is resolved
-- against whatever 0x020 last put in that slot. This is how a harvest of something you already
-- hold arrives: stacking onto an existing pile is otherwise invisible.
local function readItemCount(e)
    if e.size < 0x0A then
        return;
    end

    local quantity = struct.unpack('I4', e.data, 0x04 + 1);
    local bag      = struct.unpack('B',  e.data, 0x08 + 1);
    local slot     = struct.unpack('B',  e.data, 0x09 + 1);
    local key      = slotKey(bag, slot);
    local itemId   = slotItem[key];
    local before   = slotQty[key];

    slotQty[key] = quantity;

    if itemId == nil then
        return;
    end

    local what = 'count';
    if before ~= nil and quantity > before then
        what = 'more';
    elseif before ~= nil and quantity < before then
        what = 'fewer';
    end

    log.items = log.items + 1;
    files.item:write(('%s,%.3f,%s,%d,%d,%d,%d,\n'):format(
        clock(), os.clock() - log.started, what, itemId, quantity, bag, slot));
end

-- 0x02A: an NPC saying a line with no event behind it. 0x04 the NPC, 0x18 index, 0x1A message id.
-- Terry's Five of Spades gave message 7272, which is that zone's FIVEOFSPADES_DIALOG.
local function readTalkMessage(e)
    if e.size < 0x1C then
        return;
    end
    local npcId = struct.unpack('I4', e.data, 0x04 + 1);
    log.events = log.events + 1;
    files.event:write(('%s,in,%d,"%s",,,message,,"message id %d"\n')
        :format(clock(), npcId, log.names[npcId] or '', struct.unpack('H', e.data, 0x1A + 1)));
end

local function writePacket(dir, e)
    if files.raw == nil then
        return;
    end
    files.raw:write(('%s %s 0x%03X %4d %s\n'):format(stamp(), dir, e.id, e.size, hex(e.data:sub(1, e.size))));
    log.count = log.count + 1;
end

ashita.events.register('load', 'pktlog_load', function ()
    log.zone = currentZone();
    openFiles();
end);

ashita.events.register('packet_in', 'pktlog_in', function (e)
    writePacket('IN ', e);
    if files.ent == nil then
        return;
    end

    if e.id == 0x00E then
        readEntity(e);
    elseif e.id == 0x0F4 then
        readWidescan(e);
    elseif e.id == 0x029 or e.id == 0x02D then
        readBattleMessage(e);
    elseif e.id == 0x0D2 then
        readTreasure(e);
    elseif e.id == 0x032 then
        readEvent(e, false);
    elseif e.id == 0x034 then
        readEvent(e, true);
    elseif e.id == 0x05C then
        readEventUpdate(e);
    elseif e.id == 0x02A then
        readTalkMessage(e);
    elseif e.id == 0x020 then
        readItem(e);
    elseif e.id == 0x01E then
        readItemCount(e);
    end
end);

ashita.events.register('packet_out', 'pktlog_out', function (e)
    writePacket('OUT', e);
    if files.event ~= nil and e.id == 0x05B then
        readChoice(e);
    end
end);

ashita.events.register('d3d_present', 'pktlog_tick', function ()
    local zone = currentZone();
    if zone ~= 0 and zone ~= log.zone then
        closeFiles('left the zone');
        log.zone = zone;
        openFiles();
    end
end);

ashita.events.register('unload', 'pktlog_unload', function ()
    closeFiles('unloaded');
end);
