--[[
    vanagear: gear used from the slot (Warp Ring, Empress Band, Capacity Ring, Dim. Rings).

    /vg warp puts the ring on, holds the slot for as long as the server makes you wear it before
    it can be used (item_usable useDelay), uses it, holds it through the activation, and then
    gives the slot back to the sets. The same as Ferrin's BST profile does, for any usable piece
    the player carries. Which word calls which piece comes from vg\lists.lua or the first word
    of the piece's name.
]]--

local lists = require('vg.lists');
local vdata = require('vg.data');

local use = {};

local active = nil;   -- { piece, slot, phase, at }

-- word -> piece, for every usable piece the player carries
use.Words = function()
    local words = {};
    for _, p in ipairs(gVana.Pieces) do
        if (p.Server.use ~= nil) then
            local word = lists.UseWords[p.Name] or string.lower(string.match(p.Name, '^[%w\']+') or p.Name);
            word = string.gsub(word, '[^%w]', '');
            if (words[word] == nil) then
                words[word] = p;
            end
        end
    end
    return words;
end

local function slotFor(piece)
    local bits = piece.Server.slot;
    if (bit.band(bits, 0x4000) ~= 0) then return 'Ring2'; end
    if (bit.band(bits, 0x2000) ~= 0) then return 'Ring1'; end
    if (bit.band(bits, 0x1000) ~= 0) then return 'Ear2'; end
    if (bit.band(bits, 0x0800) ~= 0) then return 'Ear1'; end
    for i = 16, 1, -1 do
        if (bit.band(bits, vdata.SlotBit[i]) ~= 0) then
            return gData.Constants.EquipSlotNames[i];
        end
    end
    return nil;
end

use.Start = function(word)
    local words = use.Words();
    local piece = words[word];
    if (piece == nil) then
        return false;
    end
    if (active ~= nil) then
        print(chat.header('vanagear') .. chat.message(active.piece.Name .. ' is still going.'));
        return true;
    end
    local slot = slotFor(piece);
    gFunc.ForceEquip(slot, piece.Equip);
    active = { piece = piece, slot = slot, phase = 'wait', at = os.clock() + piece.Server.use.delay + 0.5,
        command = '/item "' .. piece.Name .. '" <me>', kind = 'Item' };
    print(chat.header('vanagear') .. chat.message(string.format('%s on, using it in %d s.', piece.Name, piece.Server.use.delay)));
    return true;
end

-- ---------------------------------------------------------------- Beastmaster jugs and pet food

local function say(text)
    print(chat.header('vanagear') .. chat.message(text));
end

-- Jugs are the ammo only Beastmaster can equip; pet food is the ammo named Pet Food, Pet Fd.,
-- Pet Roborant or Pet Poultice.
local function isJug(p)
    return (p.Server.slot == 8) and (p.Server.jobs == 256) and (p.Server.skill == 0);
end

local function isFood(p)
    return (p.Server.slot == 8) and (string.find(string.lower(p.Name), '^pet ') ~= nil);
end

local function bare(s)
    return (string.gsub(string.lower(s or ''), '[^%w]', ''));
end

