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

local function augmentMods(item)
    -- Same bit layout LuAshitacast's gData.GetAugment reads; only the plain (Oseem style) and
    -- Magian augments are numbers we can score.
    if (item.Extra == nil) or (#item.Extra < 12) then
        return nil;
    end
    local augType = struct.unpack('B', item.Extra, 1);
    if (augType ~= 2) and (augType ~= 3) then
        return nil;
    end
    local augFlag = struct.unpack('B', item.Extra, 2);
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
    };
end

return bags;
