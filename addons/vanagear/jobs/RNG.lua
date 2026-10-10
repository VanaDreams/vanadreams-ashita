--[[
    vanagear: Ranger.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against the RNG Guide by kunami (FFXIAH node 365, last updated March 2023; read in full
    10 Oct 2026):
    - Preshot: Snapshot to its 70% cap first (gifts, gear, traits and Flurry all count), then
      Rapid Shot.
    - Midshot: TP gain to weaponskill as often as possible, Store TP before damage.
    - Weaponskills: Trueflight wants light affinity, weaponskill damage, AGI, magic attack;
      Last Stand weaponskill damage, AGI and ranged attack; Jishnu's Radiance DEX and crits.
      The stats each one reads come from the server's own script.
    - Ability gear: Sharpshot, Barrage, Camouflage, Scavenge.
]]--

return {
    Fighter = true, Shooter = true,
    Tweaks = {
        Midshot = { Stats = { STORETP = 2.5 } },
    },
    -- Ability sets come from what the server reads when each ability goes off (Sharpshot,
    -- Camouflage, Scavenge, Shadowbind, Bounty Shot). The server reads no gear when Barrage
    -- goes off, so no set is worn for it.
};
