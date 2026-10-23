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
- The weights come from the player-written job guides on FFXIclopedia and BG-wiki, by level
  band (1-30, 31-60, 61-75, 76-99), so a level 60 is scored by what the guides say a 60 wants,
  not by what a 99 wants. Each job file names its guides. The support jobs (WHM, BLM, RDM,
  SCH, GEO, SMN, BRD) get no fighting set unless their guide gives one at that level; NIN, PLD
  and RUN are built as tanks.
- Each weaponskill's set starts from the server's own script for it (its stat modifiers, hits
  and kind) and adds what the wiki weaponskill pages say about gear (`vg\wsguide.lua`): crit
  rate on the ones that crit with TP, attack on Fudo, Kaiten, Iron Tempest and Sturmwind,
  accuracy on the ones whose accuracy drops with TP, and so on.
- Builds idle, resting, damage taken, fighting (default, hybrid, accuracy), one set per
  weaponskill your weapons can use, precast, interim and midcast by skill, one set per spell you
  have learned and can cast now (main or support job: Utsusemi, elemental and debuff ninjutsu,
  cures, nukes...; listed under "Your spells" in /vg), shooting, and pet sets for pet jobs.
- Rebuilds by itself, without a word in chat, on job change, level up, a weapon change, and
  every zone (gear you picked up or moved since is counted then). `/vg build` rebuilds now and
  says so.
- Gear you put on yourself from the equipment menu stays on until you zone: a Job Points cape
  isn't swapped back off.
- The fighting set is listed as **Engaged** (a set of yours named Engaged, TP or Melee is the
  same set).
- Waits after every zone: nothing is equipped, loaded or sent to the game until you have been
  standing in the zone for 10 seconds (`/vg wait <seconds>` changes it).

## Your own profiles

