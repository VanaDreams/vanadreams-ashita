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
    -- Ability sets come from what the server reads when each ability goes off (Steal, Mug,
    -- Flee, Hide, Feint, Accomplice, Conspirator, Despoil). The server reads no gear when
    -- Sneak Attack or Trick Attack goes off, so no set is worn for them.
};
