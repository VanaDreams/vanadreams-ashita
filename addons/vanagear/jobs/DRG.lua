--[[
    vanagear: Dragoon.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against Sigurd's Descendants: The Art of Dragon Slaying by Bleusummers (FFXIAH node 182,
    September 2014; read in full 10 Oct 2026). The guide's sets are item lists with few stated
    priorities; what it does say:
    - TP: Store TP and multi-attack, with gear haste less needed under Hasso; accuracy tiers.
    - Weaponskills: Drakesbane wants critical damage; Stardiver's four hits all carry fTP, so the
      elemental gorget and belt win; Moonshade adds TP bonus.
    - Jumps are job abilities: attack speed does nothing, multi-attack and Store TP do, and Jump
      gear adds base TP and attack.
    - Ability gear: Ancient Circle, Spirit Link, Angon, Deep Breathing.
]]--

return {
    Fighter = true, Pet = true,
    Tweaks = {
        Tp_Default = { Stats = { STORETP = 2 } },
    },
    Abilities = {
        ['Jump'] = { Stats = { JUMP_TP_BONUS = 0.05, JUMP_ATT_BONUS = 1, DOUBLE_ATTACK = 1.5, TRIPLE_ATTACK = 2, STORETP = 1, ACC = 0.3 } },
        ['High Jump'] = { Stats = { JUMP_TP_BONUS = 0.05, JUMP_ATT_BONUS = 1, DOUBLE_ATTACK = 1.5, TRIPLE_ATTACK = 2, STORETP = 1, ACC = 0.3 } },
        ['Spirit Jump'] = { Stats = { JUMP_SPIRIT_TP_BONUS = 0.05, JUMP_SOUL_SPIRIT_ATT_BONUS = 1, DOUBLE_ATTACK = 1.5, STORETP = 1 } },
        ['Soul Jump'] = { Stats = { JUMP_SPIRIT_TP_BONUS = 0.05, JUMP_SOUL_SPIRIT_ATT_BONUS = 1, DOUBLE_ATTACK = 1.5, STORETP = 1 } },
        ['Ancient Circle'] = { Stats = { ANCIENT_CIRCLE_DURATION = 1, ANCIENT_CIRCLE_POTENCY = 2 } },
        ['Spirit Link'] = { Stats = { ENHANCES_SPIRIT_LINK = 1 } },
        ['Deep Breathing'] = { Stats = { ENHANCE_DEEP_BREATHING = 1 } },
    },
};
