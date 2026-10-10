--[[
    vanagear: Monk.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against iiPunch - MNK 101 by Minjo (FFXIAH node 173, last modified April 2015; read in
    full 10 Oct 2026). The page ends after the job traits: its equipment section is not on it.
    What it says that bears on gear:
    - Delay: hand-to-hand delay floors at 96 after Martial Arts, gear, magic and ability haste.
    - Accuracy should be capped: Impetus and counters depend on it.
    - Ability gear worn as it goes off: Chakra (VIT and Chakra gear), Counterstance (counter),
      Boost, Footwork, Impetus.
]]--

return {
    Fighter = true,
    Tweaks = {
        Tp_Default = { Stats = { MARTIAL_ARTS = 0.5, KICK_ATTACK_RATE = 0.8, SUBTLE_BLOW = 0.3, ACC = 0.8, COUNTER = 0.3 } },
    },
    Abilities = {
        ['Chakra'] = { Stats = { VIT = 1, CHAKRA_MULT = 2, CHAKRA_REMOVAL = 1 } },
        ['Counterstance'] = { Stats = { COUNTERSTANCE_EFFECT = 1, COUNTER = 0.5 } },
        ['Boost'] = { Stats = { BOOST_EFFECT = 1 } },
        ['Footwork'] = { Stats = { FOOTWORK_ATT_BONUS = 1 } },
        ['Impetus'] = { Stats = { AUGMENTS_IMPETUS = 1 } },
    },
};
