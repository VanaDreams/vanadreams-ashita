r"""Off-game regression test for VanaFish's rod/bait recovery.

When fishing starts with both slots empty but a fishing rod and bait are in a wardrobe, VanaFish
must equip both by the normal 0x050 equipment packet before it attempts a cast.

    python addons/vanafish/tests/auto_swap.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
addon = {}
function T(t)
    return setmetatable(t or {}, { __index = {
        append = function(self, value) self[#self + 1] = value end,
    } })
end
string.args = function(s)
    local out = {}
    for word in string.gmatch(s or '', '%S+') do out[#out + 1] = word end
    return out
end
package.preload['common'] = function() return true end
package.preload['imgui'] = function() return {} end
package.preload['vdstyle'] = function() return {} end
package.preload['settings'] = function()
    return {
        load = function(defaults) defaults.window_open[1] = false; return defaults end,
        save = function() end,
    }
end

local events, packets = {}, {}
ashita = {
    events = { register = function(kind, name, fn) events[kind] = fn end },
    fs = { create_directory = function() end },
}
local inventory = {
    GetContainerCountMax = function(_, bag) if bag == 8 then return 2 elseif bag == 0 then return 80 else return 0 end end,
    GetContainerCount = function() return 0 end,
    GetContainerItem = function(_, bag, slot)
        if bag == 8 and slot == 1 then return { Id = 1001, Count = 1 } end
        if bag == 8 and slot == 2 then return { Id = 1002, Count = 12 } end
        return { Id = 0, Count = 0 }
    end,
    GetEquippedItem = function() return { Index = 0 } end,
}
AshitaCore = {
    GetMemoryManager = function() return {
        GetInventory = function() return inventory end,
        GetPlayer = function() return { GetLoginStatus = function() return 2 end, GetIsZoning = function() return 0 end,
            GetCraftSkill = function() return { GetSkill = function() return 0 end } end } end,
        GetParty = function() return { GetMemberServerId = function() return 1 end, GetMemberTargetIndex = function() return 1 end } end,
        GetEntity = function() return { GetLocalPositionX = function() return 0 end, GetLocalPositionY = function() return 0 end, GetLocalPositionZ = function() return 0 end } end,
    } end,
    GetResourceManager = function() return { GetItemById = function(_, id)
        if id == 1001 then return { Name = { 'Carbon Fishing Rod' }, Skill = 48, Slots = 4 } end
        if id == 1002 then return { Name = { 'Little Worm' }, Skill = 48, Slots = 8 } end
    end } end,
    GetConfigurationManager = function() return { GetString = function() return '' end } end,
    GetPacketManager = function() return { AddOutgoingPacket = function(_, id, data) packets[#packets + 1] = { id = id, data = data } end } end,
    GetChatManager = function() return { QueueCommand = function() end } end,
}

dofile(addon_path .. '/vanafish.lua')
events.command({ command = '/vanafish start' })
events.d3d_present()
local out = {}
for _, p in ipairs(packets) do
    out[#out + 1] = string.format('%02X:%d:%d:%d', p.id, p.data[5], p.data[6], p.data[7])
end
return table.concat(out, '|')
""", str(ADDON))

assert result == '50:1:2:8|50:2:3:8', result
print('PASS')
