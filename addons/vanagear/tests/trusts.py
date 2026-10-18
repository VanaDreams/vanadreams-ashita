r"""Off-game test of vg\trusts.lua: the trusts in the party get a set built from the bags for
their job and level, your picks lay over it, and the line sent to the server carries the item
ids in the server's slot order (Main 0 ... Back 15).

    python addons/vanagear/tests/trusts.py
"""
import tempfile
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent
TMP = tempfile.mkdtemp()

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path, tmp = ...
package.path = addon_path .. '/?.lua;' .. package.path
bit = require('bit')
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
string.trimend = function(s, c) return (s:gsub(c .. '+$', '')) end
ashita = { fs = { exists = function() return true end, create_directory = function() end } }

local mods = dofile(addon_path .. '/data/mods.lua')
local ids = {}
for id, name in pairs(mods) do ids[name] = id end
package.loaded['vg.data'] = {
    ModId = function(name) return ids[name] end,
    SlotBit = (function() local t = {} for i = 1, 16 do t[i] = bit.lshift(1, i - 1) end return t end)(),
    Items = function() return {} end,
}
package.preload['encoding'] = function() return { ShiftJIS_To_UTF8 = function(_, s) return s end } end

local sent = {}
local jobs = { [1] = 'WAR', [3] = 'WHM' }
-- party: you (0), Excenmille the WAR trust (1), a player (2), Kupipi the WHM trust (3)
local members = {
    [0] = { id = 1000, name = 'Me', job = 1, lv = 99 },
    [1] = { id = 0x0100A001, name = 'Excenmille', job = 1, lv = 99, sub = 0 },
    [2] = { id = 1234, name = 'Friend', job = 3, lv = 99 },
    [3] = { id = 0x0100A002, name = 'Kupipi', job = 3, lv = 99, sub = 0 },
}
AshitaCore = {
    GetInstallPath = function() return tmp .. '/' end,
    GetMemoryManager = function() return {
        GetParty = function() return {
            GetMemberIsActive = function(_, i) return members[i] and 1 or 0 end,
            GetMemberServerId = function(_, i) return members[i] and members[i].id or 0 end,
            GetMemberTargetIndex = function(_, i) return 0x700 + i end,
            GetMemberName = function(_, i) return members[i].name end,
            GetMemberMainJob = function(_, i) return members[i].job end,
            GetMemberMainJobLevel = function(_, i) return members[i].lv end,
            GetMemberSubJob = function(_, i) return members[i].sub or 0 end,
        } end,
    } end,
    GetResourceManager = function() return { GetString = function(_, _, id) return jobs[id] or '' end } end,
    GetChatManager = function() return { QueueCommand = function(_, _, line) sent[#sent + 1] = line end } end,
}
gData = { Constants = { EquipSlotNames = { 'Main', 'Sub', 'Range', 'Ammo', 'Head', 'Body', 'Hands', 'Legs', 'Feet',
                                           'Neck', 'Waist', 'Ear1', 'Ear2', 'Ring1', 'Ring2', 'Back' } } }

local WAR, WHM = bit.lshift(1, 0), bit.lshift(1, 2)
local function piece(id, name, slot, jobMask, level, stats)
    local m = {}
    for k, v in pairs(stats) do m[ids[k]] = v end
    local mask = bit.lshift(1, slot)
    if slot == 13 then mask = mask + bit.lshift(1, 14) end -- rings go on either hand
    return { Id = id, Name = name, Equippable = true, Server = { slot = mask, jobs = jobMask, level = level, mods = m } }
end
gVana = {
    Who = { Job = 1 }, BuiltAt = 1, Prefs = {},
    Pieces = {
        piece(101, 'Fighter Mask', 4, WAR, 99, { ACC = 20, STORETP = 5 }),
        piece(102, 'Healer Cap', 4, WHM, 99, { CURE_POTENCY = 10, MND = 10 }),
        piece(103, 'Shared Ring', 13, WAR + WHM, 50, { ACC = 5, CURE_POTENCY = 5 }),
        piece(104, 'Shared Ring', 13, WAR + WHM, 50, { ACC = 5, CURE_POTENCY = 5 }),
        piece(105, 'High Belt', 10, WAR, 120, { ACC = 50 }),
    },
}

local trusts = require('vg.trusts')
local out = {}
local called = trusts.Called()
out[#out + 1] = 'called ' .. #called .. ' ' .. called[1].Name .. ' ' .. called[2].Name
out[#out + 1] = trusts.Line(called[1])
out[#out + 1] = trusts.Line(called[2])
trusts.SetSlot(called[1], 'Head', 'remove')
trusts.SetSlot(called[2], 'Body', 'Fighter Mask')
out[#out + 1] = trusts.Line(called[1])
out[#out + 1] = trusts.Line(called[2])
-- the picks are on disk and come back after a reload
package.loaded['vg.trusts'] = nil
trusts = require('vg.trusts')
out[#out + 1] = 'reloaded ' .. tostring(trusts.Own(called[1]).Head)
return table.concat(out, '\n')
""", str(ADDON), TMP)
print(result)
lines = result.split('\n')
#              main sub rng ammo head body hands legs feet neck waist ear1 ear2 ring1 ring2 back
expect = [
    'called 2 Excenmille Kupipi',
    '/say !trustgear 16818177 0 0 0 0 101 0 0 0 0 0 0 0 0 103 103 0',
    '/say !trustgear 16818178 0 0 0 0 102 0 0 0 0 0 0 0 0 103 103 0',
    '/say !trustgear 16818177 0 0 0 0 0 0 0 0 0 0 0 0 0 103 103 0',
    '/say !trustgear 16818178 0 0 0 0 102 101 0 0 0 0 0 0 0 103 103 0',
    'reloaded remove',
]
assert lines == expect, '\n'.join(f'{a!r} vs {b!r}' for a, b in zip(lines, expect) if a != b)
print('PASS')
