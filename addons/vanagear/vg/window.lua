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
local newWord = { '' };
local newLines = { '' };
local everyJob = { false };
local mineNote = nil;
local newSet = { '' };
local setNote = nil;

local function sayNote(text) mineNote = text; end
local renderSlots;

-- The "Your commands" tab: one word that runs several game commands in order. Laid out as
-- numbered steps with examples to fill the form from (Ferrin, 18 Oct 2026: "the write your own
-- lines is confusing", "a tab of the whole screen with instructions and examples").
local examples = {
    { word = 'pull', lines = '/ja "Provoke" <t>', every = false,
      what = 'Provoke whatever you have targeted.' },
    { word = 'buffs', lines = '/ma "Haste" <me>\nwait 4\n/ma "Refresh" <me>', every = false,
      what = 'Cast Haste on yourself, wait 4 seconds, then Refresh.' },
    { word = 'warpout', lines = '/vg warp', every = true,
      what = 'Put on your Warp Ring, use it and give the slot back (any job).' },
    { word = 'lizard', lines = '/vg with lizard /ja "Bestial Loyalty" <me>', every = false,
      what = 'BST: wear your set called lizard (the jug) and use Bestial Loyalty.' },
};

local function renderMine()
    local mine = require('vg.mine');
    vd.Eyebrow('Your commands: one word that runs several game commands, in order');
    imgui.TextWrapped('Make your own short command. You pick a word, then write the game commands it runs, one per line. '
        .. 'Afterwards typing /vg <word> (or just /<word>) runs them for you.');
    imgui.Separator();

    imgui.Text('1. The word you will type');
    imgui.SameLine(260);
    imgui.PushItemWidth(160);
    imgui.InputText('##vg_mine_word', newWord, 32);
    imgui.PopItemWidth();
    imgui.SameLine();
    imgui.TextDisabled(newWord[1] ~= '' and ('you will type /vg ' .. newWord[1]) or 'for example: pull');

    imgui.Text('2. The commands it runs, one per line');
    imgui.InputTextMultiline('##vg_mine_lines', newLines, 1024, { -1, 90 });
    imgui.TextDisabled('Any game command works: /ja "Provoke" <t>, /ma "Cure IV" <stpc>, /vg wear Idle. '
        .. 'A line "wait 3" waits 3 seconds before the next line. <t> is your target, <me> is you.');

    imgui.Text('3. Which jobs can use it');
    imgui.SameLine(260);
    imgui.Checkbox('every job (leave it off for ' .. ((gVana.Who and gVana.Who.JobName) or 'this job') .. ' only)##vg_mine_all', everyJob);

    if (vd.Button('4. Save it', nil, true)) then
        local ok, word, file = mine.Add(newWord[1], mine.Split(newLines[1]), everyJob[1]);
        if (ok) then
            sayNote(string.format('Saved. Type /vg %s to run it (kept in %s.lua).', word, file));
            newWord[1] = '';
            newLines[1] = '';
        else
            sayNote('Give it a word (step 1) and at least one command (step 2).');
        end
    end
    if (mineNote ~= nil) then
        imgui.SameLine();
        imgui.TextDisabled(mineNote);
    end

    imgui.Separator();
    vd.Eyebrow('Examples');
    imgui.TextDisabled('Press Use to put an example in the boxes above, change it if you like, then Save.');
    for i, ex in ipairs(examples) do
        if (vd.SmallButton('Use##vg_ex_' .. i)) then
            newWord[1] = ex.word;
            newLines[1] = ex.lines;
            everyJob[1] = ex.every;
        end
        imgui.SameLine();
        imgui.Text('/vg ' .. ex.word);
        imgui.SameLine(170);
        imgui.TextDisabled(ex.what);
    end

    imgui.Separator();
    vd.Eyebrow('Your saved commands');
    local list = mine.List();
    local words = {};
    for word in pairs(list) do words[#words + 1] = word; end
    table.sort(words);
    if (#words == 0) then
        imgui.TextDisabled('None yet.');
    end
    for _, word in ipairs(words) do
        local entry = list[word];
        if (vd.SmallButton('Run##run_' .. word)) then
            mine.Run(word);
        end
        imgui.SameLine();
        if (vd.SmallButton('Change##edit_' .. word)) then
            newWord[1] = word;
            newLines[1] = table.concat(entry.lines, '\n');
            everyJob[1] = (entry.file == 'ALL');
        end
        imgui.SameLine();
        if (vd.SmallButton('Delete##del_' .. word)) then
            mine.Remove(word);
        end
        imgui.SameLine();
        imgui.Text('/vg ' .. word);
        imgui.SameLine();
        imgui.TextDisabled(table.concat(entry.lines, '  then  ') .. ((entry.file == 'ALL') and '   (every job)' or ''));
    end
    imgui.Separator();
    if (vd.Button('Open the folder')) then
        mine.OpenFolder();
    end
    imgui.SameLine();
    if (vd.Button('Read the files again')) then
        mine.Reload();
        sayNote('Read the files again.');
    end
    imgui.SameLine();
    imgui.TextDisabled('The files are config\\addons\\vanagear\\commands\\<JOB>.lua, if you would rather write them by hand.');
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
    vd.Eyebrow(require('vg.mine').Label(name) .. ((ex and ex.weaponskill) and ('  (' .. ex.weaponskill .. ')') or ''));
    imgui.SameLine();
    imgui.TextDisabled(yours and (gVana.Built[name] and '  built, with your changes' or '  your own set') or '  built');
    imgui.Separator();
    for slot = 1, 16 do
        local slotName = gData.Constants.EquipSlotNames[slot];
        local entry = pieceName(set[slotName]);
        local ranks = ex and ex.ranks[slot];
        imgui.Text(string.format('%-6s', slotName));
        imgui.SameLine(80);
        if (slot <= 3) and (not gVana.Prefs.Weapons) then
            -- one weapon per slot for the job, worn in every set; nothing else ever changes it
            local core = require('vg.core');
            local picked = ((gVana.Prefs.WeaponPick or {})[gVana.Who and gVana.Who.JobName or ''] or {})[slotName];
            imgui.PushItemWidth(260);
            if (imgui.BeginCombo('##vg_slot_' .. slotName, picked or '(left alone)', ImGuiComboFlags_None)) then
                if (imgui.Selectable('Leave it alone##vg_wnone_' .. slotName, picked == nil)) then
                    core.SetWeaponPick(slotName, nil);
                end
                if (imgui.Selectable('Nothing (take it off)##vg_wrem_' .. slotName, picked == 'remove')) then
                    core.SetWeaponPick(slotName, 'remove');
                end
                for _, piece in ipairs(choices(slot)) do
                    if (imgui.Selectable(piece .. '##vg_wpick_' .. slotName, picked == piece)) then
                        core.SetWeaponPick(slotName, piece);
                    end
                end
                imgui.EndCombo();
            end
            imgui.PopItemWidth();
            imgui.SameLine();
            imgui.TextDisabled('every set');
        elseif (gVana.Held[slot] ~= nil) and (not own[slotName]) then
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


-- A trust's tab: its set from your bags, every slot a list like yours. Nothing is worn: the
-- trust gets the stats of these pieces while you carry them (vg\trusts.lua).
local trustNote = nil;
local function renderTrust(t)
    local trusts = require('vg.trusts');
    local bags = require('vg.bags');
    local who = trusts.Who(t);
    local mine = trusts.Own(t);
    local set = trusts.Built(t);
    vd.Eyebrow(string.format('%s  %s%d/%s', t.Name, t.JobName, t.Level or 0, t.SubName));
    imgui.SameLine();
    imgui.TextDisabled('  stats only: your gear stays in your bags');
    imgui.Separator();
    for slot = 1, 16 do
        local slotName = gData.Constants.EquipSlotNames[slot];
        local pick = mine[slotName];
        local builtPiece = set[slot] and set[slot].Name or nil;
        local shown = (pick == 'remove') and nil or (pick or builtPiece);
        imgui.Text(string.format('%-6s', slotName));
        imgui.SameLine(80);
        imgui.PushItemWidth(260);
        local preview = (shown or '(nothing)') .. ((pick ~= nil) and '  *' or '');
        if (imgui.BeginCombo('##vg_t_' .. t.Name .. slotName, preview, ImGuiComboFlags_None)) then
            if (imgui.Selectable('Built: ' .. (builtPiece or 'nothing') .. '##vg_tb_' .. slotName, pick == nil)) then
                trusts.SetSlot(t, slotName, nil);
            end
            if (imgui.Selectable('Nothing##vg_tn_' .. slotName, pick == 'remove')) then
                trusts.SetSlot(t, slotName, 'remove');
            end
            local seen = {};
            local list = {};
            for _, p in ipairs(gVana.Pieces or {}) do
                if (not seen[p.Name]) and (bags.CanWear(p, slot, who, true)) then
                    seen[p.Name] = true;
                    list[#list + 1] = p.Name;
                end
            end
            table.sort(list);
            for _, piece in ipairs(list) do
                if (imgui.Selectable(piece .. '##vg_tp_' .. slotName, pick == piece)) then
                    trusts.SetSlot(t, slotName, piece);
                end
            end
            imgui.EndCombo();
        end
        imgui.PopItemWidth();
    end
    imgui.Separator();
    if (vd.Button('Send to ' .. t.Name, nil, true)) then
        trusts.Send(t);
        trustNote = 'Sent. The server answers in chat with what went on.';
    end
    if (next(mine) ~= nil) then
        imgui.SameLine();
        if (vd.Button('Undo my changes')) then
            trusts.Reset(t);
            trustNote = t.Name .. ' is back to the built set.';
        end
    end
    imgui.TextDisabled('* = your pick. Changes are sent by themselves; the trust keeps them for the next call.');
    if (trustNote ~= nil) then
        imgui.TextDisabled(trustNote);
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
                imgui.SameLine();
                if (vd.SmallButton('Copy its sets into vanagear and stop using it')) then
                    require('vg.commands').Vg({ 'takeover' });
                end
            end
            imgui.Separator();

            -- tabs: your sets, your commands, and one for every trust you have called
            local called = require('vg.trusts').Called();
            local tabs = imgui.BeginTabBar('##vg_tabs', ImGuiTabBarFlags_None);
            if (not tabs) or (imgui.BeginTabItem('Your sets##vg_tab_you')) then
                local names = sortedNames();
                if (window.Selected == nil) or (gVana.Sets[window.Selected] == nil) then
                    window.Selected = gVana.Sets.Idle and 'Idle' or names[1];
                end

                imgui.BeginChild('sets', { 180, -60 }, ImGuiChildFlags_Borders);
                for _, name in ipairs(names) do
                    if (vd.Selectable(require('vg.mine').Label(name) .. '##vg_set_' .. name, window.Selected == name)) then
                        window.Selected = name;
                    end
                end
                imgui.EndChild();
                imgui.SameLine();

                imgui.BeginChild('slots', { 0, -60 }, ImGuiChildFlags_Borders);
                renderSlots(window.Selected);
                imgui.EndChild();
                if (tabs) then imgui.EndTabItem(); end
            end
            -- /vg mine opens straight on this tab
            local mineFlags = window.Mine and ImGuiTabItemFlags_SetSelected or ImGuiTabItemFlags_None;
            window.Mine = false;
            if (tabs) and (imgui.BeginTabItem('Your commands##vg_tab_mine', nil, mineFlags)) then
                imgui.BeginChild('mine', { 0, -60 }, ImGuiChildFlags_Borders);
                renderMine();
                imgui.EndChild();
                imgui.EndTabItem();
            end
            if (tabs) then
                for _, t in ipairs(called) do
                    if (imgui.BeginTabItem(t.Name .. '##vg_tab_' .. t.Id)) then
                        imgui.BeginChild('trust_slots', { 0, -60 }, ImGuiChildFlags_Borders);
                        renderTrust(t);
                        imgui.EndChild();
                        imgui.EndTabItem();
                    end
                end
                imgui.EndTabBar();
            end
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
        imgui.TextDisabled('/vg help lists the commands for this job.  vanagear is built on LuAshitacast by ThornyFFXI (MIT).');
    end
    vd.End();
    if (not isOpen[1]) then
        window.Open = false;
    end
end

return window;
