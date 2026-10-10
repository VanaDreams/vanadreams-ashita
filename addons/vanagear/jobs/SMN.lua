--[[
    vanagear: Summoner.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.
    Starting weights, to be read against an FFXIAH guide for the job.
]]--

return {
    Fighter = true, Pet = true, Skills = { 'Healing Magic', 'Enhancing Magic' },
    Tweaks = {
    Idle = { Stats = { REFRESH = 8, AVATAR_PERPETUATION = 0, PERPETUATION_REDUCTION = 6 } },
    },
};
