--[[
    vanagear: Puppetmaster.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against PUP Gearsets by Falsetto with Aerix (FFXIAH node 426, last updated October
    2022; read in full 10 Oct 2026):
    - Idle: HP, damage taken and magic evasion; pet regen for the dual regen idle.
    - Master TP: DEX, accuracy/attack, double attack, triple attack, martial arts.
    - Pet TP: pet haste to its cap first, then pet accuracy and double attack; Store TP when
      Companion's Roll is missing.
    - Pet weaponskills: pet TP bonus (Arcuballista, Daze), double attack (Bone Crusher).
    - Pet tank sets: the automaton's damage taken and regen.
    - Pet nuke set: pet magic attack and accuracy.
    - Ability gear: Repair potency, Overdrive.
]]--

return {
    Fighter = true, Pet = true,
    Tweaks = {
        Tp_Default = { Stats = { MARTIAL_ARTS = 0.5 } },
        PetTP = { Pet = { HASTE_GEAR = 4, ACC = 0.8, DOUBLE_ATTACK = 2.2, STORETP = 0.8 } },
        PetAtk = { Pet = { PET_TP_BONUS = 0.02, TP_BONUS = 0.01 } },
        PetDT = { Pet = { REGEN = 1.5 } },
    },
    -- Repair, Overdrive, Tactical Switch: their sets come from what the server reads on the press.
};
