--[[
    vanagear: Rune Fencer.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against Destiny's Device - A New and Updated Guide to Rune Fencer by Shiraj (FFXIAH node
    397, last modified March 2026; read in full 10 Oct 2026). Its sets are item lists; what it
    states:
    - Tank sets: damage taken to the -50% cap, HP, magic evasion over magic defence (magic
      defence has diminishing returns), enmity, parry for the parry build.
    - DD sets: DEX and double attack capes.
    - Weaponskills: Resolution STR and double attack; Dimidiation DEX and weaponskill damage.
    - Ability gear: Swordplay, Liement, Vallation and Valiance, Pflug, Rayke, Gambit, Embolden,
      Battuta.
]]--

return {
    Fighter = true, Tank = true, Skills = { 'Enhancing Magic', 'Divine Magic' },
    Tweaks = {
        Idle = { Stats = { PDT = -5, MDT = -5, HP = 0.04, MEVA = 0.1, ENMITY = 0.2 } },
        Dt = { Stats = { MEVA = 0.15, HP = 0.06, ENMITY = 0.3 } },
        Tp_Default = { Stats = { PDT = -2, MDT = -2, ENMITY = 0.5, PARRY = 0.3, INQUARTATA = 1 } },
    },
    Abilities = {
        ['Swordplay'] = { Stats = { SWORDPLAY = 1 } },
        ['Liement'] = { Stats = { LIEMENT = 1, LIEMENT_DURATION = 1 } },
        ['Vallation'] = { Stats = { VALIANCE_VALLATION_DURATION = 1 } },
        ['Valiance'] = { Stats = { VALIANCE_VALLATION_DURATION = 1 } },
        ['Pflug'] = { Stats = { PFLUG = 1 } },
        ['Rayke'] = { Stats = { RAYKE_DURATION = 1 } },
        ['Gambit'] = { Stats = { GAMBIT_DURATION = 1 } },
        ['Embolden'] = { Stats = { EMBOLDEN_DURATION = 1 } },
        ['Battuta'] = { Stats = { ENHANCES_BATTUTA = 1, INQUARTATA = 1 } },
    },
};
