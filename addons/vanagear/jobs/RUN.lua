--[[
    vanagear: Rune Fencer.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.
    Starting weights, to be read against an FFXIAH guide for the job.
]]--

return {
    Fighter = true, Tank = true, Skills = { 'Enhancing Magic', 'Divine Magic' },
    Tweaks = {
    Tp_Default = { Stats = { PDT = -2, MDT = -2, ENMITY = 0.5, PARRY = 0.3 } },
    },
};
