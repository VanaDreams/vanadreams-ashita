--[[
    vanagear: Black Mage.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against The Black Sacrament by Byrne/Kiddeath, Verbannt/Akton and Toralin (FFXIAH node
    297, last modified March 2021; read in full 10 Oct 2026):
    - Free nukes: magic attack and magic accuracy, burst bonus irrelevant; accuracy versions
      trade some magic attack for accuracy.
    - Bursts: magic burst bonus from gear soft-caps at 40%; Burst II on top of it; then magic
      attack and accuracy.
    - Above 10 dINT, 2 INT count as 1 magic accuracy.
    - Death: MP, magic attack, burst; precast MP then fast cast.
    - Aspir: Drain/Aspir potency and magic accuracy. Enfeebling: skill over accuracy.
    - Mana Wall: damage taken gear on the ability.
    - Idle: Refresh.
]]--

return {
    Fighter = true, Skills = { 'Elemental Magic', 'Enfeebling Magic', 'Dark Magic' },
    Tweaks = {
        Idle = { Stats = { REFRESH = 8 } },
        Nuke = { Stats = { MATT = 2.2, MACC = 0.8, INT = 0.8 } },
        Burst = { Stats = { MAGIC_BURST_BONUS_CAPPED = 4, MAGIC_BURST_BONUS_UNCAPPED = 4.5 } },
        Enfeebling = { Stats = { ENFEEBLE = 1.5, MACC = 1.2 } },
    },
    Modes = { 'Macc' },
    Abilities = {
        ['Mana Wall'] = { Stats = { PDT = -5, MDT = -5 }, Caps = { PDT = -50, MDT = -50 } },
    },
};
