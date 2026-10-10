--[[
    vanagear: White Mage.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against On Healing Hands - A Comprehensive WHM Guide v3 by Wotasu (FFXIAH node 408,
    last modified July 2025; read in full 10 Oct 2026):
    - Precast: fast cast and quick cast just to 80% combined; cure precast the same for cures.
    - Cure: cure potency to its cap, then MND, less enmity, Conserve MP; Curaga with healing skill.
    - Cursna for Doom: Cursna success gear and healing skill.
    - Enhancing: skill for barspells and boosts (500 cap); duration for the rest; Regen potency
      and duration; Stoneskin bonus; Aquaveil.
    - Enfeebling: MND and magic accuracy.
    - Idle: Refresh; a damage-taken and magic evasion idle for danger.
    - Weaponskills: Black Halo and Realmrazer MND and weaponskill damage; Hexa Strike STR and crits.
]]--

return {
    Fighter = true, Skills = { 'Healing Magic', 'Enhancing Magic', 'Divine Magic', 'Enfeebling Magic' },
    Tweaks = {
        Idle = { Stats = { REFRESH = 8 } },
        Cure = { Stats = { CURE_POTENCY = 3.5, MND = 0.6, ENMITY = -0.5, CONSERVE_MP = 0.3, HEALING = 0.4 } },
        Cursna = { Stats = { ENHANCES_CURSNA = 4, HEALING = 1.2 } },
        Regen = { Stats = { REGEN_DURATION = 2.5 } },
    },
};
