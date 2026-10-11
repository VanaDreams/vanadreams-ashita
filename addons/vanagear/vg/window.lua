--[[
    vanagear: the /vg window. The sets built for the job you are on, the piece in each slot and
    what it scored, and the commands that mean something for this job.
]]--

local imgui = require('imgui');

local window = { Open = false, Selected = nil };
gVana.Window = window;

local isOpen = { false };
local mineOpen = { false };
local newWord = { '' };
local newLines = { '' };
local everyJob = { false };
local mineNote = nil;
local newSet = { '' };

local function sayNote(text) mineNote = text; end

-- Your own commands: what /vg <word> runs, a form to add one, and the folder they are kept in.
local function renderMine()
    local mine = require('vg.mine');
    mineOpen[1] = true;
    imgui.SetNextWindowSize({ 560, 520 }, ImGuiCond_FirstUseEver);
    if (imgui.Begin('vanagear: your commands and sets', mineOpen)) then
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
            if (imgui.SmallButton('Run##run_' .. word)) then
                mine.Run(word);
            end
            imgui.SameLine();
            if (imgui.SmallButton('Edit##edit_' .. word)) then
                newWord[1] = word;
                newLines[1] = table.concat(entry.lines, '; ');
                everyJob[1] = (entry.file == 'ALL');
            end
            imgui.SameLine();
            if (imgui.SmallButton('Delete##del_' .. word)) then
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
        if (imgui.Button('Save')) then
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
        if (imgui.Button('Open folder')) then
            mine.OpenFolder();
        end
        imgui.SameLine();
        if (imgui.Button('Reload')) then
            mine.Reload();
            sayNote('Read the files again.');
        end

        imgui.Spacing();
        imgui.Separator();
        imgui.Text('Your sets');
        imgui.TextDisabled('Save keeps what you wear now. Wear keeps it on; Look locks it as how you look.');
        local use = require('vg.use');
        local sets = mine.Sets();
        local names = {};
        for name in pairs(sets) do names[#names + 1] = name; end
        table.sort(names);
        for _, name in ipairs(names) do
            local wearing = (use.Worn() == name);
            if (imgui.SmallButton((wearing and 'Take off' or 'Wear') .. '##wear_' .. name)) then
                if (wearing) then use.Wear(nil); else use.Wear(name, sets[name]); end
            end
            imgui.SameLine();
            if (imgui.SmallButton('Look##look_' .. name)) then
                gFunc.LockStyle(sets[name]);
                sayNote(name .. ' locked as your look.');
            end
            imgui.SameLine();
            if (imgui.SmallButton('Delete##delset_' .. name)) then
                mine.RemoveSet(name);
            end
            imgui.SameLine();
            imgui.Text(name);
            if (imgui.IsItemHovered()) then
                local lines = {};
                for _, slot in ipairs(gData.Constants.EquipSlotNames) do
                    if (sets[name][slot] ~= nil) then lines[#lines + 1] = slot .. ': ' .. tostring(sets[name][slot]); end
                end
                imgui.SetTooltip(table.concat(lines, '\n'));
            end
        end
        imgui.PushItemWidth(200);
        imgui.InputText('##vg_mine_set', newSet, 64);
        imgui.PopItemWidth();
        imgui.SameLine();
        if (imgui.Button('Save what I wear')) then
            local ok, name, count = mine.SaveSet(newSet[1]);
            if (ok) then
                sayNote(string.format('%s saved, %d pieces.', name, count));
                newSet[1] = '';
            else
                sayNote('Name the set first.');
            end
        end
        imgui.SameLine();
        if (imgui.Button('Unlock look')) then
            AshitaCore:GetChatManager():QueueCommand(1, '/lockstyle off');
        end
        if (mineNote ~= nil) then
            imgui.TextDisabled(mineNote);
        end
    end
    imgui.End();
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

window.Render = function()
    if (not window.Open) then
        return;
    end
    isOpen[1] = true;
    imgui.SetNextWindowSize({ 720, 480 }, ImGuiCond_FirstUseEver);
    if (imgui.Begin('vanagear', isOpen)) then
        local who = gVana.Who;
        if (who == nil) then
            imgui.Text('Waiting to build: sets are made once you have been in the zone for a few seconds.');
        else
            imgui.Text(string.format('%s%d/%s%d', who.JobName, who.Level, who.SubName, who.SubLevel));
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
                if (imgui.Selectable(name, window.Selected == name)) then
                    window.Selected = name;
                end
            end
            imgui.EndChild();
            imgui.SameLine();

            imgui.BeginChild('slots', { 0, -60 }, ImGuiChildFlags_Borders);
            local ex = gVana.Explain[window.Selected];
            if (ex ~= nil) then
                imgui.Text(window.Selected .. (ex.weaponskill and ('  (' .. ex.weaponskill .. ')') or ''));
                imgui.Separator();
                for slot = 1, 16 do
                    local slotName = gData.Constants.EquipSlotNames[slot];
                    local ranks = ex.ranks[slot];
                    local set = gVana.Sets[window.Selected];
                    local entry = set and set[slotName];
                    imgui.Text(string.format('%-6s', slotName));
                    imgui.SameLine(80);
                    if (gVana.Held[slot] ~= nil) then
                        imgui.TextDisabled(gVana.Held[slot]);
                    elseif (entry ~= nil) and (ranks ~= nil) and (ranks[1] ~= nil) then
                        imgui.Text(string.format('%s  +%.1f', entry.Name, ranks[1].gain));
                        if (imgui.IsItemHovered()) and (ranks[2] ~= nil) then
                            local lines = {};
                            for i = 2, math.min(4, #ranks) do
                                lines[#lines + 1] = string.format('%s  +%.1f', ranks[i].c.piece.Name, ranks[i].gain);
                            end
                            imgui.SetTooltip('Behind it:\n' .. table.concat(lines, '\n'));
                        end
                    else
                        imgui.TextDisabled('nothing you carry adds to this set here');
                    end
                end
            end
            imgui.EndChild();
        end
        imgui.Separator();
        if (imgui.Button('Your commands and sets')) then
            window.Mine = not window.Mine;
        end
        imgui.SameLine();
        imgui.TextDisabled('/vg help lists the commands for this job.  vanagear is built on LuAshitacast by ThornyFFXI (MIT).');
    end
    imgui.End();
    if (not isOpen[1]) then
        window.Open = false;
    end
    if (window.Mine) and (window.Open) then
        renderMine();
    end
end

return window;
