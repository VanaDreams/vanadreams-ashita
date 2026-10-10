--[[
    vanagear: Dark Knight.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against the DRK Guide by Quetzacoatl (FFXIAH node 52, last modified May 2017; read in
    full 10 Oct 2026):
    - TP: Store TP, multi-attack, accuracy and attack, critical damage, then gear haste as
      needed. Last Resort with Desperate Blows covers about 25% of the haste, so gear haste
      is needed less while it is up (26% without it).
    - Weaponskills: STR, accuracy/attack and weaponskill damage (Cross Reaper, Catastrophe);
      Torcleaver reads VIT; one-hit weaponskills like attack. Moonshade's TP Bonus raises damage.
    - Dark Magic: skill, and Drain/Aspir potency; Absorb spells the same with Absorb gear; Dread
      Spikes is HP; Stun uses the Dark Magic set.
    - Fast cast near 78% with the guide's set.
    - Ability gear worn as the ability goes off: Souleater, Arcane Circle, Blood Weapon,
      Nether Void, Dark Seal, Diabolic Eye.
]]--

return {
    Fighter = true, Skills = { 'Dark Magic', 'Elemental Magic', 'Enfeebling Magic' },
    Tweaks = {
        Tp_Default = { Stats = { STORETP = 2, CRIT_DMG_INCREASE = 0.5 } },
        Dark = { Stats = { DARK = 1.5, ENHANCES_ABSORB_EFFECTS = 2 } },
        Drain = { Stats = { ENH_DRAIN_ASPIR = 3, DARK = 1 } },
    },
    Modes = {},
    Abilities = {
        ['Souleater'] = { Stats = { SOULEATER_EFFECT = 2, SOULEATER_EFFECT_II = 2 } },
        ['Arcane Circle'] = { Stats = { ARCANE_CIRCLE_DURATION = 1, ARCANE_CIRCLE_POTENCY = 2 } },
        ['Blood Weapon'] = { Stats = { ENHANCES_BLOOD_WEAPON = 1 } },
        ['Nether Void'] = { Stats = { ENHANCES_NETHER_VOID = 1 } },
        ['Dark Seal'] = { Stats = { ENHANCES_DARK_SEAL = 1 } },
        ['Diabolic Eye'] = { Stats = { ENHANCES_DIABOLIC_EYE = 1 } },
        ['Dread Spikes'] = { Stats = { HP = 1 } },
    },
};
