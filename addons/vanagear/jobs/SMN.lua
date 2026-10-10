--[[
    vanagear: Summoner.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against the Summoner Guide by Lunareticc (FFXIAH node 245, last updated January 2017;
    read in full 10 Oct 2026):
    - Idle with an avatar out (the guide's perpetuation set): perpetuation cost down, Refresh,
      pet haste, pet damage taken.
    - Blood pact press: Blood Pact Delay and Delay II, II capped at -15 s.
    - Physical rage pacts: blood pact damage, pet attack and crit. Magical rage pacts: blood
      pact damage, pet magic attack. Debuff wards: summoning skill, pet magic accuracy, Blood
      Boon. Buff wards: summoning skill, avatar TP bonus.
    - Avatar PDT set: pet damage taken, pet regen.
    - Elemental Siphon: summoning skill.
    - Cure, enhancing, enfeebling and nuke sets as for any caster; fast cast 54% in the guide's set.
]]--

return {
    Fighter = true, Pet = true, Skills = { 'Healing Magic', 'Enhancing Magic' },
    Modes = { 'BPDelay', 'BPWard' },
    Tweaks = {
        Idle = { Stats = { REFRESH = 8, PERPETUATION_REDUCTION = 6 } },
        PetTP = { Stats = { PERPETUATION_REDUCTION = 4, REFRESH = 3 } },
        PetAtk = { Stats = { BP_DAMAGE = 3 }, Pet = { ATT = 1, ACC = 0.5, CRITHITRATE = 1 } },
        PetMab = { Stats = { BP_DAMAGE = 3 }, Pet = { MATT = 2, MACC = 0.5 } },
        PetAcc = { Stats = { SUMMONING = 1, BLOOD_BOON = 0.5 }, Pet = { MACC = 2 } },
    },
    Abilities = {
        ['Elemental Siphon'] = { Stats = { SUMMONING = 1, ENHANCES_ELEMENTAL_SIPHON = 2 } },
    },
};
