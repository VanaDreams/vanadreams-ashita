--[[
    vanagear: the /vg window. The sets built for the job you are on, the piece in each slot and
    what it scored, and the commands that mean something for this job. Every slot can be changed
    here: the piece you pick is yours (vg\mine.lua) and replaces the built one in that set, and
    New set makes a set of your own.
]]--

local imgui = require('imgui');
local vd = require('vdstyle');

local window = { Open = false, Selected = nil };
gVana.Window = window;

local isOpen = { false };
local mineOpen = { false };
local newWord = { '' };
local newLines = { '' };
local everyJob = { false };
local mineNote = nil;
local newSet = { '' };
local setNote = nil;

local function sayNote(text) mineNote = text; end
local renderSlots;

-- Your own commands: what /vg <word> runs, a form to add one, and the folder they are kept in.
local function renderMine()
    local mine = require('vg.mine');
    mineOpen[1] = true;
    imgui.SetNextWindowSize({ 560, 380 }, ImGuiCond_FirstUseEver);
    if (vd.Begin('vanagear: your commands', mineOpen)) then
        imgui.TextWrapped('/vg <word> runs the lines in order. A line is any game command; "wait 2" pauses two seconds. '
            .. 'Put several lines in one box by ending each with ;');
        imgui.Separator();
        local list = mine.List();
        local words = {};
        for word in pairs(list) do words[#words + 1] = word; end
        table.sort(words);
        if (#words == 0) then
            imgui.TextDisabled('None yet. Example: word debuff, lines /vg reward debuff');
        end
        for _, word in ipairs(words) do
            local entry = list[word];
            if (vd.SmallButton('Run##run_' .. word)) then
                mine.Run(word);
            end
            imgui.SameLine();
            if (vd.SmallButton('Edit##edit_' .. word)) then
                newWord[1] = word;
                newLines[1] = table.concat(entry.lines, '; ');
                everyJob[1] = (entry.file == 'ALL');
            end
            imgui.SameLine();
            if (vd.SmallButton('Delete##del_' .. word)) then
                mine.Remove(word);
            end
            imgui.SameLine();
            imgui.Text('/vg ' .. word);
            imgui.SameLine();
            imgui.TextDisabled(table.concat(entry.lines, '; ') .. ((entry.file == 'ALL') and '  (every job)' or ''));
        end
        imgui.Separator();
        imgui.PushItemWidth(140);
        imgui.InputText('word##vg_mine_word', newWord, 32);
        imgui.PopItemWidth();
        imgui.SameLine();
        imgui.Checkbox('every job##vg_mine_all', everyJob);
        imgui.PushItemWidth(-1);
        imgui.InputText('##vg_mine_lines', newLines, 512);
        imgui.PopItemWidth();
        if (vd.Button('Save', nil, true)) then
            local ok, word, file = mine.Add(newWord[1], mine.Split(newLines[1]), everyJob[1]);
            if (ok) then
                sayNote(string.format('/vg %s saved in %s.lua.', word, file));
                newWord[1] = '';
                newLines[1] = '';
            else
                sayNote('Give it a word and at least one line.');
            end
        end
        imgui.SameLine();
        if (vd.Button('Open folder')) then
            mine.OpenFolder();
        end
        imgui.SameLine();
        if (vd.Button('Reload')) then
            mine.Reload();
            sayNote('Read the files again.');
        end

        if (mineNote ~= nil) then
            imgui.TextDisabled(mineNote);
        end
    end
    vd.End();
    if (not mineOpen[1]) then
        window.Mine = false;
    end
end

local function sortedNames()
    local names = {};
    for name in pairs(gVana.Sets) do names[#names + 1] = name; end
    table.sort(names);
    return names;
end

-- The pieces the player carries that can go in this slot now, one per name. The support job's
-- gear is offered too: on Vanadreams the player's own sets are where it gets worn.
local function choices(slot)
    local bags = require('vg.bags');
    local list, seen = {}, {};
    for _, p in ipairs(gVana.Pieces) do
        if (not seen[p.Name]) and (gVana.Who ~= nil) and (bags.CanWear(p, slot, gVana.Who, true)) then
            seen[p.Name] = true;
            list[#list + 1] = p.Name;
        end
    end
    table.sort(list);
    return list;
end

local function pieceName(entry)
    if (type(entry) == 'table') then return entry.Name; end
    return entry;
end

-- The right pane: every slot of the selected set, each one a list to pick a piece from.
renderSlots = function(name)
    local mine = require('vg.mine');
    local ex = gVana.Explain[name];
    local set = gVana.Sets[name] or {};
    local own = gVana.Own[name] or {};
    local built = gVana.Built[name] or {};
    local yours = next(own) ~= nil;
    vd.Eyebrow(name .. ((ex and ex.weaponskill) and ('  (' .. ex.weaponskill .. ')') or ''));
    imgui.SameLine();
    imgui.TextDisabled(yours and (gVana.Built[name] and '  built, with your changes' or '  your own set') or '  built');
    imgui.Separator();
    for slot = 1, 16 do
        local slotName = gData.Constants.EquipSlotNames[slot];
        local entry = pieceName(set[slotName]);
        local ranks = ex and ex.ranks[slot];
        imgui.Text(string.format('%-6s', slotName));
        imgui.SameLine(80);
        if (gVana.Held[slot] ~= nil) and (not own[slotName]) then
            imgui.TextDisabled(gVana.Held[slot]);
        else
            imgui.PushItemWidth(260);
            local preview = (entry or '(nothing)') .. (own[slotName] and '  *' or '');
            if (imgui.BeginCombo('##vg_slot_' .. slotName, preview, ImGuiComboFlags_None)) then
                local builtPiece = pieceName(built[slotName]);
                if (imgui.Selectable('Built: ' .. (builtPiece or 'nothing') .. '##vg_built_' .. slotName, not own[slotName])) then
                    mine.SetSlot(name, slotName, nil);
                end
                if (imgui.Selectable('Nothing (take it off)##vg_none_' .. slotName, own[slotName] and entry == 'remove')) then
                    mine.SetSlot(name, slotName, 'remove');
                end
                for _, piece in ipairs(choices(slot)) do
                    if (imgui.Selectable(piece .. '##vg_pick_' .. slotName, own[slotName] and entry == piece)) then
                        mine.SetSlot(name, slotName, piece);
                    end
                end
                imgui.EndCombo();
            end
            imgui.PopItemWidth();
            if (not own[slotName]) and (ranks ~= nil) and (ranks[1] ~= nil) then
                imgui.SameLine();
                vd.Status(string.format('+%.1f', ranks[1].gain), vd.Ok);
                if (imgui.IsItemHovered()) and (ranks[2] ~= nil) then
                    local lines = {};
                    for i = 2, math.min(4, #ranks) do
                        lines[#lines + 1] = string.format('%s  +%.1f', ranks[i].c.piece.Name, ranks[i].gain);
                    end
                    imgui.SetTooltip('Behind it:\n' .. table.concat(lines, '\n'));
                end
            end
        end
    end
    imgui.Separator();
    if (vd.Button('Wear it', nil, true)) then
        require('vg.use').Wear(name, set);
        setNote = 'Wearing ' .. name .. ' until /vg wear off.';
    end
    imgui.SameLine();
    if (vd.Button('Take what I wear')) then
        mine.SaveSet(name);
        setNote = name .. ' now holds what you wear.';
    end
    if (yours) then
        imgui.SameLine();
        if (vd.Button(gVana.Built[name] and 'Undo my changes' or 'Delete this set')) then
            mine.RemoveSet(name);
            setNote = gVana.Built[name] and (name .. ' is back to the built set.') or (name .. ' deleted.');
        end
    end
    imgui.TextDisabled('* = your pick. Name a set after an ability, weaponskill or spell and it goes on for it.');
    if (setNote ~= nil) then
        imgui.TextDisabled(setNote);
    end
end

window.Render = function()
    if (not window.Open) then
        return;
    end
    isOpen[1] = true;
    imgui.SetNextWindowSize({ 720, 480 }, ImGuiCond_FirstUseEver);
    if (vd.Begin('vanagear', isOpen)) then
        local who = gVana.Who;
        if (who == nil) then
            imgui.Text('Waiting to build: sets are made once you have been in the zone for a few seconds.');
        else
            vd.Eyebrow(string.format('%s%d/%s%d', who.JobName, who.Level, who.SubName, who.SubLevel));
            imgui.SameLine();
            imgui.TextDisabled(string.format('  built from your inventory and wardrobes at %s', os.date('%H:%M', gVana.BuiltAt or os.time())));
            if (gProfile ~= nil) and (not gProfile.IsVanagearStock) then
                imgui.TextDisabled(string.format('Your profile %s runs; its own sets win. Built sets under it: %s.',
                    tostring(gProfile.FileName), require('vg.core').Underlay() and 'on' or 'off'));
            end
            imgui.Separator();

            local names = sortedNames();
            if (window.Selected == nil) or (gVana.Sets[window.Selected] == nil) then
                window.Selected = gVana.Sets.Idle and 'Idle' or names[1];
            end

            imgui.BeginChild('sets', { 180, -60 }, ImGuiChildFlags_Borders);
            for _, name in ipairs(names) do
                if (vd.Selectable(name, window.Selected == name)) then
                    window.Selected = name;
                end
            end
            imgui.EndChild();
            imgui.SameLine();

            imgui.BeginChild('slots', { 0, -60 }, ImGuiChildFlags_Borders);
            renderSlots(window.Selected);
            imgui.EndChild();
        end
        imgui.Separator();
        imgui.PushItemWidth(160);
        imgui.InputText('##vg_new_set', newSet, 64);
        imgui.PopItemWidth();
        imgui.SameLine();
        if (vd.Button('New set', nil, true)) then
            local mine = require('vg.mine');
            local name = mine.SetName(newSet[1]);
            if (name == '') then
                setNote = 'Type a name first.';
            elseif (gVana.Sets[name] ~= nil) and (not mine.Dressing) then
                window.Selected = name;
                setNote = name .. ' is already a set: change its slots on the right.';
            else
                local _, _, count = mine.SaveSet(name);
                window.Selected = name;
                newSet[1] = '';
                setNote = string.format('%s saved from what you wear (%d pieces). Change any slot on the right.', name, count or 0);
            end
        end
        imgui.SameLine();
        local mine = require('vg.mine');
        if (vd.Button(mine.Dressing and 'Swap again' or 'Dress')) then
            mine.Dress(not mine.Dressing);
            setNote = mine.Dressing and 'Swapping paused: put gear on, type a name and press New set (or /vg save <name>).' or nil;
        end
        imgui.SameLine();
        if (vd.Button('Your commands')) then
            window.Mine = not window.Mine;
        end
        imgui.SameLine();
        imgui.TextDisabled('/vg help lists the commands for this job.  vanagear is built on LuAshitacast by ThornyFFXI (MIT).');
    end
    vd.End();
    if (not isOpen[1]) then
        window.Open = false;
    end
    if (window.Mine) and (window.Open) then
        renderMine();
    end
end

return window;
