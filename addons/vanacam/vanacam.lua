-- VanaCam: one camera distance for exploration and engaged combat.
-- Camera patching is provided by XICamera's BSD-licensed core; see LICENSE.xicamera.txt.
addon.name = 'vanacam';
addon.author = 'Vanadreams';
addon.version = '0.2.0';
addon.desc = 'Camera presets, combat distance, tight-space zoom, and screen aspect correction.';

require('common');
local settings = require('settings');
local imgui = require('imgui');
local Core = require('xicamera_core');

local presets = { 8, 15, 20 };
local defaults = T{ distance = 15, battleDistance = 0, autoAspect = true };
local config = settings.load(defaults);
local ready = false;
local failure = nil;
local window = T{ false };
local normal_slider = T{ 15 };
local battle_slider = T{ 15 };
local aspect_toggle = T{ true };
local aspect_pointer = 0;
local aspect_address = nil;
local aspect_original = nil;
local aspect_written = nil;
local screen_width, screen_height = 0, 0;

local function normalize_settings()
    local normal = tonumber(config.distance);
    local battle = tonumber(config.battleDistance);
    config.distance = normal and normal >= 4 and normal <= 40 and normal or 15;
    config.battleDistance = battle and battle >= 4 and battle <= 40 and battle or 0;
    if config.autoAspect == nil then config.autoAspect = true; end
    normal_slider[1] = config.distance;
    battle_slider[1] = config.battleDistance > 0 and config.battleDistance or config.distance;
    aspect_toggle[1] = config.autoAspect;
end

normalize_settings();

local function say(message)
    print(('[vanacam] %s'):format(message));
end

local mem = {
    find = function(sig) return ashita.memory.find('FFXiMain.dll', 0, sig, 0, 0); end,
    read_u16 = function(addr) return ashita.memory.read_uint16(addr); end,
    read_u32 = function(addr) return ashita.memory.read_uint32(addr); end,
    read_float = function(addr) return ashita.memory.read_float(addr); end,
    write_u16 = function(addr, value) ashita.memory.write_uint16(addr, value); end,
    write_u32 = function(addr, value) ashita.memory.write_uint32(addr, value); end,
    write_float = function(addr, value) ashita.memory.write_float(addr, value); end,
    alloc = function(size) return ashita.memory.alloc(size); end,
    log = say,
};
local core = Core.new(mem);

local function camera_groups_ready()
    return core:groupOn('min') and core:groupOn('max')
        and core:groupOn('minBattle') and core:groupOn('maxBattle');
end

local function apply_distance(value)
    if not ready then return false; end
    local battle_distance = config.battleDistance > 0 and config.battleDistance or value;
    core:setCameraDistance(value);
    core:setBattleDistance(battle_distance);
    -- XICamera slides the minimum up with the maximum (20 becomes roughly 17).
    -- That leaves the camera stuck outside narrow passages. Keep the client's
    -- original zoom-in limits so wall avoidance and manual zoom can pull it in.
    local normal_min = core:original('min');
    local battle_min = core:original('minBattle');
    if normal_min then core:setSlot('min', math.min(normal_min, value - 1)); end
    if battle_min then core:setSlot('minBattle', math.min(battle_min, battle_distance - 1)); end
    return true;
end

local function restore_aspect()
    local root = aspect_pointer and aspect_pointer ~= 0 and ashita.memory.read_uint32(aspect_pointer) or 0;
    local data = root ~= 0 and ashita.memory.read_uint32(root) or 0;
    if aspect_address and data ~= 0 and data + 0x2F0 == aspect_address and aspect_original and aspect_written then
        local current = ashita.memory.read_float(aspect_address);
        if math.abs(current - aspect_written) < 0.0001 then
            ashita.memory.write_float(aspect_address, aspect_original);
        end
    end
    aspect_address, aspect_original, aspect_written = nil, nil, nil;
end

