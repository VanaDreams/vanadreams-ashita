r"""Off-game test of chain lines in a command word (M402, 22 Oct 2026):
- "then <command>" goes once the action before it has started and finished, plus the link gap;
- "burst <command>" goes the moment it finishes, no gap;
- a step that never starts stops the command; a target that dies stops it.

    python addons/vanagear/tests/chain_lines.py
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
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
ashita = { fs = { exists = function() return true end, create_directory = function() end } }
local said = {}
print = function(s) said[#said + 1] = s end

-- a clock the test moves by hand
local clock = 0
os.clock = function() return clock end

-- what the game is doing: nil, or { Name = ... }; one for the player, one for the pet
local playerAction, petAction = nil, nil
local target = { Id = 7, HPP = 100 }
gData = {
    Constants = { EquipSlotNames = {} },
    GetAction = function() return playerAction end,
    GetPetAction = function() return petAction end,
    GetTarget = function() return target end,
}
local sent = {}
AshitaCore = {
    GetInstallPath = function() return tmp .. '/' end,
    GetChatManager = function() return { QueueCommand = function(_, _, line) sent[#sent + 1] = string.format('%.1f %s', clock, line) end } end,
}
gVana = { Who = { JobName = 'BST' } }
-- commands are written into the job's profile file (vg\profile.lua): one from the template to write into
gState = { PlayerName = 'Me', PlayerId = 1 }
gSettings = { AddSetEquipScreenOrder = false }
function T(t) return setmetatable(t or {}, { __index = { append = function(self, v) self[#self + 1] = v end } }) end
ashita = { fs = {
    exists = function(p) local f = io.open(p, 'r'); if f then f:close(); return true end return false end,
    create_directory = function(p) os.execute('mkdir "' .. p:gsub('/', '\\') .. '" 2>nul'); return true end,
} }
gFileTools = require('filetools')
require('vg.profile').Ensure('BST', nil)
local mine = require('vg.mine')
mine.LinkGap = 2
mine.StepTimeout = 3

-- the words under test, written as the window would save them
package.loaded['vg.mine'] = mine
mine.Add('chain', { '/pet "Brain Crush" <t>', 'then /ws "Cloudsplitter" <t>', 'burst /pet "Fireball" <t>' }, false)
mine.Add('stuck', { '/ws "Savage Blade" <t>', 'then /ws "Savage Blade" <t>' }, false)
mine.Add('plain', { 'then /ja "Provoke" <t>' }, false)

local function tick(dt) clock = clock + (dt or 0); mine.Tick() end

-- 1. the chain: step 1 sent at once
mine.Run('chain')
tick(0)
assert(#sent == 1 and sent[1]:find('Brain Crush'), 'step 1 should go at once: ' .. tostring(sent[1]))
-- nothing more until the pet move is seen to start, then finish
tick(1); assert(#sent == 1, 'then must wait for the start')
petAction = { Name = 'Brain Crush' }; tick(0.5); assert(#sent == 1, 'then must wait while it runs')
petAction = nil; tick(0.5)      -- finished at 2.0: the gap starts
tick(1); assert(#sent == 1, 'then must wait out the link gap')
tick(1.1)                       -- 4.1 > 2.0 + 2
assert(#sent == 2 and sent[2]:find('Cloudsplitter'), 'step 2 after the gap: ' .. tostring(sent[2]))
-- the burst: as soon as Cloudsplitter is done
playerAction = { Name = 'Cloudsplitter' }; tick(0.5)
playerAction = nil; tick(0.1)   -- finished: no gap
tick(0.1)
assert(#sent == 3 and sent[3]:find('Fireball'), 'burst goes at once: ' .. tostring(sent[3]))
local chainSent = table.concat(sent, ' | ')
tick(1)   -- the tick after the last line is what ends the command

-- 2. a step that never starts stops the command
sent = {}; clock = 100
mine.Run('stuck'); tick(0)
assert(#sent == 1, 'first line sent')
tick(1); tick(1); tick(1.5)     -- 3.5 s with no action seen
assert(#sent == 1, 'nothing more after a step that never started')
local stuckSaid = said[#said]

-- 3. 'then' with nothing before it just runs
sent = {}; clock = 200
mine.Run('plain'); tick(0)
assert(#sent == 1 and sent[1]:find('Provoke'), 'a bare then line runs')
tick(1)

-- 4. the target dying stops a chain mid-way
sent = {}; clock = 300
mine.Run('chain'); tick(0)
petAction = { Name = 'Brain Crush' }; tick(0.5)
target = { Id = 7, HPP = 0 }; tick(0.5)
petAction = nil; tick(3)
assert(#sent == 1, 'nothing after the target died')
local deadSaid = said[#said]

return chainSent .. '\n' .. stuckSaid .. '\n' .. deadSaid
""", str(ADDON), TMP)
print(result)
lines = result.split('\n')
assert lines[0] == '0.0 /pet "Brain Crush" <t> | 4.1 /ws "Cloudsplitter" <t> | 4.8 /pet "Fireball" <t>', lines[0]
assert 'never started' in lines[1], lines[1]
assert 'target lost' in lines[2], lines[2]
print('PASS')