Profiles in `config\addons\luashitacast\` keep working, `/lac` included. A profile with sets of
its own runs exactly as LuAshitacast runs it: its weapons swap as written, nothing vanagear built
fills a slot it leaves out, and a piece you put on from the menu is swapped off as usual. The one
difference is the wait after every zone (below), which protects the server. vanagear's sets go
under your profile only if you turn that on with `/vg underlay on`.

Don't load the luashitacast addon as well: vanagear is LuAshitacast, and two of them swap every slot
against each other. If it is loaded, vanagear notices, stops swapping and says so in chat; type
`/addon unload luashitacast` and your profile and `/lac` commands carry on in vanagear.
Profiles the launcher made from GearSwap files, and profiles whose sets are all empty, get the
built sets underneath automatically, so anything they leave out is filled.

To move a profile over to vanagear, press **Copy its sets into vanagear and stop using it** in
the window, or type `/vg takeover`: its sets become your own vanagear sets for that job (Engaged,
precast and the rest under the names vanagear wears them by), and vanagear runs the job from
then on. The profile file is left as it was; `/vg profile on` uses it again.

## Commands

`/vg help` lists the ones that mean something for your job. `/gs c <word>` does the same as
`/vg <word>`, and Motenten's mode commands and F9-F12 keys (`/vg keys mote`) work for players
coming from GearSwap.

## Your support job's gear

Vanadreams lets your main job wear the weapons and armour your support job can wear, through your
own sets. The game's equipment menu and the built sets only use your main job's gear. In the `/vg`
window, every slot's list of your own set also offers your support job's pieces; or use
`/vg set <name> <slot> <item>` (for example `/vg set Axe Main Great Axe` on BLM/WAR), then
`/vg wear <name>`. Weapon skills you get this way cap at 80% of the support job's cap.

## Your trusts

Every trust you have called gets its own tab in the `/vg` window, next to yours. The top of the
tab says who the trust is: its name, its job and support job, its level, and the spells,
abilities and weaponskills it uses (the server tells vanagear; those lines stay out of your chat).

Like your own, a trust has several sets, built from your inventory and wardrobes for its job and
level:

- **Idle** while it stands around, and **Engaged** while it fights. A Paladin or Rune Fencer
  trust's Engaged set is a tank's: damage taken down, HP, defence and enmity.
- **Weaponskill**, and a set for each weaponskill it uses that vanagear knows (Savage Blade...).
- A set for each kind of spell it casts: **Cure**, **Nuke**, **Enfeebling**, **Enhancing**,
  **Dark**, **Divine**, **Song**, **Ninjutsu**, **Blue Magic**, **Geomancy**.
- **Ability** (enmity) for a tank.

Every slot of every set is a list you can change. Under the set list, **Add a set for** offers a
set for anything the trust uses that has none yet (Flash, Sentinel, Cure IV), and **New set for**
takes any name. An action's set goes on over Idle or Engaged while the action goes off: first a
set with the action's own name, else one for its kind, else **Magic** for any spell. Slots it
leaves empty keep the Idle or Engaged piece.

The trust never wears anything and nothing leaves your bags: the server gives the trust the
stats of the pieces, and a weapon's damage when it beats the trust's own, evened out to the
trust's swing speed. A piece you drop, sell or move to storage stops counting; the trust's job
and level must fit the piece, the same as yours (its support job's gear counts too). The server
keeps every set, so a set is only sent again when it changes; **Send all again** sends them all.
`/vg trusts off` stops vanagear sending anything. Without vanagear, `!trustgear <trust>` lists a
trust's sets, `!trustgear <trust> <set>` shows one, `!trustgear <trust> <set> clear` deletes it
and `!trustgear <trust> clear` deletes them all. Your picks are kept in
`config\addons\vanagear\trusts.lua`.

**Choosing what a trust uses.** Each spell, ability and weaponskill in a trust's "Uses" list has a
checkbox. Untick one and that trust never casts or uses it: the server skips it for as long as the
trust is out and remembers the choice for the next time you call it. "other spells" and "other
abilities" switch off the ones a trust picks in the fight (bursts, storms, Indi spells...). Without
vanagear, `!trustuse <trust>` lists what is switched off, `!trustuse <trust> off <action>` and
`on <action>` change it (a name like `Cure IV` or `Flash`, or a kind: `cure`, `nuke`, `enfeebling`,
`enhancing`, `dark`, `divine`, `song`, `ninjutsu`, `ability`, `weaponskill`) and
`!trustuse <trust> reset` puts everything back. The server answers `!trustuse <trust> info` with a
`[VG off]` line that vanagear reads and hides, as it does the `[VG uses]` line.

## Weapons

With `/vg weapons off` (the default) nothing changes Main, Sub or Range: not the built sets, not
your own vanagear sets, not an imported GearSwap file. (A LuAshitacast profile of your own with sets
of its own is the exception: it swaps weapons as it always did.) The only way a weapon changes
is the lists on the Main, Sub and Range lines of the `/vg` window (or `/vg weapon main <item>`):
they start at "Leave it alone", and the weapon picked is worn in every set for that job.

A profile of your own can still put on a weapon for a command of its own (a fishing rod for `/fish`):
it lists the item names in `gVana.WeaponAllow` and vanagear lets those through.

## Your own sets

- `/vg dress` stops the swapping. Put on what you like from the equipment menu, then
  `/vg save <name>` saves it under that name and swapping comes back on. Weapons are left out
  unless `/vg weapons on`.
- Every slot in the `/vg` window is a list: pick a piece and it is yours in that set (marked *).
  **New set** saves what you wear under the typed name. A slot you pick in Idle stays yours
  while you walk and when your HP or MP is low: the Movement, Regen and Refresh pieces vanagear
  would lay over Idle by themselves skip it. `/vg idle speed`, `/vg idle regen`, `/vg idle refresh`
  and `/vg kite` are you asking for them, so they still go on.
- A set of yours named like a built one (`Idle`, `Tp_Default`, `Reward`, `CallBeast`, a
  weaponskill or spell) replaces the slots it names wherever that set goes on. Any other name is a
  set of its own: `/vg wear <name>` (until `/vg wear off`), `/vg with <name> <command>`,
  `/gs equip <name>`, `/vg look <name>` (lockstyle).
- `/vg set <name> <slot> <item>` changes one slot from chat; `/vg sets` lists yours.
- Kept in `config\addons\vanagear\sets\<JOB>.lua`.

## Your own commands

- `/vg add <word> <command>; <command>` makes `/<word>` (and `/vg <word>`) run those lines in
  order; `wait 2` pauses. `/vg add all <word> ...` makes it for every job.
- **Chains.** A line `then <command>` goes once the action on the line before it has been seen
  to start and finish, plus the 3.3 s gap the server wants between links; `burst <command>` goes
  the moment it finishes. The lines it can wait on are `/ws`, `/ja`, `/ma` and `/pet`. A step
  that never starts, or a target that dies or changes, stops the command and says why; `/vg stop`
  stops one by hand. It does not check TP, Ready charges or finishing moves first.
  `/vg add chain /pet "Brain Crush" <t>; then /pet "Tail Blow" <t>; then /ws "Cloudsplitter" <t>; burst /pet "Fireball" <t>`
- The **How to** tab of the window explains the lines, chains, your own sets in a command, and
  what a LuAshitacast profile in Lua can do on top (the hooks, `gFunc`, `gData`).
- `/vg with <set> <command>` puts a set on, sends the command and keeps the set on until the
  action is done. Example: `/vg dress`, equip the jug, `/vg save lizard`, then
  `/vg add lizard /vg with lizard /ja "Bestial Loyalty" <me>` and type `/lizard`.
- The **Your commands** tab of the window walks you through making one (a word, its lines, which
  jobs), has examples to start from, and runs, changes and deletes the ones you have; **Open folder** opens
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
