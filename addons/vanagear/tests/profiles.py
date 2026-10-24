r"""Off-game test of vanagear's own profile files (vg\profile.lua; Lee, 24 Oct 2026: "make vg operate like
lac"; "you cant store the gear in memory, hence why lac uses profile"; "vg should have lac machinery for
itself and never have to touch lac"):
- a LuAshitacast profile is pulled over the first time, code kept, /lac commands now /vg, a built table and
  a commands table added, the sets and commands vanagear kept beside it before written in;
- the built sets go into the built table only; what is worn is your sets over built, read from the file,
  into the same table the profile's handlers hold; Engaged / WS carry the built set they mean;
- a drop-down changes only its own slot (a list of pieces by level in another slot stays), takes a slot out,
  and a set comes out whole; /vg addset (the engine's SaveSet) writes your sets table, not built;
- commands go in the profile's commands table; a new profile from the template loads;
- a trust's file has built and sets, and what is sent is sets over built.

    python addons/vanagear/tests/profiles.py
"""
import shutil
import tempfile
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

STARTER = r"""-- DNC profile for Ferrin. Starter file.
local profile = {};

local sets = {
    Idle = {
    },
    Engaged = {
        Body = 'My Engaged Body',
    },
    WS = {
    },
    Idle_Priority = {
        Head = { 'Glory Crown', 'Old Hat' },
    },
};
profile.Sets = sets;

profile.HandleDefault = function()
    gFunc.EquipSet(sets.Idle);
end

profile.OnLoad = function()
    AshitaCore:GetChatManager():QueueCommand(-1, '/alias /dnc /lac fwd');
end

return profile;
"""