local function apply_aspect()
    if not config.autoAspect or not aspect_pointer or aspect_pointer == 0 then return; end
    local manager = AshitaCore:GetConfigurationManager();
    local width = manager:GetUInt32('boot', 'ffxi.registry', '0001', 0);
    local height = manager:GetUInt32('boot', 'ffxi.registry', '0002', 0);
    if width < 640 or width > 16384 or height < 480 or height > 16384 then return; end
    screen_width, screen_height = width, height;
    local root = ashita.memory.read_uint32(aspect_pointer);
    if root == 0 then return; end
    local data = ashita.memory.read_uint32(root);
    if data == 0 then return; end
    local address = data + 0x2F0;
    if aspect_address ~= address then
        aspect_address = address;
        aspect_original = ashita.memory.read_float(address);
        aspect_written = nil;
    end
    -- FFXI's stock aspect scalar is normalized around a 4:3 window.
    local wanted = (4 * height) / (3 * width);
    if math.abs(ashita.memory.read_float(address) - wanted) > 0.0001 then
        ashita.memory.write_float(address, wanted);
    end
    aspect_written = wanted;
end

local function valid_distance(value)
    local number = tonumber(value);
    -- The core keeps the original zoom-wheel range by setting min = max - 3.
    -- Below 4 this can produce a zero or negative camera minimum.
    if number == nil or number ~= number or number < 4 or number > 40 then return nil; end
    return number;
end

local function set_distance(value, quiet)
    local number = valid_distance(value);
    if not number then say('Use a camera distance from 4 to 40.'); return; end
    if not ready then say('Camera patch is unavailable. Use /vc status for details.'); return; end
    config.distance = number;
    apply_distance(number);
    normal_slider[1] = number;
    if config.battleDistance == 0 then battle_slider[1] = number; end
    if not quiet then
        settings.save();
        say(('Normal camera distance: %g%s'):format(number, config.battleDistance == 0 and ' (combat follows)' or ''));
    end
end

local function set_battle_distance(value, quiet)
    local number = valid_distance(value);
    if not number then say('Use a combat distance from 4 to 40.'); return; end
    if not ready then say('Camera patch is unavailable. Use /vc status for details.'); return; end
    config.battleDistance = number;
    battle_slider[1] = number;
    apply_distance(config.distance);
    if not quiet then
        settings.save();
        say(('Combat camera distance: %g'):format(number));
    end
end

local function link_battle_distance()
    config.battleDistance = 0;
    battle_slider[1] = config.distance;
    apply_distance(config.distance);
    settings.save();
    say('Combat camera now follows normal distance.');
end

local function set_aspect(enabled)
    config.autoAspect = enabled;
    aspect_toggle[1] = enabled;
    if enabled then apply_aspect(); else restore_aspect(); end
    settings.save();
    say(enabled and 'Screen aspect correction on.' or 'Screen aspect correction off.');
end

local function show_status()
    say(('Normal: %g; combat: %g; camera patch: %s'):format(config.distance,
        config.battleDistance > 0 and config.battleDistance or config.distance,
        ready and 'active' or 'unavailable'));
    say(('Screen aspect: %s%s'):format(config.autoAspect and 'on' or 'off',
        screen_width > 0 and (' (%d x %d)'):format(screen_width, screen_height) or ' (resolution unavailable)'));
    if ready then return; end
    if failure then say(failure); end
    for _, group in ipairs(core:groupStatus()) do
        if (group.name == 'min' or group.name == 'max'
            or group.name == 'minBattle' or group.name == 'maxBattle') and not group.enabled then
            say(('%s: %s'):format(group.name, group.why or 'not installed'));
        end
    end
end

settings.register('settings', 'vanacam_settings', function(new_settings)
    if new_settings ~= nil then config = new_settings; end
    normalize_settings();
    apply_distance(config.distance);
    if config.autoAspect then apply_aspect(); else restore_aspect(); end
end);

