--[[
    vanagear: Ninja.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against the NIN Guide by Kincard (FFXIAH node 35, last update May 2020; read in full
    10 Oct 2026):
    - TP: Dual Wield to the delay floor for the haste you have (with the 35% trait, 39 from gear
      at no haste down to about 1 at capped haste); past that, more Dual Wield lowers TP gain.
    - Physical weaponskills lean on the weaponskill's own stat (Blade: Ten STR/DEX, Hi AGI, Shun
      DEX, Metsu DEX) over accuracy and attack; magical ones (Aeolian Edge, Blade: Yu) on magic
      attack first.
    - Damage taken caps at -50%; a hybrid set gets near -47% while still fighting.
    - Ninjutsu: magic accuracy for the debuffs, magic attack (and burst) for the nukes; Utsusemi
      cast time on precast.
    - Fast cast; enmity set for tanking.
]]--

return {
    Fighter = true, Tank = true, Shooter = true, Skills = { 'Ninjutsu' },
    Tweaks = {
        Tp_Hybrid = { Stats = { PDT = -3.5, MDT = -2.5 } },
        Ninjutsu = { Stats = { MATT = 2, MACC = 1, NINJUTSU = 0.8, NIN_NUKE_BONUS_INNIN = 1 } },
    },
    Modes = { 'Macc', 'Burst' },
};
