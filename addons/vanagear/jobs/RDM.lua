--[[
    vanagear: Red Mage.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against Jack of All Trades - A Guide to Red Mage by Rydal (FFXIAH node 295, last
    modified March 2019; read in full 10 Oct 2026):
    - Idle: Refresh and Regen, with a hybrid that keeps damage taken near the cap.
    - Fast cast to the 80% cap with the 30% trait helping.
    - Enfeebling: MND for Paralyze, Slow and Addle; enfeebling skill for Distract and Frazzle;
      magic accuracy for Sleep, Silence and Dispel.
    - Enhancing: duration on self; enhancing skill toward 500 (and past it for Enspells and Temper).
    - Nukes: INT, magic attack, magic accuracy; bursts overcap burst damage with Burst II pieces.
    - Cure potency to 50%, then healing skill and MND; Cursna wants 500 healing skill.
    - Spell interruption at 102%; Stoneskin caps at 350 HP.
    - Melee: capped haste with /NIN dual wield; Savage Blade wants weaponskill damage over
      double attack (mostly one hit); Chant du Cygne DEX and crits; Requiescat MND.
    - Ability gear: Saboteur, Chainspell, Composure.
]]--

return {
    Fighter = true, Skills = { 'Healing Magic', 'Enhancing Magic', 'Enfeebling Magic', 'Elemental Magic', 'Dark Magic', 'Divine Magic' },
    Tweaks = {
        Idle = { Stats = { REFRESH = 8, REGEN = 2, PDT = -2, MDT = -2 } },
        Enfeebling = { Stats = { MACC = 2, ENFEEBLE = 1.2, MND = 0.8 } },
        Enhancing = { Stats = { ENH_MAGIC_DURATION = 2.5, ENHANCE = 1.2 } },
    },
    Abilities = {
        ['Saboteur'] = { Stats = { ENHANCES_SABOTEUR = 1 } },
    },
};
