--[[
    vanagear: Thief.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against For the Shinies! A Guide for Thief by Malizia (FFXIAH node 171, last updated
    January 2026; read in full 10 Oct 2026). The guide covers abilities, traits and merits and
    has no gear sets; what it says about gear:
    - TP: stack triple attack; the 550 job point gift changes dual wield needs.
    - Sneak Attack and Trick Attack are used with weaponskills, so weaponskill damage over the
      Sneak/Trick bonus pieces.
    - Treasure Hunter is the job's centre; TH gear on Sneak Attack especially.
    - Ability gear: Steal, Feint, Assassin's Charge, Aura Steal, Flee.
]]--

return {
    Fighter = true,
    Tweaks = {
        Tp_Default = { Stats = { TRIPLE_ATTACK = 3.5, TREASURE_HUNTER = 2 } },
    },
    Abilities = {
        ['Steal'] = { Stats = { STEAL = 1 } },
        ['Feint'] = { Stats = { AUGMENTS_FEINT = 1 } },
        ['Assassin\'s Charge'] = { Stats = { AUGMENTS_ASSASSINS_CHARGE = 1 } },
        ['Aura Steal'] = { Stats = { AUGMENTS_AURA_STEAL = 1 } },
        ['Sneak Attack'] = { Stats = { TREASURE_HUNTER = 5, DEX = 0.5, SNEAK_ATK_DEX = 0.5 } },
        ['Trick Attack'] = { Stats = { AGI = 0.5, TRICK_ATK_AGI = 0.5 } },
    },
};
