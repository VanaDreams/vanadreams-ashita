--[[
    vanagear: Scholar.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against A Scholar's Education by tooheyv (FFXIAH node 41, last modified May 2015, an old
    guide; read in full 10 Oct 2026):
    - Idle: Refresh, then damage reduction, then Regen; Sublimation gear counts like refresh.
    - Precast: as much fast cast and casting time reduction as possible.
    - Cure: cure potency to 50%, then healing skill, MND, VIT; enmity no concern.
    - Enhancing: skill toward 500; Cursna gear; Drain/Aspir potency and dark skill.
    - Enfeebling: magic accuracy and enfeebling skill, MND for Paralyze and Slow.
    - Helixes gain a lot from magic damage.
]]--

return {
    Fighter = true, Skills = { 'Healing Magic', 'Enhancing Magic', 'Enfeebling Magic', 'Elemental Magic', 'Dark Magic' },
    Tweaks = {
        Idle = { Stats = { REFRESH = 8, SUBLIMATION_BONUS = 2 } },
        Cure = { Stats = { HEALING = 0.8, ENMITY = 0 } },
        Nuke = { Stats = { MAGIC_DAMAGE = 1.5 } },
    },
};