-- what the player carries of one kind, one entry per item name, lowest level first
local function carried(test)
    local list, seen = {}, {};
    for _, p in ipairs(gVana.Pieces) do
        if (test(p)) and (not seen[p.Name]) then
            seen[p.Name] = true;
            list[#list + 1] = p;
        end
    end
    table.sort(list, function(a, b)
        if (a.Server.level ~= b.Server.level) then return a.Server.level < b.Server.level; end
        return a.Name < b.Name;
    end);
    return list;
end

use.Jugs = function() return carried(isJug); end
use.Foods = function() return carried(isFood); end

-- the carried piece whose name holds the word: an exact name first, then the start of a word
local function find(list, word)
    local w = bare(word);
    if (w == '') then return nil; end
    for _, p in ipairs(list) do
        if (bare(p.Name) == w) then return p; end
    end
    for _, p in ipairs(list) do
        if (string.find(bare(p.Name), w, 1, true) ~= nil) then return p; end
    end
    return nil;
end

local function names(list)
    local out = {};
    for _, p in ipairs(list) do out[#out + 1] = p.Name; end
    return (#out > 0) and table.concat(out, ', ') or 'none';
end

local function ammoThen(piece, ability)
    if (active ~= nil) then
        say(active.piece.Name .. ' is still going.');
        return;
    end
    gFunc.ForceEquip('Ammo', piece.Equip);
    active = { piece = piece, slot = 'Ammo', phase = 'wait', at = os.clock() + 1,
        command = '/ja "' .. ability .. '" <me>', kind = 'Ability' };
    say(string.format('%s in, then %s.', piece.Name, ability));
end

-- /vg call [jug], /vg loyal [jug]. With no jug, the last one called, while you still carry it.
use.Call = function(ability, word)
    local jugs = use.Jugs();
    local piece;
    if (word ~= nil) and (word ~= '') then
        piece = find(jugs, word);
        if (piece == nil) then
            say(string.format('No jug matching "%s". You carry: %s.', word, names(jugs)));
            return;
        end
    else
        piece = find(jugs, gVana.Prefs.Jug);
        if (piece == nil) then
            say('Which jug? /vg ' .. ((ability == 'Call Beast') and 'call' or 'loyal') .. ' <jug>. You carry: ' .. names(jugs) .. '.');
            return;
        end
    end
    if (gVana.Prefs.Jug ~= piece.Name) then
        gVana.Prefs.Jug = piece.Name;
        require('vg.core').SavePrefs();
    end
    ammoThen(piece, ability);
end

-- Words for the pet foods that do something other than heal.
local foodWords = { debuff = { 'Pet Roborant', 'Pet Poultice' }, erase = { 'Pet Roborant', 'Pet Poultice' },
    cure = { 'Pet Roborant', 'Pet Poultice' }, roborant = { 'Pet Roborant' }, poultice = { 'Pet Poultice' } };

-- /vg reward [food]. With no food, the strongest biscuit you can use at your level.
use.Reward = function(word)
    local foods = use.Foods();
    local piece;
    local w = bare(word);
    if (w == '') then
        local level = gVana.Who and gVana.Who.Level or 99;
        for _, p in ipairs(foods) do
            if (string.find(bare(p.Name), '^petf')) and (p.Server.level <= level) then piece = p; end
        end
    elseif (foodWords[w] ~= nil) then
        for _, name in ipairs(foodWords[w]) do
            piece = piece or find(foods, name);
        end
    else
        piece = find(foods, word);
    end
    if (piece == nil) then
        say(string.format('No pet food%s. You carry: %s.', (w ~= '') and (' matching "' .. word .. '"') or '', names(foods)));
        return;
    end
    ammoThen(piece, 'Reward');
end

-- /vg wear <name>: one of your own sets, kept on over the built sets until /vg wear off.
local worn = nil;   -- { name, set }

use.Wear = function(name, set)
    worn = (set ~= nil) and { name = name, set = set } or nil;
    if (set ~= nil) then gFunc.ForceEquipSet(set); end
end

use.Worn = function()
    return worn and worn.name;
end

-- Runs after the sets and the profile on every equip pass, so the slot stays held.
use.Tick = function()
    if (worn ~= nil) then
        gFunc.EquipSet(worn.set);
    end
    local u = active;
    if (u == nil) then
        return;
    end
    local now = os.clock();
    local action = gData.GetAction();
    if (u.phase == 'wait') then
        if (now >= u.at) then
            AshitaCore:GetChatManager():QueueCommand(1, u.command);
            u.phase = 'sent';
            u.at = now;
        end
    elseif (u.phase == 'sent') then
        if (action ~= nil) and (action.ActionType == u.kind) then
            u.phase = 'active';
        elseif (u.kind == 'Ability') and (now - u.at > 3) then
            -- a job ability can come and go between two passes; the slot was held long enough
            active = nil;
            return;
        elseif (now - u.at > 6) then
            active = nil;
            print(chat.header('vanagear') .. chat.message(u.piece.Name .. ' did not go (on its reuse timer?); slot given back.'));
            return;
        end
    elseif (u.phase == 'active') and (action == nil) then
        active = nil;
        print(chat.header('vanagear') .. chat.message(u.piece.Name .. ' done; slot given back.'));
        return;
    end
    gFunc.Equip(u.slot, u.piece.Equip);
end

return use;
