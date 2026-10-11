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

## Your own sets

- `/vg dress` stops the swapping. Put on what you like from the equipment menu, then
  `/vg save <name>` saves it under that name and swapping comes back on. Weapons are left out
  unless `/vg weapons on`.
- Every slot in the `/vg` window is a list: pick a piece and it is yours in that set (marked *).
  **New set** saves what you wear under the typed name.
- A set of yours named like a built one (`Idle`, `Tp_Default`, `Reward`, `CallBeast`, a
  weaponskill or spell) replaces the slots it names wherever that set goes on. Any other name is a
  set of its own: `/vg wear <name>` (until `/vg wear off`), `/vg with <name> <command>`,
  `/gs equip <name>`, `/vg look <name>` (lockstyle).
- `/vg set <name> <slot> <item>` changes one slot from chat; `/vg sets` lists yours.
- Kept in `config\addons\vanagear\sets\<JOB>.lua`.

## Your own commands

- `/vg add <word> <command>; <command>` makes `/<word>` (and `/vg <word>`) run those lines in
  order; `wait 2` pauses. `/vg add all <word> ...` makes it for every job.
- `/vg with <set> <command>` puts a set on, sends the command and keeps the set on until the
  action is done. Example: `/vg dress`, equip the jug, `/vg save lizard`, then
  `/vg add lizard /vg with lizard /ja "Bestial Loyalty" <me>` and type `/lizard`.
- **Your commands** in the window adds, edits, runs and deletes them; **Open folder** opens
  `config\addons\vanagear\` to write them by hand (`/vg mine reload` after).

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
