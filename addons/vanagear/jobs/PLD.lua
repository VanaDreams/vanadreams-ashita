--[[
    vanagear: Paladin.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against The Divine Protector: PLD Guide by Tetsouou (FFXIAH node 482, last modified
    August 2026; read in full 10 Oct 2026):
    - Idle and engaged: physical and magical damage taken capped at -50%, with HP, DEF and VIT
      high; magic evasion and magic defence for magic fights; engaged adds Store TP, enmity and
      accuracy.
    - Fast cast to the 80% cap; defence does not matter in it. Keep the sets' HP close together.
    - Cure: potency to 50% (Majesty covers potency II), spell interruption to the 102% cap,
      then damage taken; self cures want HP high.
    - Phalanx: Phalanx+ gear and 500 enhancing skill; in combat with spell interruption down.
    - Enmity is capped at +200 from gear.
    - Spell interruption caps at 102%.
    - Ability gear worn as it goes off: Rampart, Sentinel, Cover, Fealty, Chivalry, Divine Emblem,
      Holy Circle, Shield Bash.
]]--

return {
    Fighter = true, Tank = true, Skills = { 'Healing Magic', 'Enhancing Magic', 'Divine Magic' },
    Tweaks = {
        Idle = { Stats = { PDT = -6, MDT = -5, HP = 0.05, DEF = 0.03, VIT = 0.2, MEVA = 0.05, ENMITY = 0.2 } },
        Tp_Default = { Stats = { PDT = -3, MDT = -2.5, ENMITY = 0.5, SHIELDBLOCKRATE = 1, STORETP = 1, HP = 0.02 } },
        Tp_Hybrid = { Stats = { PDT = -5, MDT = -4, ENMITY = 0.5 } },
        Cure = { Stats = { SPELLINTERRUPT = 1, PDT = -1, MDT = -1, HP = 0.03, ENMITY = 0.5 }, Caps = { SPELLINTERRUPT = 102 } },
        Phalanx = { Stats = { PHALANX = 6, ENHANCE = 0.6, SPELLINTERRUPT = 0.5 }, Caps = { SPELLINTERRUPT = 102 } },
        Divine = { Stats = { ENMITY = 1, FASTCAST = 1 } },
    },
    Abilities = {
        ['Rampart'] = { Stats = { RAMPART_DURATION = 1 } },
        ['Sentinel'] = { Stats = { SENTINEL_EFFECT = 1 } },
        ['Cover'] = { Stats = { COVER_DURATION = 1, COVER_TO_MP = 1 } },
        ['Fealty'] = { Stats = { ENHANCES_FEALTY = 1 } },
        ['Chivalry'] = { Stats = { ENHANCES_CHIVALRY = 1, MND = 0.2 } },
        ['Divine Emblem'] = { Stats = { ENHANCES_DIVINE_EMBLEM = 1 } },
        ['Holy Circle'] = { Stats = { HOLY_CIRCLE_DURATION = 1, HOLY_CIRCLE_POTENCY = 2 } },
        ['Shield Bash'] = { Stats = { SHIELD_BASH = 1 } },
        ['Provoke'] = { Stats = { ENMITY = 1 } },
    },
};