tmp = Path(tempfile.mkdtemp())
try:
    install = str(tmp) + '\\'
    shutil.copytree(ADDON / 'templates', tmp / 'addons' / 'vanagear' / 'templates')
    L = luajit21.LuaRuntime(unpack_returned_tuples=True)
    result = L.execute(r"""
local addon_path, install, starter = ...
package.path = addon_path .. '/?.lua;' .. package.path
function T(t)
    return setmetatable(t or {}, { __index = { append = function(self, v) self[#self + 1] = v end } })
end
chat = setmetatable({}, { __index = function() return function(...) return table.concat({ ... }, ' ') end end })
ashita = { fs = {
    exists = function(p) local f = io.open(p, 'r'); if f then f:close(); return true end return false end,
    create_directory = function(p) os.execute('mkdir "' .. p .. '" 2>nul'); return true end,
} }
AshitaCore = { GetInstallPath = function() return install end }
gSettings = { AddSetEquipScreenOrder = false, AddSetBackups = true }
gData = { Constants = { EquipSlotNames = { 'Main', 'Sub', 'Range', 'Ammo', 'Head', 'Body', 'Hands', 'Legs', 'Feet', 'Neck',
    'Waist', 'Ear1', 'Ear2', 'Ring1', 'Ring2', 'Back' } } }
gFunc = { Error = function(m) print(m) end }
gState = { PlayerName = 'Ferrin', PlayerId = 6971 }
gFileTools = require('filetools')
gVana = { Who = { JobName = 'DNC' }, Sets = {}, Built = {}, Prefs = { Weapons = true, Underlay = {} } }
local underlay = true
package.loaded['vg.core'] = { Underlay = function() return underlay end }

local out = {}
local function check(label, ok) out[#out + 1] = (ok and 'ok   ' or 'FAIL ') .. label end
local function readAll(p) local f = io.open(p); local t = f:read('*all'); f:close(); return t end
local function writeAll(p, t) local f = io.open(p, 'w'); f:write(t); f:close() end

local profile = require('vg.profile')
local vgDir = install .. 'config\\addons\\vanagear'
ashita.fs.create_directory(vgDir)
ashita.fs.create_directory(vgDir .. '\\sets')
ashita.fs.create_directory(vgDir .. '\\commands')
-- what vanagear 0.1.20 kept beside the profile
writeAll(vgDir .. '\\sets\\DNC.lua', "return { ['Idle'] = { Feet = 'Tandava Crackows', Ring1 = 'Warp Ring' }, ['RudrasStorm'] = { Ring2 = \"Epaminondas's Ring\" } };\n")
writeAll(vgDir .. '\\commands\\DNC.lua', "return { ['samba'] = { '/ja \"Haste Samba\" <me>', 'wait 2' } };\n")

-- the pull-over
local lac = install .. 'lac_DNC.lua'
writeAll(lac, starter)
local path, made = profile.Ensure('DNC', lac)
check('profile made at profiles\\Ferrin_6971\\DNC.lua', made and path == vgDir .. '\\profiles\\Ferrin_6971\\DNC.lua')
local text = readAll(path)
check('code kept', text:find('profile.HandleDefault = function()', 1, true) ~= nil)
check('/lac fwd now /vg fwd', text:find('/alias /dnc /vg fwd', 1, true) ~= nil and text:find('/lac ', 1, true) == nil)
check('built table added', text:find('local built = {', 1, true) ~= nil)
local p = profile.Run(path)
check('pulled-over profile loads', p ~= nil and type(p.Built) == 'table' and type(p.Commands) == 'table')
check('old picks in your sets', p.Sets.Idle.Feet == 'Tandava Crackows' and p.Sets.Idle.Ring1 == 'Warp Ring')
check('old picks set of its own in your sets', p.Sets.RudrasStorm and p.Sets.RudrasStorm.Ring2 == "Epaminondas's Ring")
check('profile\'s own sets kept', p.Sets.Engaged.Body == 'My Engaged Body' and p.Sets.Idle_Priority.Head[2] == 'Old Hat')
check('old commands in the profile', p.Commands.samba and p.Commands.samba[2] == 'wait 2')
check('made only once', select(2, profile.Ensure('DNC', lac)) == false)

-- loaded and running: built written, read back merged
gProfile = profile.Run(path)
gProfile.FilePath = path
gProfile.VgJob = 'DNC'
local held = gProfile.Sets
check('built written for the profile\'s job', profile.WriteBuilt({
    Idle = { Head = 'Nyame Helm', Feet = 'Built Boots', Body = 'Built Body' },
    Tp_Default = { Head = 'Built TP Head', Body = 'Built TP Body' },
    Ws_Default = { Ear2 = 'Moonshade Earring' },
}, 'DNC'))
check('no built write for another job', profile.WriteBuilt({ Idle = {} }, 'WAR') == false)
check('refresh', profile.Refresh())
check('Idle: your feet over built', gProfile.Sets.Idle.Feet == 'Tandava Crackows' and gProfile.Sets.Idle.Body == 'Built Body')
check('Engaged: your body over the built Tp_Default', gProfile.Sets.Engaged.Body == 'My Engaged Body' and gProfile.Sets.Engaged.Head == 'Built TP Head')
check('Tp_Default the same set', gProfile.Sets.Tp_Default == gProfile.Sets.Engaged)
check('WS carries Ws_Default', gProfile.Sets.WS.Ear2 == 'Moonshade Earring')
check('handlers hold the same table', gProfile.Sets == held and held.Idle.Feet == 'Tandava Crackows')
check('own slots known', gVana.Own.Idle.Feet == true and gVana.Own.Idle.Body == nil)
check('file names known', profile.FileName('Tp_Default') == 'Engaged')
local built = profile.Run(path).Built
check('built stays apart in the file', built.Idle.Feet == 'Built Boots' and profile.Run(path).Sets.Idle.Body == nil)
local worn = {}
gFunc.EquipSet = function(set) for k, v in pairs(set) do worn[k] = v end end
gProfile.HandleDefault()
check('pulled-over HandleDefault wears the merged Idle', worn.Feet == 'Tandava Crackows' and worn.Head == 'Nyame Helm')

-- underlay off: your sets as written
underlay = false
profile.Refresh()
check('underlay off: no built pieces', gProfile.Sets.Idle.Body == nil and gProfile.Sets.Idle.Feet == 'Tandava Crackows')
underlay = true
profile.Refresh()

-- drop-downs: one slot at a time
check('slot added', profile.SetSlot(path, 'Idle', 'Neck', 'Etoile Gorget +2'))
check('slot changed', profile.SetSlot(path, 'Idle', 'Feet', 'Other Boots'))
check('slot in a priority set, list in another slot kept', profile.SetSlot(path, 'Idle_Priority', 'Body', 'Prio Body'))
p = profile.Run(path)
check('slot edits read back', p.Sets.Idle.Neck == 'Etoile Gorget +2' and p.Sets.Idle.Feet == 'Other Boots' and p.Sets.Idle.Ring1 == 'Warp Ring')
check('priority list untouched', p.Sets.Idle_Priority.Head[1] == 'Glory Crown' and p.Sets.Idle_Priority.Head[2] == 'Old Hat' and p.Sets.Idle_Priority.Body == 'Prio Body')
check('slot taken out', profile.SetSlot(path, 'Idle', 'Ring1', nil) and profile.Run(path).Sets.Idle.Ring1 == nil)
check('remove written as remove', profile.SetSlot(path, 'WS', 'Head', 'remove') and profile.Run(path).Sets.WS.Head == 'remove')
check('new set from a slot', profile.SetSlot(path, 'Movement', 'Feet', 'Tandava Crackows') and profile.Run(path).Sets.Movement.Feet == 'Tandava Crackows')
check('set out whole', profile.SaveSet(path, 'RudrasStorm', false) and profile.Run(path).Sets.RudrasStorm == nil)
check('backup made', io.open(vgDir .. '\\profiles\\Ferrin_6971\\backups') ~= nil or os.execute('dir "' .. vgDir .. '\\profiles\\Ferrin_6971\\backups" >nul 2>nul') == 0)
check('built untouched by your edits', profile.Run(path).Built.Idle.Feet == 'Built Boots')

-- the engine's addset writes your sets table
gFileTools.SaveSet('Fishing', { Range = 'Lu Shang\'s Fishing Rod' })
p = profile.Run(path)
check('addset into your sets, not built', p.Sets.Fishing and p.Sets.Fishing.Range == "Lu Shang's Fishing Rod" and p.Built.Fishing == nil)

-- commands
check('commands written', profile.WriteCommands(path, { reward = { '/ja "Reward" <me>' } }))
p = profile.Run(path)
check('commands read back, sets kept', p.Commands.reward ~= nil and p.Commands.samba == nil and p.Sets.Idle.Neck == 'Etoile Gorget +2')

-- a new profile from the job's template (templates\WAR.lua, copied into this install below)
local warPath = profile.Ensure('WAR', nil)
local war = profile.Run(warPath)
check('template loads with built, sets and commands', war and type(war.Built) == 'table' and type(war.Sets) == 'table' and type(war.Commands) == 'table')
check('new profile is the job template', war and war.VgTemplate == true and readAll(warPath):find('vanagear template: Warrior', 1, true) ~= nil)
gProfile = war; gProfile.FilePath = warPath; gProfile.VgJob = 'WAR'
profile.WriteBuilt({ Idle = { Head = 'Nyame Helm' } }, 'WAR')
profile.SetSlot(warPath, 'Idle', 'Feet', 'Hermes\' Sandals')
check('template takes built and your slots', profile.Run(warPath).Built.Idle.Head == 'Nyame Helm' and profile.Run(warPath).Sets.Idle.Feet == "Hermes' Sandals")

-- trusts
check('trust sets written', profile.WriteTrustSets('Shantotto II', { Idle = { Head = 'remove', Feet = 'Tandava Crackows' } }))
local sent = profile.WriteTrust('Shantotto II', { Idle = { Head = 'Nyame Helm', Body = 'Nyame Mail' }, Cure = { Hands = 'Nyame Gauntlets' } })
check('trust file at profiles\\Ferrin_6971\\trusts\\ShantottoII.lua', io.open(vgDir .. '\\profiles\\Ferrin_6971\\trusts\\ShantottoII.lua') ~= nil)
check('sent: yours over built', sent and sent.Idle.Feet == 'Tandava Crackows' and sent.Idle.Body == 'Nyame Mail' and sent.Idle.Head == nil and sent.Cure.Hands == 'Nyame Gauntlets')
check('trust sets read back', profile.ReadTrustSets('Shantotto II').Idle.Head == 'remove')
sent = profile.WriteTrust('Shantotto II', { Idle = { Head = 'Glory Crown' } })
check('trust built rewritten whole', sent.Cure == nil and sent.Idle.Feet == 'Tandava Crackows')

return table.concat(out, '\n')
""", str(ADDON), install, STARTER)
    print(result)
    print('all passed' if 'FAIL' not in result else 'FAILED')
finally:
    shutil.rmtree(tmp, ignore_errors=True)
