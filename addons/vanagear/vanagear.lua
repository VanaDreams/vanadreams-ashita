--[[
    vanagear: gear sets built from your own bags, for your job and level.

    Built on LuAshitacast by ThornyFFXI (https://github.com/ThornyFFXI/LuAshitacast, MIT licence,
    Copyright (c) 2021 ThornyFFXI; see LICENSE-LuAshitacast). LuAshitacast 2.14's files are
    carried here and still do the swapping; vanagear's own code is in vg\, the server's gear
    tables in data\ and the job weights in jobs\.
]]--

addon.name      = 'vanagear';
addon.author    = 'Vanadreams, on LuAshitacast by Thorny';
addon.version   = '0.1.4';
addon.desc      = 'Gear sets built from your own bags for your job and level, on LuAshitacast.';
addon.link      = 'https://github.com/VanaDreams/vanadreams-ashita/tree/main/addons/vanagear';

require('common');
chat = require('chat');
local jit = require('jit');
jit.off();
if (ashita.addons_version >= 4.3) then
    addon.instance:enable_feature(addon_feature.use_packet_chunks);
end

gConfigGUI           = require('config');
gData                = require('data');
gFunc                = require('func');
gEquip               = require('equip');
gFileTools           = require('filetools');
gIntegration         = require('integration');
gState               = require('state');
gCommandHandlers     = require('commandhandlers');
gPacketHandlers      = require('packethandlers');
gSetDisplay          = require('setdisplay');
gPreservedGlobalKeys = T{};

-- vanagear's own parts; made before the load event so LuAshitacast keeps them across profiles
gVanaCore            = require('vg.core');
gVanaStock           = require('vg.stock');
gVanaUse             = require('vg.use');
gVanaMine            = require('vg.mine');
gVanaCommands        = require('vg.commands');
gVanaWindow          = require('vg.window');

ashita.events.register('load', 'vanagear_load_cb', function ()
    --Create a list of all globals the ashita environment has created.
    --This will be used to clear all leftover globals when loading a new profile.
    for key,_ in pairs(_G) do
        gPreservedGlobalKeys[key] = true;
    end
    gVanaCore.LoadPrefs();
    gState.Init();
end);

ashita.events.register('unload', 'vanagear_unload_cb', function ()
    gState.UnloadProfile();
end);

ashita.events.register('d3d_present', 'vanagear_render', function()
    gVanaCore.Tick();
    gVanaMine.Tick();
    gConfigGUI:Render();
    gSetDisplay:Render();
    gVanaWindow.Render();
end);

ashita.events.register('packet_in', 'vanagear_packet_in_cb', function (e)
    gPacketHandlers.HandleIncomingPacket(e);
end);

ashita.events.register('packet_out', 'vanagear_packet_out_cb', function (e)
    gPacketHandlers.HandleOutgoingPacket(e);
end);

ashita.events.register('command', 'vanagear_command_cb', function (e)
    if (gVanaCommands.Handle(e)) then
        return;
    end
    gCommandHandlers.HandleCommand(e);
end);
