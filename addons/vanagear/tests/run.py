r"""Off-game test for vanagear's builder.

    python addons/vanagear/tests/run.py

Runs vg\core.lua under LuaJIT (lupa) with a stand-in for Ashita that carries the gear named in
Ferrin's BST profile, as BST99/DNC49 holding Kumbhakarna and Hunahpu, and prints the sets it
builds. Needs the lupa package. Augments are not in the stand-in, so augment-only stats (Artio's
Mantle's pet stats, the Desultor Tassets' Ready recast) do not count here.
"""
import re
import sys
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

# Ferrin's BST.lua gear, by the client names it uses.
GEAR = """Kumbhakarna|Hunahpu|Nyame Helm|Nyame Mail|Nyame Gauntlets|Nyame Flanchard|Nyame Sollerets|Sanctity Necklace
Kuku Stone|Suppanomimi|Eabani Earring|Keen Ring|Varar Ring|Atheling Mantle|Desultor Tassets|Sprinter's Shoes
Warp Ring|Capacity Ring|Pastoralist's Mantle|Ferine Necklace|Incarnation Sash|Artio's Mantle|Ankusa Gloves +2
Demonry Core|Taeon Tabard|Taeon Tights|Totemic Helm +1|Totemic Jackcoat +1|Totemic Trousers +1|Totemic Gaiters +1
Totemic Gloves +1|Crudelis Belt|Angel's Ring|Dynamic Belt|Moonshade Earring|Spiral Ring|Snow Belt|Flame Belt
Empress Band|Echad Ring|Facility Ring|Vocation Ring|Tavnazian Ring|Charmer's Merlin"""

LUA_PRELUDE = r"""
local addon_path = ...
package.path = addon_path .. '\\?.lua;' .. package.path
bit = require('bit')
function T(t) return t end
string.trimend = function(s, c) return (s:gsub('%z+$', '')) end
string.totable = function(s) local t = {} for i = 1, #s do t[i] = s:byte(i) end return t end
local function args(s) local t = {} for w in s:gmatch('%S+') do t[#t+1] = w end return t end
string.args = args
chat = setmetatable({}, { __index = function() return function(...) return table.concat({...}, ' ') end end })
ashita = { fs = { exists = function() return false end, create_directory = function() end },
           bits = { unpack_be = function() return 0 end } }
struct = { unpack = function() return 0 end }
"""


def norm(s):
    return re.sub(r'[^a-z0-9+]', '', s.lower().replace('+', 'plus'))