ashita.events.register('load', 'vanacam_load', function()
    aspect_pointer = ashita.memory.find(0, 0, 'A1????????85C074??D9442404D80D????????D80D', 1, 0);
    if aspect_pointer == 0 then say('Screen aspect correction unavailable on this client.'); end
    local ok, result = pcall(function() return core:install(); end);
    if not ok then
        pcall(function() core:uninstall(); end);
        failure = 'Camera patch could not start: ' .. tostring(result);
        say(failure);
        return;
    end
    ready = camera_groups_ready();
    if not ready then
        -- Do not leave only the normal or only the battle camera patched.
        failure = 'Required camera signatures were not found or were owned by another addon.';
        core:uninstall();
        say('Camera patch is incomplete; no camera distance was changed. Use /vc status.');
        return;
    end
    failure = nil;
    normalize_settings();
    apply_distance(config.distance);
    say(('Ready at %g. /vc ui opens camera controls; /vc cycles presets.'):format(config.distance));
end);

ashita.events.register('d3d_present', 'vanacam_present', function()
    if config.autoAspect and aspect_pointer ~= 0 then apply_aspect(); end
    if not window[1] then return; end
    if imgui.Begin('VanaCam', window) then
        imgui.Text(ready and 'Camera patch active' or 'Camera patch unavailable');
        if screen_width > 0 then
            imgui.Text(('Screen: %d x %d (%.2f:1)'):format(screen_width, screen_height, screen_width / screen_height));
        end
        imgui.Separator();
        imgui.Text('Quick distances');
        if imgui.Button('Close 8') then set_distance(8); end
        imgui.SameLine();
        if imgui.Button('Adventure 15') then set_distance(15); end
        imgui.SameLine();
        if imgui.Button('Far 20') then set_distance(20); end
        if imgui.SliderFloat('Normal distance', normal_slider, 4, 40, '%.1f') then set_distance(normal_slider[1], true); end
        if imgui.IsItemDeactivatedAfterEdit() then settings.save(); end
        local linked = T{ config.battleDistance == 0 };
        if imgui.Checkbox('Use same distance in combat', linked) then
            if linked[1] then link_battle_distance(); else set_battle_distance(config.distance); end
        end
        if config.battleDistance > 0 then
            if imgui.SliderFloat('Combat distance', battle_slider, 4, 40, '%.1f') then set_battle_distance(battle_slider[1], true); end
            if imgui.IsItemDeactivatedAfterEdit() then settings.save(); end
        end
        imgui.Text('Camera can zoom close to the original game limit near walls.');
        if imgui.Checkbox('Match view to screen shape', aspect_toggle) then
            set_aspect(aspect_toggle[1]);
        end
        imgui.Text('Use the mouse wheel to zoom within the range.');
    end
    imgui.End();
end);

ashita.events.register('command', 'vanacam_command', function(e)
    local args = e.command:args();
    if #args == 0 or (args[1] ~= '/vc' and args[1] ~= '/vanacam') then return; end
    e.blocked = true;
    local action = args[2] and args[2]:lower() or 'cycle';

    if action == 'help' then
        say('/vc cycle - 8, 15, 20, then 8 again');
        say('/vc ui - open camera controls');
        say('/vc 20 or /vc set 20 - set normal camera distance (4-40)');
        say('/vc battle 20 or /vc link - separate or match combat distance');
        say('/vc aspect on|off - match the camera view to your screen shape');
        say('/vc status - show the current setting and patch status');
    elseif action == 'status' and #args == 2 then
        show_status();
    elseif action == 'ui' and #args == 2 then
        window[1] = not window[1];
    elseif action == 'cycle' and #args <= 2 then
        if not ready then say('Camera patch is unavailable. Use /vc status.'); return; end
        for _, preset in ipairs(presets) do
            if preset > config.distance then
                set_distance(preset);
                return;
            end
        end
        set_distance(presets[1]);
    elseif action == 'set' and #args == 3 then
        set_distance(args[3]);
    elseif action == 'battle' and #args == 3 then
        set_battle_distance(args[3]);
    elseif action == 'link' and #args == 2 then
        link_battle_distance();
    elseif action == 'aspect' and #args == 3 and (args[3] == 'on' or args[3] == 'off') then
        set_aspect(args[3] == 'on');
    elseif #args == 2 and valid_distance(action) then
        set_distance(action);
    else
        say('Unknown command. Use /vc help.');
    end
end);

ashita.events.register('unload', 'vanacam_unload', function()
    ready = false;
    restore_aspect();
    core:uninstall();
    settings.save();
end);
