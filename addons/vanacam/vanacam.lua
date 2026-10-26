-- VanaCam: one camera distance for exploration and engaged combat.
-- Camera patching is provided by XICamera's BSD-licensed core; see LICENSE.xicamera.txt.
addon.name = 'vanacam';
addon.author = 'Vanadreams';
addon.version = '0.1.0';
addon.desc = 'Set or cycle the third-person camera distance, including in combat.';

require('common');
local settings = require('settings');
local Core = require('xicamera_core');

local presets = { 8, 15, 20 };
local defaults = T{ distance = 15 };
local config = settings.load(defaults);
local ready = false;
local failure = nil;

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
    core:setCameraDistance(value);
    core:setBattleDistance(value);
    return true;
end

local function valid_distance(value)
    local number = tonumber(value);
    -- The core keeps the original zoom-wheel range by setting min = max - 3.
    -- Below 4 this can produce a zero or negative camera minimum.
    if number == nil or number ~= number or number < 4 or number > 40 then return nil; end
    return number;
end

local function set_distance(value)
    local number = valid_distance(value);
    if not number then say('Use a camera distance from 4 to 40.'); return; end
    if not ready then say('Camera patch is unavailable. Use /vc status for details.'); return; end
    config.distance = number;
    apply_distance(number);
    settings.save();
    say(('Normal and battle camera distance: %g'):format(number));
end

local function show_status()
    say(('Distance: %g; camera patch: %s'):format(config.distance, ready and 'active' or 'unavailable'));
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
    if valid_distance(config.distance) == nil then config.distance = defaults.distance; end
    apply_distance(config.distance);
end);

ashita.events.register('load', 'vanacam_load', function()
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
    if valid_distance(config.distance) == nil then config.distance = defaults.distance; end
    apply_distance(config.distance);
    say(('Ready at %g for normal and battle camera. /vc help lists controls.'):format(config.distance));
end);

ashita.events.register('command', 'vanacam_command', function(e)
    local args = e.command:args();
    if #args == 0 or (args[1] ~= '/vc' and args[1] ~= '/vanacam') then return; end
    e.blocked = true;
    local action = args[2] and args[2]:lower() or 'cycle';

    if action == 'help' then
        say('/vc cycle - 8, 15, 20, then 8 again');
        say('/vc 20 or /vc set 20 - set normal and battle camera distance (4-40)');
        say('/vc status - show the current setting and patch status');
    elseif action == 'status' and #args == 2 then
        show_status();
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
    elseif #args == 2 and valid_distance(action) then
        set_distance(action);
    else
        say('Unknown command. Use /vc help.');
    end
end);

ashita.events.register('unload', 'vanacam_unload', function()
    ready = false;
    core:uninstall();
    settings.save();
end);
