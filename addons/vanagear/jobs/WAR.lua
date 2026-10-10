--[[
    vanagear: Warrior.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against The Parthenon: A Warrior's Kyklos (FFXIAH forum topic 35200; the guide is the
    topic's opening posts, read 10 Oct 2026):
    - TP sets come in accuracy tiers (low, mid, high); the capes are DEX, accuracy, attack and
      double attack, or Store TP without Samurai Roll.
    - Weaponskill sets: STR, accuracy and attack, weaponskill damage (cape STR+30 Acc/Atk+20
      WSD+10). Resolution is five hits, so the elemental gorget and belt carry to every hit;
      Upheaval reads VIT; Ukko's Fury crits more with TP. Each weaponskill's stats come from the
      server's own script.
    - Ability gear worn as the ability goes off: Berserk duration (Agoge Calligae, Pummeler's
      Lorica), Warcry (Agoge Mask), Blood Rage (Ravager's Lorica), Restraint (Ravager's Mufflers).
    - PDT set: survive the biggest hits long enough to be healed.
]]--

return {
    Fighter = true,
    Tweaks = {
        Tp_Default = { Stats = { DOUBLE_ATTACK = 2.2, STORETP = 1.5 } },
        Dt = { Stats = { PDT = -12, HP = 0.06 } },
    },
    Abilities = {
        ['Berserk'] = { Stats = { BERSERK_DURATION = 1, BERSERK_POTENCY = 2 } },
        ['Warcry'] = { Stats = { WARCRY_DURATION = 1 } },
        ['Aggressor'] = { Stats = { AGGRESSOR_DURATION = 1 } },
        ['Blood Rage'] = { Stats = { ENHANCES_BLOOD_RAGE = 1 } },
        ['Restraint'] = { Stats = { ENHANCES_RESTRAINT = 1 } },
    },
};