def main():
    L = luajit21.LuaRuntime(unpack_returned_tuples=True)
    L.execute(LUA_PRELUDE, str(ADDON))
    items = L.execute("return dofile(...)", str(ADDON / 'data' / 'items.lua'))
    by_name = {}
    for item_id in items.keys():
        by_name.setdefault(norm(items[item_id].name.replace('_', ' ')), item_id)
    carried, missing = [], []
    for name in [g for line in GEAR.splitlines() for g in line.split('|')]:
        item_id = by_name.get(norm(name))
        (carried if item_id else missing).append((name, item_id))
    print(f'carried {len(carried)}; not found by name in the server tables: {[m[0] for m in missing]}')

    L.globals().CARRIED = L.table_from({i + 1: L.table_from({'name': n, 'id': i_}) for i, (n, i_) in enumerate(carried)})
    L.execute(r"""
local addon_path = ...
local jobs = { [1]='WAR',[2]='MNK',[3]='WHM',[4]='BLM',[5]='RDM',[6]='THF',[7]='PLD',[8]='DRK',[9]='BST',[10]='BRD',[11]='RNG',
    [12]='SAM',[13]='NIN',[14]='DRG',[15]='SMN',[16]='BLU',[17]='COR',[18]='PUP',[19]='DNC',[20]='SCH',[21]='GEO',[22]='RUN' }
local byIdName = {}
for _, c in ipairs(CARRIED) do byIdName[c.id] = c.name end
local inv = {}
for i, c in ipairs(CARRIED) do inv[i] = { Id = c.id, Count = 1, Extra = '' } end
local equipped = { Main = 'Kumbhakarna', Sub = 'Hunahpu' }
AshitaCore = {
    GetInstallPath = function() return addon_path .. '\\..\\..\\' end,
    GetMemoryManager = function() return {
        GetInventory = function() return {
            GetContainerItem = function(_, c, i) if c == 0 then return inv[i] end return nil end,
            GetContainerCountMax = function(_, c) return (c == 0) and 80 or 0 end,
        } end,
        GetPlayer = function() return {
            GetMainJob = function() return 9 end, GetMainJobLevel = function() return 99 end,
            GetSubJob = function() return 19 end, GetSubJobLevel = function() return 49 end,
        } end,
        GetParty = function() return { GetMemberTargetIndex = function() return 1 end } end,
        GetEntity = function() return { GetRace = function() return 1 end } end,
    } end,
    GetResourceManager = function() return {
        GetItemById = function(_, id) return byIdName[id] and { Name = { byIdName[id] }, Races = 0 } or nil end,
        GetString = function(_, kind, id) return jobs[id] or '' end,
        GetAbilityById = function(_, id) return nil end,
    } end,
    GetChatManager = function() return { QueueCommand = function() end } end,
}
gSettings = { EquipBags = { 0 } }
gState = {}
gProfile = nil
local constants = { EquipSlotNames = { 'Main','Sub','Range','Ammo','Head','Body','Hands','Legs','Feet','Neck','Waist','Ear1','Ear2','Ring1','Ring2','Back' } }
gData = {
    Constants = constants,
    GetContainerAvailable = function(c) return c == 0 end,
    GetContainerMax = function() return 80 end,
    GetAugment = function() return { Type = 'Unaugmented' } end,
    GetEquipment = function()
        local t = {}
        for slot, name in pairs(equipped) do
            for _, c in ipairs(CARRIED) do if c.name == name then t[slot] = { Item = { Id = c.id }, Name = name } end end
        end
        return t
    end,
}
-- the weaponskill names the client would give
local wsNames = { [74]='Primal Rend', [76]='Cloudsplitter', [77]='Ruinator', [72]='Decimation', [71]='Mistral Axe' }
local rm = AshitaCore.GetResourceManager
AshitaCore.GetResourceManager = function()
    local r = rm()
    r.GetAbilityById = function(_, id) return wsNames[id] and { Name = { wsNames[id] } } or nil end
    return r
end
print = print or function() end
local core = require('vg.core')
gVana.Prefs.ZoneWait = 0
core.Moved()
for i = 1, 400 do core.Tick() if gVana.BuiltAt then break end end
""", str(ADDON))
    sets = L.globals().gVana.Sets
    explain = L.globals().gVana.Explain
    show = sys.argv[1:] or ['Idle', 'Dt', 'Tp_Default', 'Tp_Hybrid', 'PetTP', 'PetDT', 'PetAtk', 'PetMab', 'Ruinator',
                            'Cloudsplitter', 'PrimalRend', 'Ws_Default', 'Movement']
    names = sorted(sets.keys())
    print(f'{len(names)} sets: {", ".join(names)}')
    order = ['Main', 'Sub', 'Range', 'Ammo', 'Head', 'Neck', 'Ear1', 'Ear2', 'Body', 'Hands', 'Ring1', 'Ring2', 'Back', 'Waist', 'Legs', 'Feet']
    for name in show:
        s = sets[name]
        if s is None:
            print(f'\n{name}: not built')
            continue
        print(f'\n{name}  (score {explain[name].total:.1f})')
        for slot in order:
            e = s[slot]
            if e is not None:
                print(f'  {slot:6} {e.Name}')


if __name__ == '__main__':
    main()
