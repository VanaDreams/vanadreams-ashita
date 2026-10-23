r"""Off-game check of the job templates' level bands (M401, 22 Oct 2026):
- every jobs\<JOB>.lua loads and its Bands rise (To = 30, 60, 75, 99);
- every mode a band or tweak names exists in roles.lua, the job's Modes or a band's Modes;
- plan.Band picks the band the level falls in, and the band's tweaks lay over the job's.

    python addons/vanagear/tests/bands.py
"""
from pathlib import Path

from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent

L = luajit21.LuaRuntime(unpack_returned_tuples=True)
result = L.execute(r"""
local addon_path = ...
package.path = addon_path .. '/?.lua;' .. package.path
package.loaded['vg.data'] = { ModId = function() return 1 end, Items = function() return {} end, Abilities = function() return {} end, Weaponskills = function() return {} end }
AshitaCore = { GetInstallPath = function() return addon_path .. '/../../' end }
local roles = require('vg.roles')
local plan = require('vg.plan')
local jobs = { 'WAR','MNK','WHM','BLM','RDM','THF','PLD','DRK','BST','BRD','RNG','SAM','NIN','DRG','SMN','BLU','COR','PUP','DNC','SCH','GEO','RUN' }
local problems, banded = {}, 0
for _, abbr in ipairs(jobs) do
    local job = plan.Job(abbr)
    local known = {}
    for name in pairs(roles.Modes) do known[name] = true end
    for _, m in ipairs(job.Modes or {}) do known[m] = true end
    for _, b in ipairs(job.Bands or {}) do for _, m in ipairs(b.Modes or {}) do known[m] = true end end
    local function checkTweaks(where, tweaks)
        for mode in pairs(tweaks or {}) do
            if not known[mode] then problems[#problems + 1] = abbr .. ' ' .. where .. ': no mode ' .. mode end
        end
    end
    checkTweaks('Tweaks', job.Tweaks)
    local last = 0
    for i, b in ipairs(job.Bands or {}) do
        if (b.To or 0) <= last then problems[#problems + 1] = abbr .. ' band ' .. i .. ' does not rise' end
        last = b.To or 0
        checkTweaks('band ' .. tostring(b.To), b.Tweaks)
    end
    if job.Bands then
        banded = banded + 1
        if last ~= 99 then problems[#problems + 1] = abbr .. ' last band is ' .. last .. ', not 99' end
    end
    if job.Support and job.Fighter then problems[#problems + 1] = abbr .. ' is both Support and Fighter' end
end
-- the pick: WAR at 45 gets the 60 band, at 99 the last; its tweak lays over the all-level one
local war = plan.Job('WAR')
local b45, b99, b1 = plan.Band(war, 45), plan.Band(war, 99), plan.Band(war, 1)
local out = { 'banded ' .. banded, 'WAR@45 -> ' .. tostring(b45 and b45.To), 'WAR@99 -> ' .. tostring(b99 and b99.To), 'WAR@1 -> ' .. tostring(b1 and b1.To) }
if #problems > 0 then out[#out + 1] = 'PROBLEMS: ' .. table.concat(problems, '; ') end
return table.concat(out, '\n')
""", str(ADDON))
print(result)
lines = result.split('\n')
assert lines[0] == 'banded 22', lines[0]
assert lines[1] == 'WAR@45 -> 60' and lines[2] == 'WAR@99 -> 99' and lines[3] == 'WAR@1 -> 30', lines[1:4]
assert not any(l.startswith('PROBLEMS') for l in lines), '\n'.join(lines)
print('PASS')
