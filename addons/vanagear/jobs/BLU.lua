--[[
    vanagear: Blue Mage.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against The Beast Within - A Guide to Blue Mage by Prothescar (FFXIAH node 25, last
    modified March 2024; read in full 10 Oct 2026):
    - Idle: damage taken to -50% and magic evasion first, then Refresh or Regen.
    - TP: DEX, accuracy, Store TP or double attack, triple attack; gear haste caps at 25%; with
      Erratic Flutter, Mighty Guard and capped gear haste, about 11% more Dual Wield from gear.
    - Accuracy set aims around 1200-1250 accuracy before buffs.
    - Physical blue magic: STR, accuracy/attack (Chain Affinity gear on top).
    - Magical blue magic and Sanguine Blade: INT, magic attack, magic damage.
    - Fast cast caps at 80%.
    - White Wind and the healing spells: MND and cure potency; cure potency received caps at 30%.
    - Magic accuracy set for Dream Flower and the debuffs: the most magic accuracy per slot.
    - Ability gear: Burst Affinity, Chain Affinity.
]]--

return {
    Fighter = true, Skills = { 'Blue Magic', 'Healing Magic', 'Enhancing Magic' },
    Tweaks = {
        Idle = { Stats = { PDT = -4, MDT = -3, MEVA = 0.08 } },
        BlueMagical = { Stats = { MAGIC_DAMAGE = 1.2 } },
        Cure = { Stats = { BLUE = 0.3 } },
    },
    Modes = { 'Macc' },
    Abilities = {
        ['Burst Affinity'] = { Stats = { ENHANCES_BURST_AFFINITY = 1 } },
        ['Chain Affinity'] = { Stats = { ENHANCES_CHAIN_AFFINITY = 1 } },
    },
};
