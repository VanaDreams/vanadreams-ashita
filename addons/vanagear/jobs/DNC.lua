--[[
    vanagear: Dancer.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against The Last Dance III: A Dancer's Guide by kate99 (FFXIAH node 373, last update
    June 2020; read in full 10 Oct 2026):
    - TP: first the dual wield the haste level needs, then DEX, accuracy, Store TP and
      multi-attack; hybrid sets matter ("a dead Dancer does no DPS").
    - Waltz potency caps at 50%, waltz received at 30%; samba and jig duration cap at 50%.
    - Weaponskills: Rudra's Storm DEX, attack and weaponskill damage, TP bonus past 2000;
      Evisceration crits and DEX; Pyrrhic Kleos STR and double attack.
    - Ability gear: Waltz (CHR, delay), Steps and Flourishes (DEX and accuracy), Violent
      Flourish (magic accuracy), Reverse Flourish, Samba and Jig duration.
]]--

local waltz = { Stats = { WALTZ_POTENCY = 3, CHR = 0.5, VIT = 0.2, WALTZ_DELAY = 1 }, Caps = { WALTZ_POTENCY = 50 } };
local step = { Stats = { STEP_ACCURACY = 1, ACC = 1, DEX = 0.3, STEP_FINISH = 2 } };

return {
    Fighter = true,
    Tweaks = {
        Tp_Default = { Stats = { STORETP = 1.8, DEX = 0.5 } },
    },
    Abilities = {
        ['Curing Waltz'] = waltz, ['Curing Waltz II'] = waltz, ['Curing Waltz III'] = waltz,
        ['Curing Waltz IV'] = waltz, ['Curing Waltz V'] = waltz, ['Divine Waltz'] = waltz, ['Divine Waltz II'] = waltz,
        ['Box Step'] = step, ['Quickstep'] = step, ['Stutter Step'] = step, ['Feather Step'] = step,
        ['Violent Flourish'] = { Stats = { MACC = 1, VFLOURISH_MACC = 1, ACC = 0.5 } },
        ['Reverse Flourish'] = { Stats = { REVERSE_FLOURISH_EFFECT = 1 } },
        ['Drain Samba III'] = { Stats = { SAMBA_DURATION = 1, SAMBA_PDURATION = 1 } },
        ['Haste Samba'] = { Stats = { SAMBA_DURATION = 1, SAMBA_PDURATION = 1 } },
        ['Spectral Jig'] = { Stats = { JIG_DURATION = 1 } },
    },
};
