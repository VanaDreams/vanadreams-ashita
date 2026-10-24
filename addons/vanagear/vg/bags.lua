--[[
    vanagear: what the player carries.

    Reads the bags LuAshitacast can equip from (gSettings.EquipBags: Inventory and the Wardrobes)
    and turns every equippable piece into a record the builder can score. The other bags (Mog
    Safe, Storage, Locker, Satchel, Sack, Case) are read only for /vg upgrades: a piece there is
    never put in a set, because it cannot be equipped from there.
]]--

local encoding = require('encoding');
local vdata = require('vg.data');

local bags = {};

-- Bags that can only be looked at, never equipped from.
local storageBags = { 1, 2, 4, 5, 6, 7, 9 };

-- Mezzotint (Delve, Geas Fete) augments as the server applies them (src/map/items/item_equipment.cpp
-- ApplyMezzotint): three slots of client index and value; the value is the number shown less one, an
-- _II index adds 256, and an index the server has no stat for does nothing.
local mezzotint = {
    [2] = { 'HP' }, [3] = { 'HP', 256 }, [4] = { 'MP' }, [5] = { 'MP', 256 }, [8] = { 'ATT' }, [9] = { 'ATT', 256 },
    [10] = { 'RATT' }, [11] = { 'RATT', 256 }, [12] = { 'ACC' }, [13] = { 'ACC', 256 }, [14] = { 'RACC' }, [15] = { 'RACC', 256 },
    [16] = { 'DEF' }, [17] = { 'DEF', 256 }, [18] = { 'EVA' }, [19] = { 'EVA', 256 }, [20] = { 'MATT' }, [21] = { 'MATT', 256 },
    [22] = { 'MDEF' }, [23] = { 'MDEF', 256 }, [24] = { 'MACC' }, [25] = { 'MACC', 256 }, [26] = { 'MEVA' }, [27] = { 'MEVA', 256 },
    [28] = { 'DMG_RATING' }, [29] = { 'DMG_RATING', 256 },
    [128] = { 'MATT', 0, 1, true }, [129] = { 'MACC', 0, 1, true }, [130] = { 'ATT', 0, 1, true }, [131] = { 'ACC', 0, 1, true },
    [185] = { 'STORETP' }, [186] = { 'DOUBLE_ATTACK' }, [187] = { 'SNAPSHOT' },
    [188] = { 'DMGPHYS', 0, -100 }, [189] = { 'DMGMAGIC', 0, -100 },
    [191] = { 'STR' }, [192] = { 'DEX' }, [193] = { 'VIT' }, [194] = { 'AGI' }, [195] = { 'INT' }, [196] = { 'MND' }, [197] = { 'CHR' },
};

local function mezzotintMods(extra)
    local mods, pet = {}, {};
    for i = 0, 2 do
        local index = struct.unpack('B', extra, 7 + i * 2);
        local value = struct.unpack('B', extra, 8 + i * 2) + 1;
        local m = mezzotint[index];
        local id = (m ~= nil) and vdata.ModId(m[1]) or nil;
        if (id ~= nil) and (index ~= 0) then
            local v = (value + (m[2] or 0)) * (m[3] or 1);
            if (m[4]) then
                pet[0] = pet[0] or {};
                pet[0][id] = (pet[0][id] or 0) + v;
            else
                mods[id] = (mods[id] or 0) + v;
            end
        end
    end
    return { mods = mods, pet = pet };
end
bags.MezzotintMods = mezzotintMods;

