--[[
    vanagear: Geomancer.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against Bubble Trouble: A Geomancer Guide by Brixy (FFXIAH node 112, last modified
    September 2022; read in full 10 Oct 2026):
    - Geomancy: skill first (900 with capped skill, merits and gear), then Conserve MP; every
      Indi and Geo spell is cast in it.
    - Luopan: pet damage taken and pet regen make it last.
    - Idle: Regen and Refresh; movement from Geomancy Sandals.
    - Damage taken capped with Shell V, plenty of magic defence and evasion.
    - Cure potency to 50%, then healing skill, MND. Enfeebling by MND or INT. Enhancing by skill
      (Phalanx, Stoneskin) or duration (Haste).
    - Nukes: magic attack; a high-tier set with as much magic accuracy as it takes to land.
    - Ability gear: Bolster, Life Cycle, Full Circle, Mending Halation, Radial Arcana.
]]--

return {
    Fighter = true, Pet = true, Skills = { 'Geomancy', 'Elemental Magic', 'Enfeebling Magic', 'Dark Magic', 'Healing Magic', 'Enhancing Magic' },
    Tweaks = {
        Idle = { Stats = { REFRESH = 7, REGEN = 3 } },
        Geomancy = { Stats = { GEOMANCY_SKILL = 1.5, HANDBELL_SKILL = 1.5, CONSERVE_MP = 0.3 } },
        PetDT = { Pet = { REGEN = 2, DMG = -5 } },
    },
    -- Bolster, Life Cycle, Full Circle, Theurgic Focus and the rest: their sets come from what
    -- the server reads on the press.
};
