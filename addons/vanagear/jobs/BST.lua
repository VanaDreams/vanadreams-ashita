--[[
    vanagear: Beastmaster.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against Falkirk's Killer Instinct: The Beastmaster Compendium (FFXIAH node 227, the
    gear sets as extracted 19 Sept 2026 in Claude\Output 058):
    - Idle is the guide's Idle Regen set (regen and damage taken); BST has no MP to refresh.
    - Pet TP: pet haste from gear caps at 25%, then pet double attack, pet accuracy and attack.
    - Physical Ready moves: pet attack and accuracy; pet TP bonus; pet double attack for the
      multi-hit ones. Magic Ready moves: pet magic attack and accuracy, INT.
    - Pet damage taken: pet DT and PDT, pet regen.
    - Sic and Ready recast -5 on the press (Desultor Tassets, Charmer's Merlin, Gleti's Breeches).
    - Reward: potency and MND. Charm: CHR and charm. Call Beast: jug pet level (Ankusa Gloves).
    - Weaponskills: Ruinator, Decimation and Rampage are physical multi-hit, Mistral Axe and
      Onslaught single-hit with weaponskill damage, Primal Rend and Cloudsplitter magical; the
      stat each one reads comes from the server's own weaponskill script.
]]--

return {
    Fighter = true, Pet = true,
    Modes = { 'ReadyPrecast', 'Reward', 'Charm', 'Call' },
    Tweaks = {
        Idle = { Stats = { REFRESH = 0, REGEN = 4 } },
        PetTP = { Pet = { DOUBLE_ATTACK = 2.5, REGAIN = 0.5 } },
        PetAtk = { Pet = { PET_TP_BONUS = 0.02 } },
        PetDT = { Pet = { REGEN = 1 } },
    },
};
