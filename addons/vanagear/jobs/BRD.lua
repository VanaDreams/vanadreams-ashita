--[[
    vanagear: Bard.

    Which kinds of sets this job gets, and any weight that differs from vg\roles.lua. A weight
    here replaces the one in roles.lua for that stat; 0 turns the stat off for this job.

    Read against You Spoony Guide! - A Troubadour's Libretto by Scragg (FFXIAH node 58, last
    modified May 2020; read in full 10 Oct 2026):
    - Song precast: cap casting time reduction at 80% in as few pieces as possible; song
      casting time pieces beat plain fast cast.
    - Buff songs: all-songs and song duration first; per-song pieces (Paeon, Ballad, March...)
      where the player has them.
    - Debuff songs need magic accuracy: about 2 CHR to 1 magic accuracy, singing skill about
      1 to 1, string and wind skill a little less.
    - Cure: cure potency to its 50% cap first, then recast and less enmity.
    - Stoneskin caps at 350 HP from Enhancing skill and MND.
    - Damage taken set: -50% PDT.
]]--

return {
    Fighter = true, Skills = { 'Singing', 'Healing Magic', 'Enhancing Magic', 'Enfeebling Magic' },
    Tweaks = {
        Song = { Stats = { ALL_SONGS_EFFECT = 5, SONG_DURATION_BONUS = 2.5, SINGING = 0.6, CHR = 0.5, MACC = 1,
                           STRING = 0.4, WIND = 0.4 } },
        Cure = { Stats = { ENMITY = -0.5 } },
    },
};
