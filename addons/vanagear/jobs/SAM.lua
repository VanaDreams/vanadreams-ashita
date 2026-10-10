--[[
    vanagear: Samurai.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against Bushido - The Way of The Samurai (A Guide) V. 2.0 by Reiden (FFXIAH node 125,
    last modified Sept 2020; read in full 10 Oct 2026):
    - TP sets in low, mid and high accuracy, built for 2100 job points: with fewer job points,
      more Store TP. Cape DEX, accuracy/attack, Store TP or double attack.
    - Weaponskills (Fudo, Kaiten, Shoha, Rana): STR, accuracy/attack, weaponskill damage; the
      stats each one reads come from the server's own script.
    - Bow TP and Namas Arrow for Yoichinoyumi.
    - Ability gear worn as the ability goes off: Meditate (duration), Third Eye (counter),
      Sengikori, Warding Circle, Seigan.
]]--

return {
    Fighter = true, Shooter = true,
    Tweaks = {
        Tp_Default = { Stats = { STORETP = 2.2, ZANSHIN = 0.5 } },
        Tp_Acc = { Stats = { STORETP = 1.5 } },
    },
    Abilities = {
        ['Meditate'] = { Stats = { MEDITATE_DURATION = 1 } },
        ['Third Eye'] = { Stats = { THIRD_EYE_COUNTER_RATE = 1, THIRD_EYE_BONUS = 1 } },
        ['Sengikori'] = { Stats = { SENGIKORI_BONUS = 1 } },
        ['Seigan'] = { Stats = { SEIGAN_COUNTER_BONUS = 1 } },
    },
};
