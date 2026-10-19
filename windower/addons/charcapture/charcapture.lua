--[[
    charcapture for Windower 4 - snapshot a character for porting to Vanadreams.

    //capture         write addons\charcapture\data\<Name>.json and print a summary
    //capture show    print the last summary again

    The same snapshot the Ashita charcapture writes, so the launcher's Capture page sends either.
    Reads only: it sends nothing to the server and changes nothing in the game.
]]

_addon.name     = 'charcapture';
_addon.author   = 'Vanadreams';
_addon.version  = '0.1.8';
_addon.commands = { 'capture', 'charcapture' };

local snapshot = require('snapshot');
local array = snapshot.array;

local last_summary = nil;

-- What arrives only when a menu is opened, a zone is entered or a job is changed, kept for the next capture.
local seen = {
    merits = {}, merits_at = nil, job_points = {}, job_points_at = nil, logs = {}, logs_at = nil,
    currencies = {}, currencies1_at = nil, currencies2_at = nil, chocobo = nil,
    misc = {}, skills_by_job = {}, look = nil, size = nil,
};

local CONTAINERS = 16;      -- 0 Inventory .. 16 Mog Wardrobe 8, numbered as Ashita and the server number them
local STORAGE_NAME = {
    [0] = 'Inventory', [1] = 'Mog Safe', [2] = 'Storage', [3] = 'Temporary Items', [4] = 'Mog Locker',
    [5] = 'Mog Satchel', [6] = 'Mog Sack', [7] = 'Mog Case', [8] = 'Mog Wardrobe', [9] = 'Mog Safe 2',
    [10] = 'Mog Wardrobe 2', [11] = 'Mog Wardrobe 3', [12] = 'Mog Wardrobe 4', [13] = 'Mog Wardrobe 5',
    [14] = 'Mog Wardrobe 6', [15] = 'Mog Wardrobe 7', [16] = 'Mog Wardrobe 8',
};
-- Windower's names for the equipment slots, in the order Ashita and the server number them.
local EQUIP_SLOTS = { [0] = 'main', 'sub', 'range', 'ammo', 'head', 'body', 'hands', 'legs', 'feet', 'neck',
                      'waist', 'left_ear', 'right_ear', 'left_ring', 'right_ring', 'back' };
local JOB_ABBR = { 'WAR', 'MNK', 'WHM', 'BLM', 'RDM', 'THF', 'PLD', 'DRK', 'BST', 'BRD', 'RNG', 'SAM', 'NIN', 'DRG',
                   'SMN', 'BLU', 'COR', 'PUP', 'DNC', 'SCH', 'GEO', 'RUN' };
local ABILITY_CLIENT_OFFSET = 512;   -- Ashita numbers job abilities from 512 and the importer expects that

local function say(msg)
    windower.add_to_chat(207, '[charcapture] ' .. msg);
end

local function safe(f, default)
    local ok, v = pcall(f);
    if ok and v ~= nil then return v; end
    return default;
end

local function last(id)
    return safe(function () return windower.packets.last_incoming(id); end, nil);
end

local function own_id()
    return safe(function () return windower.ffxi.get_player().id; end, 0);
end

-- ---- packets ------------------------------------------------------------------------------

-- The skill list (0x062) is for the job you are on when it arrives; each job pair seen this session keeps its own
-- reading, so changing jobs in your Mog House before //capture brings every job's skills.
local function remember_skills(data)
    local player = windower.ffxi.get_player();
    if not player or not player.main_job_id or player.main_job_id == 0 then return; end
    local combat = snapshot.read_skills(data);
    if not combat then return; end
    seen.skills_by_job[('%d/%d'):format(player.main_job_id, player.sub_job_id or 0)] = {
        main = player.main_job_id, main_level = player.main_job_level,
        sub = player.sub_job_id or 0, sub_level = player.sub_job_level or 0, combat = combat,
    };
end

local function on_packet(id, data)
    if id == 0x08C then seen.merits_at = snapshot.read_merits(data, seen.merits) and os.time() or seen.merits_at;
    elseif id == 0x08D then snapshot.read_job_points(data, seen.job_points); seen.job_points_at = os.time();
    elseif id == 0x113 then snapshot.read_currencies_1(data, seen.currencies); seen.currencies1_at = os.time();
    elseif id == 0x118 then snapshot.read_currencies_2(data, seen.currencies); seen.currencies2_at = os.time();
    elseif id == 0x056 then if snapshot.read_log(data, seen.logs) then seen.logs_at = os.time(); end
    elseif id == 0x063 then snapshot.read_misc(data, seen.misc);
    elseif id == 0x051 then seen.look = snapshot.read_look_update(data) or seen.look;
    elseif id == 0x00A then
        local look, size = snapshot.read_zone_in(data);
        if look then seen.look = look; end
        if size then seen.size = size; end
    elseif id == 0x067 then seen.chocobo = snapshot.read_chocobo_sync(data, own_id()) or seen.chocobo;
    elseif id == 0x00D then seen.chocobo = snapshot.read_chocobo_update(data, own_id()) or seen.chocobo;
    elseif id == 0x062 then
        -- read once the game has applied the job change it belongs to
        coroutine.schedule(function () pcall(remember_skills, data); end, 2);
    end
