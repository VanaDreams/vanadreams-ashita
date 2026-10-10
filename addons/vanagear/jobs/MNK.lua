--[[
    vanagear: Monk.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.
    Starting weights, to be read against an FFXIAH guide for the job.
]]--

return {
    Fighter = true,
    Tweaks = {
    Tp_Default = { Stats = { MARTIAL_ARTS = 0.5, KICK_ATTACK_RATE = 0.8, SUBTLE_BLOW = 0.3 } },
    },
};
