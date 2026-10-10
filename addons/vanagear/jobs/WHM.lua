--[[
    vanagear: White Mage.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.
    Starting weights, to be read against an FFXIAH guide for the job.
]]--

return {
    Fighter = true, Skills = { 'Healing Magic', 'Enhancing Magic', 'Divine Magic', 'Enfeebling Magic' },
    Tweaks = {
    Cure = { Stats = { CURE_POTENCY = 3.5 } },
    },
};
