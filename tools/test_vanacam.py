"""Small host mock for VanaCam commands, panel, and aspect writes (requires lupa)."""
from pathlib import Path
from lupa import LuaRuntime

root = Path(__file__).resolve().parents[1]
lua = LuaRuntime(unpack_returned_tuples=True)
lua.execute(r'''
addon = {}
function T(value) return value end
handlers = {}
slots = {}
memory = {[0x32F0] = 1.0}
local core = {}
function core:install() return true end
function core:uninstall() return true end
function core:groupOn() return true end
function core:original(name) return name == 'min' and 3 or name == 'minBattle' and 7.2 or nil end
function core:setSlot(name, value) slots[name] = value end
function core:setCameraDistance(value) slots.max, slots.min = value, value - 3 end
function core:setBattleDistance(value) slots.maxBattle, slots.minBattle = value, value - 1.4 end
function core:groupStatus() return {} end
package.preload.common = function() return {} end
package.preload.settings = function()
    return {load = function() return {distance = 20} end,
            register = function() end, save = function() end}
end
package.preload.imgui = function()
    return {Begin = function() return true end, ['End'] = function() end,
            Text = function() end, Separator = function() end,
            Button = function() return false end, SameLine = function() end,
            SliderFloat = function() return false end,
            Checkbox = function() return false end,
            IsItemDeactivatedAfterEdit = function() return false end}
end
package.preload.xicamera_core = function() return {new = function() return core end} end
ashita = {
    events = {register = function(name, _, callback) handlers[name] = callback end},
    memory = {
        find = function() return 0x1000 end,
        read_uint32 = function(address)
            if address == 0x1000 then return 0x2000 end
            if address == 0x2000 then return 0x3000 end
            return 0
        end,
        read_float = function(address) return memory[address] or 0 end,
        write_float = function(address, value) memory[address] = value end,
    },
}
AshitaCore = {GetConfigurationManager = function()
    return {GetUInt32 = function(_, _, _, key) return key == '0001' and 2560 or 1080 end}
end}
function command(...)
    local parts = {...}
    local e = {command = {args = function() return parts end}}
    handlers.command(e)
    assert(e.blocked)
end
''')
lua.execute((root / "addons/vanacam/vanacam.lua").read_text(encoding="utf-8"))
lua.execute("handlers.load(); handlers.d3d_present()")
assert lua.eval("slots.max") == 20
assert lua.eval("slots.min") == 3
assert lua.eval("slots.maxBattle") == 20
assert lua.eval("slots.minBattle") == 7.2
assert abs(lua.eval("memory[0x32F0]") - 0.5625) < 1e-6
lua.execute("command('/vc', 'battle', '12'); assert(slots.maxBattle == 12)")
lua.execute("command('/vc', 'link'); assert(slots.maxBattle == 20)")
lua.execute("command('/vc', '4'); assert(slots.max == 4 and slots.min == 3 and slots.maxBattle == 4 and slots.minBattle == 3)")
lua.execute("command('/vc', 'ui'); handlers.d3d_present()")
lua.execute("command('/vc', 'aspect', 'off'); assert(memory[0x32F0] == 1.0)")
lua.execute("command('/vc', 'aspect', 'on'); handlers.d3d_present()")
lua.execute("handlers.unload(); assert(memory[0x32F0] == 1.0)")
print("VanaCam host mock passed: distance, combat, panel, aspect, unload")
