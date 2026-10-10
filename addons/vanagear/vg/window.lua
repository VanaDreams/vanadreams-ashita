--[[
    vanagear: the /vg window. The sets built for the job you are on, the piece in each slot and
    what it scored, and the commands that mean something for this job.
]]--

local imgui = require('imgui');

local window = { Open = false, Selected = nil };
gVana.Window = window;

local isOpen = { false };

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

            imgui.BeginChild('sets', { 180, -60 }, true);
            for _, name in ipairs(names) do
                if (imgui.Selectable(name, window.Selected == name)) then
                    window.Selected = name;
                end
            end
            imgui.EndChild();
            imgui.SameLine();

            imgui.BeginChild('slots', { 0, -60 }, true);
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
        imgui.TextDisabled('/vg help lists the commands for this job.  vanagear is built on LuAshitacast by ThornyFFXI (MIT).');
    end
    imgui.End();
    if (not isOpen[1]) then
        window.Open = false;
    end
end

return window;
