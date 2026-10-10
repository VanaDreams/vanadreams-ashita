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
    active = { piece = piece, slot = slot, phase = 'wait', at = os.clock() + piece.Server.use.delay + 0.5 };
    print(chat.header('vanagear') .. chat.message(string.format('%s on, using it in %d s.', piece.Name, piece.Server.use.delay)));
    return true;
end

-- Runs after the sets and the profile on every equip pass, so the slot stays held.
use.Tick = function()
    local u = active;
    if (u == nil) then
        return;
    end
    local now = os.clock();
    local action = gData.GetAction();
    if (u.phase == 'wait') then
        if (now >= u.at) then
            AshitaCore:GetChatManager():QueueCommand(1, '/item "' .. u.piece.Name .. '" <me>');
            u.phase = 'sent';
            u.at = now;
        end
    elseif (u.phase == 'sent') then
        if (action ~= nil) and (action.ActionType == 'Item') then
            u.phase = 'active';
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
