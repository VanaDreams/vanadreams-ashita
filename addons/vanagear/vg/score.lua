--[[
    vanagear: scoring gear and building a set.

    A mode is a table of weights (how much one point of a stat is worth) and caps (the most of a
    stat gear can usefully give). A set's score is the weighted sum of its stats, each stat cut
    off at its cap. The set is built by filling every slot with its best piece, then going round
    the slots again, trying every piece in each slot with the others held, until a round changes
    nothing. The caps are why it goes round: once gear haste is at 25%, another haste piece is
    worth nothing and its slot goes to something else.
]]--

local vdata = require('vg.data');
local bags = require('vg.bags');

local score = {};

-- Server units are not the units players think in: haste and damage taken are kept in hundredths
-- of a percent. Weights and caps in the job files are written per point as players read them
-- (1% haste, 1% damage taken), and this converts.
local unit = {
    HASTE_GEAR = 100, HASTE_MAGIC = 100,
    DMG = 100, DMGPHYS = 100, DMGPHYS_II = 100, DMGMAGIC = 100, DMGMAGIC_II = 100, DMGBREATH = 100, DMGRANGE = 100,
    UDMGPHYS = 100, UDMGMAGIC = 100,
    PDT = 100, MDT = 100,
};

-- Stats that are a sum of server mods and share one cap: physical damage taken is general
-- damage taken plus physical damage taken, both against the same -50%.
local virtual = {
    PDT = { 'DMG', 'DMGPHYS' },
    MDT = { 'DMG', 'DMGMAGIC' },
};

-- Weights and caps by stat name -> by key, in server units. Keys: 'm<mod id>' for the player's
-- stats, 'p<mod id>' for the pet's, 'v<NAME>' for a virtual stat.
score.Compile = function(mode)
    local c = { w = {}, cap = {}, parts = {}, Name = mode.Name };
    local function key(name, pet)
        if (virtual[name] ~= nil) and (not pet) then
            return 'v' .. name;
        end
        local id = vdata.ModId(name);
        return id and ((pet and 'p' or 'm') .. id) or nil;
    end
    local function weights(from, pet)
        for name, weight in pairs(from or {}) do
            local k = key(name, pet);
            if (k ~= nil) then
                c.w[k] = (c.w[k] or 0) + weight / (unit[name] or 1);
            elseif (vdata.ModId(name) == nil) and (virtual[name] == nil) then
                print(chat.header('vanagear') .. chat.error('Unknown stat in ' .. tostring(mode.Name) .. ': ' .. name));
            end
        end
    end
    local function caps(from, pet)
        for name, cap in pairs(from or {}) do
            local k = key(name, pet);
            if (k ~= nil) then c.cap[k] = cap * (unit[name] or 1); end
        end
    end
    weights(mode.Stats, false);
    weights(mode.Pet, true);
    caps(mode.Caps, false);
    caps(mode.PetCaps, true);
    -- which server mods feed each virtual stat
    for name, mods in pairs(virtual) do
        if (c.w['v' .. name] ~= nil) then
            for _, modName in ipairs(mods) do
                local id = vdata.ModId(modName);
                if (id ~= nil) then
                    c.parts[id] = c.parts[id] or {};
                    table.insert(c.parts[id], 'v' .. name);
                end
            end
        end
    end
    c.HasPet = (mode.Pet ~= nil) and (next(mode.Pet) ~= nil);
    return c;
end

-- The weighted stats one piece gives in one slot: { [key] = value }.
local function vector(piece, slot, mode, petTypes)
    local v = {};
    local function add(mods, prefix)
        for mod, value in pairs(mods or {}) do
            local k = prefix .. mod;
            if (mode.w[k] ~= nil) then
                v[k] = (v[k] or 0) + value;
            end
            if (prefix == 'm') and (mode.parts[mod] ~= nil) then
                for _, vk in ipairs(mode.parts[mod]) do
                    v[vk] = (v[vk] or 0) + value;
                end
            end
        end
    end
    local s = piece.Server;
    add(s.mods, 'm');
    if (s.inslot ~= nil) then
        add(s.inslot[slot - 1], 'm');
    end
    if (piece.Aug ~= nil) then
        add(piece.Aug.mods, 'm');
    end
    if (mode.HasPet) then
        for _, t in ipairs(petTypes or {}) do
            add(s.pet and s.pet[t], 'p');
            add(piece.Aug and piece.Aug.pet[t], 'p');
        end
    end
    return v;
end
score.Vector = vector;

local function total(mode, totals)
    local sum = 0;
    for k, value in pairs(totals) do
        local w = mode.w[k];
        if (w ~= nil) then
            local cap = mode.cap[k];
            if (cap ~= nil) then
                if (cap > 0) and (value > cap) then value = cap; end
                if (cap < 0) and (value < cap) then value = cap; end
            end
            sum = sum + w * value;
        end
    end
    return sum;
end
score.Total = total;

local function apply(totals, v, sign)
    for k, value in pairs(v) do
        totals[k] = (totals[k] or 0) + sign * value;
    end
end

-- Builds one set over the given slots. Returns { [slot] = piece }, the ranked candidates per
-- slot from the last round (for /vg why), the set's score and its stat totals.
score.Build = function(mode, pieces, who, slots, petTypes)
    local cands = {};
    for _, slot in ipairs(slots) do
        cands[slot] = {};
        for _, piece in ipairs(pieces) do
            if (piece.Equippable) and (bags.CanWear(piece, slot, who)) then
                local v = vector(piece, slot, mode, petTypes);
                if (next(v) ~= nil) then
                    cands[slot][#cands[slot] + 1] = { piece = piece, v = v };
                end
            end
        end
    end

    local choice, used, totals, ranks = {}, {}, {}, {};
    for round = 1, 8 do
        local changed = false;
        for _, slot in ipairs(slots) do
            local held = choice[slot];
            if (held ~= nil) then
                apply(totals, held.v, -1);
                used[held.piece] = nil;
            end
            local base = total(mode, totals);
            local ranked = {};
            for _, c in ipairs(cands[slot]) do
                if (not used[c.piece]) then
                    apply(totals, c.v, 1);
                    ranked[#ranked + 1] = { c = c, gain = total(mode, totals) - base };
                    apply(totals, c.v, -1);
                end
            end
            -- best gain first; on a tie the piece already held stays, so the rounds settle
            table.sort(ranked, function(a, b)
                if (math.abs(a.gain - b.gain) > 1e-9) then return a.gain > b.gain; end
                if (a.c == held) ~= (b.c == held) then return a.c == held; end
                return a.c.piece.Id < b.c.piece.Id;
            end);
            ranks[slot] = ranked;
            local best = (ranked[1] ~= nil) and (ranked[1].gain > 1e-9) and ranked[1].c or nil;
            if (best ~= nil) then
                apply(totals, best.v, 1);
                used[best.piece] = true;
            end
            if (best ~= held) then
                changed = true;
            end
            choice[slot] = best;
        end
        if (not changed) then
            break;
        end
    end

    local set = {};
    for slot, c in pairs(choice) do
        set[slot] = c.piece;
    end
    return set, ranks, total(mode, totals), totals;
end

return score;
