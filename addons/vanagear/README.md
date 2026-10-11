# vanagear

Gear sets built from what you carry, for the job and level you are on, and a list of commands
for that job. Type `/vg` in game.

vanagear is built on [LuAshitacast](https://github.com/ThornyFFXI/LuAshitacast) by ThornyFFXI
(MIT licence, see `LICENSE-LuAshitacast`), which still does the swapping. It replaces the
separate luashitacast addon: tick one or the other, and the launcher keeps luashitacast out
while vanagear is ticked.

## What it does

- Reads your inventory and wardrobes, and scores every piece your job, level and race can wear
  against that job's weights (`jobs\<JOB>.lua`, built on `vg\roles.lua`). Caps count: once gear
  haste is at 25% or damage taken at -50%, a slot goes to something else.
- Builds idle, resting, damage taken, fighting (default, hybrid, accuracy), one set per
  weaponskill your weapons can use, precast, interim and midcast by skill and spell, shooting,
  and pet sets for pet jobs.
- Rebuilds by itself on job change, level up, a weapon change, or gear moving into or out of
  your wardrobes.
- Waits after every zone: nothing is equipped, loaded or sent to the game until you have been
  standing in the zone for 10 seconds (`/vg wait <seconds>` changes it).

## Your own profiles

Profiles in `config\addons\luashitacast\` keep working, `/lac` included. A profile with sets of
its own wins: vanagear's sets go under it only if you turn that on with `/vg underlay on`.
Profiles the launcher made from GearSwap files, and profiles whose sets are all empty, get the
built sets underneath automatically, so anything they leave out is filled.

## Commands

`/vg help` lists the ones that mean something for your job. `/gs c <word>` does the same as
`/vg <word>`, and Motenten's mode commands and F9-F12 keys (`/vg keys mote`) work for players
coming from GearSwap.

## Your own commands and sets

- `/vg add <word> <command>; <command>` makes `/vg <word>` run those lines in order (`wait 2`
  pauses). `/vg add all <word> ...` makes it for every job. The window's **Your commands and
  sets** button does the same, and **Open folder** opens `config\addons\vanagear\`, where they
  are kept as `commands\<JOB>.lua` and `sets\<JOB>.lua` to edit by hand (`/vg mine reload` after).
- `/vg save <name>` keeps what you are wearing as a set. `/vg wear <name>` keeps it on over the
  built sets until `/vg wear off`; `/vg look <name>` locks it as your look (lockstyle) while the
  built sets go on swapping, `/vg look off` unlocks.

## Beastmaster

- `/vg call <jug>` puts the jug in the ammo slot and uses Call Beast; `/vg loyal <jug>` does the
  same with Bestial Loyalty. A part of the name is enough (`/vg call fizzy`); with no jug it
  calls the last one. `/vg jugs` lists the ones you carry.
- `/vg reward [food]` puts the pet food in and uses Reward: `alpha` ... `theta`, `roborant`,
  `poultice`, or `debuff` (Pet Roborant, else Pet Poultice). With no food it uses the best
  biscuit for your level. `/vg foods` lists them.

## Data

`data\` is generated from the Vanadreams server's tables by `tools\gearstats\build.py`; rebuild
it when the server's gear changes. `tests\run.py` builds sets off-game from a stand-in bag.
