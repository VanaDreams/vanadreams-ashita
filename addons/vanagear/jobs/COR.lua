--[[
    vanagear: Corsair.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against Luck of the Draw: A Corsair Guide (FFXIAH node 326, last updated September
    2023, read in full 10 Oct 2026):
    - Phantom Roll: potency first (the highest piece counts, they do not add), then duration and
      recast; fill the rest with damage taken and magic evasion.
    - Preshot: Snapshot to the 70% cap, 10% of which comes from job points, then Rapid Shot.
    - Midshot: ranged accuracy and Store TP for fewer shots to 1000 TP; damage a distant second.
    - Quick Draw: magic attack, AGI, magic accuracy; Light and Dark Shot need magic accuracy to land.
    - Idle: damage taken and magic evasion, since COR has no native defence traits.
    - Weaponskills: Leaden Salute and Wildfire are magical, Last Stand physical ranged, Savage
      Blade physical; the stats each reads come from the server's own scripts.
    - Casting: fast cast; Cure set for /WHM and /SCH.
]]--

return {
    Fighter = true, Shooter = true,
    Modes = { 'PhantomRoll', 'QuickDraw', 'QuickDrawAcc' },
    Tweaks = {
        Idle = { Stats = { MEVA = 0.08, PDT = -4, MDT = -3, REFRESH = 1 } },
        Preshot = { Caps = { SNAPSHOT = 60 } },
    },
};