local function augmentMods(item)
    -- Same bit layout LuAshitacast's gData.GetAugment reads: the plain (Oseem style) and Magian
    -- augments, and the mezzotint ones, are what the server applies and so what is scored. Odyssey
    -- and other bundled augments are stored but not applied by this server (scripts/globals/
    -- oboro_jse_neck.lua: "which this server does not apply"), so they count for nothing here too.
    if (item.Extra == nil) or (#item.Extra < 12) then
        return nil;
    end
    local augType = struct.unpack('B', item.Extra, 1);
    if (augType ~= 2) and (augType ~= 3) then
        return nil;
    end
    local augFlag = struct.unpack('B', item.Extra, 2);
    if (augType == 2) and (bit.band(augFlag, 0x20) ~= 0) then
        return mezzotintMods(item.Extra);
    end
    if (bit.band(augFlag, 0x20) ~= 0) or (augFlag == 131) or (bit.band(augFlag, 0x08) ~= 0) or (bit.band(augFlag, 0x80) ~= 0) then
        return nil;
    end
    local count = (bit.band(augFlag, 0x40) ~= 0) and 4 or 5;
    local raw = item.Extra:totable();
    local table = vdata.Augments();
    local mods, pet = {}, {};
    for i = 1, count do
        local id = ashita.bits.unpack_be(raw, 16 * i, 11);
        local level = ashita.bits.unpack_be(raw, (16 * i) + 11, 5);
        for _, a in ipairs(table[id] or {}) do
            -- the server's own sum: (value > 0 and value + level or value - level) * max(multiplier, 1)
            local v = ((a.v > 0) and (a.v + level) or (a.v - level)) * math.max(a.m, 1);
            if (a.pet ~= nil) then
                pet[a.pet] = pet[a.pet] or {};
                pet[a.pet][a.mod] = (pet[a.pet][a.mod] or 0) + v;
            else
                mods[a.mod] = (mods[a.mod] or 0) + v;
            end
        end
    end
    return { mods = mods, pet = pet };
end

-- How a set names this piece so LuAshitacast puts on exactly this one: by name, pinned to its
-- bag, with its augments when it has any.
local function equipEntry(piece)
    local augment = gData.GetAugment(piece.Item);
    local entry = { Name = piece.Name, Bag = piece.Bag };
    if (augment.Path ~= nil) then
        entry.AugPath = augment.Path;
    elseif (augment.Augs ~= nil) then
        local list = {};
        for _, v in pairs(augment.Augs) do
            list[#list + 1] = v.String;
        end
        if (#list > 0) then
            entry.Augment = list;
        end
    end
    return entry;
end

local function read(container, into, equippable)
    local inventory = AshitaCore:GetMemoryManager():GetInventory();
    local resources = AshitaCore:GetResourceManager();
    local items = vdata.Items();
    for index = 1, gData.GetContainerMax(container) do
        local item = inventory:GetContainerItem(container, index);
        if (item ~= nil) and (item.Id > 0) and (item.Id ~= 65535) and (item.Count > 0) then
            local server = items[item.Id];
            local res = resources:GetItemById(item.Id);
            if (server ~= nil) and (res ~= nil) then
                local piece = {
                    Id = item.Id,
                    Name = encoding:ShiftJIS_To_UTF8(res.Name[1]),
                    Bag = container,
                    Index = index,
                    Item = item,
                    Server = server,
                    Races = res.Races,
                    Equippable = equippable,
                };
                piece.Aug = augmentMods(item);
                piece.Equip = equipEntry(piece);
                into[#into + 1] = piece;
            end
        end
    end
end

-- Everything equippable the player carries in a bag they can equip from.
bags.Read = function()
    local pieces = {};
    for _, container in ipairs(gSettings.EquipBags) do
        if (gData.GetContainerAvailable(container)) then
            read(container, pieces, true);
        end
    end
    return pieces;
end

-- Pieces in bags that cannot be equipped from. Where the client cannot see a bag (a Mog Safe
-- outside the Mog House), it is skipped.
bags.ReadStorage = function()
    local pieces = {};
    for _, container in ipairs(storageBags) do
        if (AshitaCore:GetMemoryManager():GetInventory():GetContainerCountMax(container) > 0) then
            read(container, pieces, false);
        end
    end
    return pieces;
end

-- Can the player wear this piece in this slot (LuAshitacast index 1-16) at this job and level?
-- withSub: the support job's gear counts too (Vanadreams allows it in the player's own sets).
bags.CanWear = function(piece, slot, who, withSub)
    local s = piece.Server;
    if (bit.band(s.slot, vdata.SlotBit[slot]) == 0) then
        return false;
    end
    if (bit.band(s.jobs, bit.lshift(1, who.Job - 1)) == 0) and
       (not withSub or (who.Sub or 0) == 0 or bit.band(s.jobs, bit.lshift(1, who.Sub - 1)) == 0) then
        return false;
    end
    if (s.level > who.Level) then
        return false;
    end
    local races = piece.Races or 0;
    if (races ~= 0) and (who.Race > 0) and (bit.band(races, bit.lshift(1, who.Race)) == 0) then
        return false;
    end
    return true;
end

-- The player as the builder needs them.
bags.Who = function()
    local player = AshitaCore:GetMemoryManager():GetPlayer();
    local party = AshitaCore:GetMemoryManager():GetParty();
    local entity = AshitaCore:GetMemoryManager():GetEntity();
    local resources = AshitaCore:GetResourceManager();
    local function abbr(id)
        local s = resources:GetString('jobs.names_abbr', id);
        return (type(s) == 'string') and s:trimend('\x00') or '';
    end
    local myIndex = party:GetMemberTargetIndex(0);
    return {
        Job = player:GetMainJob(),
        JobName = abbr(player:GetMainJob()),
        Level = player:GetMainJobLevel(),
        Sub = player:GetSubJob(),
        SubName = abbr(player:GetSubJob()),
        SubLevel = player:GetSubJobLevel(),
        Race = entity:GetRace(myIndex) or 0,
        -- job points spent on the main job, for its gifts (vg\plan.lua Bonus)
        JobPoints = (player.GetJobPointsSpent ~= nil) and (player:GetJobPointsSpent(player:GetMainJob()) or 0) or 0,
        -- the zone, for gear that works only in one (vg\score.lua latents)
        Zone = (party.GetMemberZone ~= nil) and (party:GetMemberZone(0) or 0) or 0,
    };
end

return bags;