end

windower.register_event('incoming chunk', function (id, original)
    pcall(on_packet, id, original);
end);

-- On load, whatever Windower still holds from before (the last zone-in, job info, skills, merit and job point
-- totals) counts as seen; the menus and logs still have to be opened or zoned through with the addon loaded.
local function catch_up()
    for _, id in ipairs({ 0x00A, 0x051, 0x063 }) do
        local data = last(id);
        if data then pcall(on_packet, id, data); end
    end
    local skills = last(0x062);
    if skills then pcall(remember_skills, skills); end
end

-- ---- the snapshot -------------------------------------------------------------------------

local function server_name()
    local info = windower.ffxi.get_info() or {};
    local name = safe(function () return require('resources').servers[info.server].en; end, nil);
    return name and ('retail (' .. name .. ')') or 'unknown (Windower)';
end

local function capture()
    local player = windower.ffxi.get_player();
    if not player or not player.name then say('log in first'); return nil; end
    local status = snapshot.read_status(last(0x061) or '') or {};
    local levels, master = snapshot.read_job_info(last(0x01B) or '');
    local combat, craft = snapshot.read_skills(last(0x062) or '');
    local look = seen.look or {};
    local misc = seen.misc;

    local snap = {
        schema = 1, addon = 'charcapture', addon_version = _addon.version, client = 'windower',
        captured_at = os.date('!%Y-%m-%dT%H:%M:%SZ'),
        captured_on = server_name(),
        character = {
            name = player.name, server_id = player.id,
            race = look.race or 0, face = look.face or 0, size = seen.size,
            nation = status.nation or 0, rank = status.rank or 0, rank_points = status.rank_points or 0,
            title = status.title or 0, homepoint = status.homepoint or 0, residence = 0,
        },
        jobs = {
            main = player.main_job_id or status.main or 0, main_level = player.main_job_level or status.main_level or 0,
            sub = player.sub_job_id or status.sub or 0, sub_level = player.sub_job_level or status.sub_level or 0,
            levels = levels or {}, master_levels = master or {}, job_points = misc.job_points or {},
            merits = { points = misc.merit_points or 0, max = misc.merit_points_max or 0 },
            limit_points = misc.limit_points or 0,
            exp = { current = status.exp_current or 0, needed = status.exp_needed or 0 },
        },
        skills = { combat = combat or {}, craft = craft or {}, by_job = {} },
        spells = array(), abilities = array(), weaponskills = array(), traits = array(), key_items = array(),
        gil = 0, inventory = {}, equipment = {},
        look = {
            hair = look.face or 0, head = look.head or 0, body = look.body or 0, hands = look.hands or 0,
            legs = look.legs or 0, feet = look.feet or 0, main = look.main or 0, sub = look.sub or 0, ranged = look.ranged or 0,
        },
        merit_upgrades = seen.merits_at and seen.merits or nil,
        job_point_upgrades = seen.job_points_at and seen.job_points or nil,
        quest_mission_packets = seen.logs_at and seen.logs or nil,
        currencies = (seen.currencies1_at or seen.currencies2_at) and seen.currencies or nil,
        personal_chocobo = seen.chocobo and { properties = seen.chocobo } or nil,
        not_captured = array({ 'fame (the game never sends the number)', 'linkshells', 'mog house layout' }),
    };
    local missing = snap.not_captured;
    if not seen.merits_at then missing[#missing + 1] = 'merit upgrades (open the Merit Points menu, then //capture again)'; end
    if not seen.job_points_at then missing[#missing + 1] = 'job point upgrades (open the Job Points menu, then //capture again)'; end
    if not seen.logs_at then missing[#missing + 1] = 'quests and missions (zone once with charcapture loaded, then //capture again)'; end
    if not seen.currencies1_at then missing[#missing + 1] = 'currencies, first tab (open Profile > Currencies, then //capture again)'; end
    if not seen.currencies2_at then missing[#missing + 1] = 'currencies, second tab (open the Currencies 2 tab of the same menu, then //capture again)'; end
    if not seen.chocobo then missing[#missing + 1] = 'your own chocobo (call it with the Chocobo Whistle and ride it, then //capture again; skip this if you have none)'; end
    if not seen.look then missing[#missing + 1] = 'race, face and looks (zone once with charcapture loaded, then //capture again)'; end
    if not misc.job_points then missing[#missing + 1] = 'job points and capacity points (zone once with charcapture loaded, then //capture again)'; end
    if not levels then missing[#missing + 1] = 'job levels (zone once with charcapture loaded, then //capture again)'; end

    pcall(remember_skills, last(0x062) or '');
    for k, v in pairs(seen.skills_by_job) do snap.skills.by_job[k] = v; end

    -- known things
    local spells = safe(function () return windower.ffxi.get_spells(); end, {});
    local spell_ids = {};
    for id, known in pairs(spells) do if known == true then spell_ids[#spell_ids + 1] = id; end end
    table.sort(spell_ids);
    for _, id in ipairs(spell_ids) do snap.spells[#snap.spells + 1] = id; end

    local abilities = safe(function () return windower.ffxi.get_abilities(); end, {});
    local function add_ids(into, list, offset)
        local ids = {};
        for _, id in pairs(list or {}) do ids[#ids + 1] = id + (offset or 0); end
        table.sort(ids);
        for _, id in ipairs(ids) do into[#into + 1] = id; end
    end
    add_ids(snap.abilities, abilities.job_abilities, ABILITY_CLIENT_OFFSET);
    add_ids(snap.weaponskills, abilities.weapon_skills);
    add_ids(snap.traits, abilities.job_traits);
    add_ids(snap.key_items, safe(function () return windower.ffxi.get_key_items(); end, {}));

    -- bags
    local items = safe(function () return windower.ffxi.get_items(); end, {});
    snap.gil = items.gil or 0;
    local item_count, unsynced = 0, {};
    for c = 0, CONTAINERS do
        local bag = safe(function () return windower.ffxi.get_items(c); end, nil);
        local list = array();
        if not bag or not bag.max or bag.max == 0 or bag.enabled == false then
            if c ~= 0 then unsynced[c] = true; end
        else
            for slot = 1, bag.max do
                local it = bag[slot];
                if it and it.id and it.id ~= 0 and it.id ~= 65535 and (it.count or 0) > 0 then
                    list[#list + 1] = { slot = slot, id = it.id, count = it.count, flags = it.status or 0, price = it.bazaar or 0, extra = snapshot.hex(it.extdata) };
                    item_count = item_count + 1;
                end
            end
        end
        snap.inventory[tostring(c)] = list;
    end

    -- equipment
    local worn = items.equipment or {};
    local unsynced_worn, unsynced_seen = {}, {};
    for slot = 0, 15 do
        local name = EQUIP_SLOTS[slot];
        local index, container = worn[name], worn[name .. '_bag'];
        if index and index ~= 0 and container then
            local bag = safe(function () return windower.ffxi.get_items(container, index); end, nil);
            snap.equipment[tostring(slot)] = { container = container, slot = index, id = bag and bag.id or 0 };
            if unsynced[container] and not unsynced_seen[container] then
                unsynced_seen[container] = true;
                unsynced_worn[#unsynced_worn + 1] = STORAGE_NAME[container] or ('storage ' .. container);
            end
        end
    end
    if #unsynced_worn > 0 then
        missing[#missing + 1] = ('gear worn from %s (open it in your menu once, then //capture again)'):format(table.concat(unsynced_worn, ', '));
    end

    -- write
    local dir = windower.addon_path .. 'data/';
    if not windower.dir_exists(dir) then windower.create_dir(dir); end
    local path = dir .. player.name:gsub('[^%w]', '') .. '.json';
    local f = io.open(path, 'w');
    if not f then say('could not write ' .. path); return nil; end
    f:write(snapshot.encode(snap));
    f:close();

    last_summary = ('%s, %s%d, %d items across all bags, %d gil, %d spells, %d key items -> %s'):format(
        player.name, JOB_ABBR[snap.jobs.main] or tostring(snap.jobs.main), snap.jobs.main_level, item_count, snap.gil,
        #snap.spells, #snap.key_items, path);
    say(last_summary);
    if #unsynced_worn > 0 then
        say(('Open %s once (Windower has not seen it this session, so the gear you wear from it came out blank), then //capture again.'):format(table.concat(unsynced_worn, ', ')));
    end
    say('now send it from the Vanadreams launcher: Capture > Send to Vanadreams.');
    return path;
end

windower.register_event('addon command', function (sub)
    if (sub or ''):lower() == 'show' then
        say(last_summary or 'nothing captured yet; //capture to take a snapshot');
    else
        local ok, err = pcall(capture);
        if not ok then say('capture failed: ' .. tostring(err)); end
    end
end);

windower.register_event('load', function ()
    catch_up();
    say('loaded. Zone once, open the Merit Points, Job Points and both Currencies menus, then //capture writes your character snapshot. The game only shows the skills of the job you are on: switch to your other jobs in your Mog House first and their skills come too.');
end);
