# vanagear — gear sets built from your own bags

Status: design, nothing built. 10 Oct 2026.

## What a player gets

They tick **vanagear** in the launcher, log in, and type `/vg`. A window opens with a full set of
gear sets for the job they are on, chosen from what they actually carry, each piece with
the reason it won, and the list of commands that mean something for that job. Change job, level
up, or put a new piece in a wardrobe, and the sets rebuild themselves.

## What it is built on

LuAshitacast 2.13 is folded into vanagear: one addon, one install. LuAshitacast is MIT
licensed (Copyright (c) 2021 ThornyFFXI), which allows this as long as the notice goes with it.
It already does the hard part, putting precast and midcast gear on at the right packet, so its
code is carried over as it is and vanagear adds what it never had: deciding what goes in the
sets.

**Credit.** The LuAshitacast files keep their own code and get a header line naming ThornyFFXI
and the LuAshitacast repository. Its licence ships as `addons\vanagear\LICENSE-LuAshitacast`,
CREDITS.md gets a LuAshitacast section, and the README and the `/vg` window both say vanagear is
built on LuAshitacast by ThornyFFXI.

Folding it in removes the bridge between two addons. Everything runs in one Lua state: the
builder hands sets straight to the swapper, so there is no sets file, no `/lac reload` and no
`/lac fwd`. `/vg` is caught by the addon's own command event, so no `/alias` is ever registered.

**Existing profiles keep working.** vanagear reads the same profile folder,
`config\addons\luashitacast\<Name>_<id>\<JOB>.lua`, and keeps `gFunc`, `gData`, the profile
handlers and the `/lac` commands as they are, so Ferrin's BST.lua runs unchanged. A job with no
profile gets the built sets and the stock handlers. A job with a profile gets the built sets
underneath it, and a hand-made set of the same name always wins. A profile file is never
written over.

**GearSwap imports carry over.** The launcher's From Windower page already turns a player's
GearSwap files into LuAshitacast profiles (`launcher\...\Services\GearSwapImport.cs`, launcher
0.2.28): it writes `<Character>_<JOB>.lua` into `config\addons\luashitacast`, keeps GearSwap's set
names (`precast.FC`, `precast.WS.Savage Blade`, `midcast.Cure`, `midcast.Enhancing Magic`,
`engaged`, `idle`, `resting`), and leaves the rules behind, along with slots GearSwap's libraries
filled in at run time, such as Mote's elemental gorget. vanagear loads those profiles as the
player's own sets and reads the GearSwap names as its own (`precast.FC` is Precast,
`midcast.Cure` is Cure, `engaged` is Engaged, `precast.WS.<name>` is that weaponskill's set), so
their sets win where they made one. What the import could not bring, vanagear supplies: the
rules come from vanagear's handlers, the left-out slots are filled from vanagear's own pick for
that set, and every set the GearSwap file never had is built. A Windower player arrives with
their gear as they had it, and the gaps in it are already filled. The page's closing line, "LuAshitacast
reads these profiles", changes to name vanagear.

**vanagear replaces luashitacast; the two never load together.** Both answer `/lac` and both
swap gear. In the catalogue, luashitacast's entry names vanagear in its `conflicts`, so while
vanagear is ticked the launcher keeps luashitacast out of the startup script (the same way
macrofix gives way to XIUI), and vanagear's description says it replaces luashitacast. A
startup script line loading luashitacast by hand, like the one in Ferrin's, has to come out
when vanagear goes in.

**Nothing touches gear while a zone is loading.** Ferrin, 10 Oct 2026: LuAshitacast crashes the
server if it loads right away; it needs a good delay after zones. vanagear watches for the
zone-in packet, waits until the character is standing in the new zone, then waits 10 more
seconds (a setting) before it does anything: loading a profile, reading the bags, building sets,
or equipping. A rebuild that comes due during a zone is held until that wait is over. The
`/wait 5` and `/addon load luashitacast` lines at the end of the startup script come out.

**Upstream.** vanagear starts from LuAshitacast 2.13. When ThornyFFXI ships a fix, it is merged
in by hand.

## Where the stats come from

The game client knows an item's name, level, jobs, slots and races, but not its stats: those are
only in the description text. The server is the authority on what a piece actually does here, so
the stats ship with the addon:

- `data\items.lua`, generated from the Vanadreams server's `item_equipment`, `item_mods`,
  `item_mods_pet` and `item_latents`, with the level 99 gear stats file applied. Equippable items
  only, keyed by item id, stats as the server's mod ids.
- `data\augments.lua`, from the server's `augments.sql`, so an augmented Artio's Mantle or Taeon
  piece is scored on its real augments. The augment bytes are read from the item in the bag.
- `data\weaponskills.lua`, from `scripts\actions\weaponskills\*.lua`: each weaponskill's stat
  modifiers (STR 70%, DEX 50% ...), hit count, and whether it is physical or magical, with the
  server's Adoulin setting applied.

A script in `tools\gearstats\` rebuilds all three from the `vanadreams` checkout. When server
gear changes, the data is rebuilt, the version moves, and the launcher's start-up update carries
it to every installed copy.

## Reading the bags

The equippable bags only: Inventory and Wardrobes 1 to 8, the same list LuAshitacast equips
from. A piece is a candidate for a slot when the player's job, level, race and the slot all
allow it. Two of the same ring count as two. Mog Safe, Storage, Locker and Satchel are read too,
but only to say "you own Ferine Necklace in your Mog Safe: it would go in PetTP" — never put in a
set, since LuAshitacast cannot equip from there.

## Scoring

Each job has a weights file, `jobs\<JOB>.lua`, readable and editable, one table per mode:

```lua
Engaged = { stats = { ACC = 1.0, STORETP = 1.5, DOUBLE_ATTACK = 2.0, TRIPLE_ATTACK = 3.0,
                      HASTE_GEAR = 2.5, DUAL_WIELD = 1.0, DMG = 0.6 },
            caps  = { HASTE_GEAR = 2500, DMG = -5000 } },
```

The weights come from an FFXIAH gear guide for each job, read the way the BST sets were read
from the Beastmaster Compendium: what each of the guide's sets values becomes that mode's
weights, the pieces the guide names win their set when the player owns them, and the abilities
the guide says how to use become that job's commands. Ferrin's own pick of guide where he names
one, otherwise the top-rated guide for the job.

The sets are built slot by slot: fill every slot with its best piece, then go round again trying
each candidate in each slot with the other slots held, keeping any swap that raises the total,
until a round changes nothing. Caps are why it goes round: once gear haste is at its 25%, a
haste piece is worth nothing and a slot moves to something else. The same goes for damage taken
(-50%) and fast cast (80%).

Weapons are held: Main, Sub and Range stay as worn, because swapping them costs TP. `/vg
weapons on` lets the builder choose them, for players who want it.

### The cast in four steps

Casters carry different gear at each step of a spell, and LuAshitacast has a hook for each.
vanagear builds a set for every step:

1. **Precast**, when the spell is sent: fast cast, with `Cure_Precast`, `Enhancing_Precast`
   and `Stoneskin_Precast` laid over it for the spells that take cast-time gear of their own.
2. **Interim**, the gap while the spell is still casting: damage taken and spell interruption,
   the same staged midcast the 21 starter profiles already have (`/vg stage`).
3. **Midcast**, which lands just before the spell resolves: first the skill set, then the
   spell family on top of it.
4. **After the cast**, LuAshitacast's default handler puts Idle or Engaged back by itself.

The set names and their layering follow the all-jobs retail profiles at
github.com/GetAwayCoxn/Luashitacast-Profiles, so a player who has used community profiles
sees names they know. Midcast by skill, then spell:

| Skill | Sets, laid on in this order |
|---|---|
| Healing | `Cure`, then `Self_Cure` on yourself, `Cursna` |
| Enhancing | `Enhancing`, then `Self_Enhancing`, `Phalanx`, `Stoneskin`, `Regen`, `Refresh`, `Self_Refresh` |
| Enfeebling | `Enfeebling`, then `Macc` in accuracy mode |
| Elemental | `Nuke`, then `NukeACC`, `Burst` with `/vg burst`, `Helix`; Hachirin-no-Obi when the weather or day matches the spell |
| Dark | `Macc`, then `Drain` for Drain and Aspir |
| Ranged | `Preshot`, `Midshot` |

From the same profiles: `Idle_Regen` and `Idle_Refresh` come on by themselves when HP or MP is
low, `Dt` when HP falls below a set point, `Movement` while running, `Town` gear in towns, and
TH gear with `/vg th`. Their way of registering 32 aliases at load is not taken: that is the
same thing that hung Ferrin's login on 28 Sept.

### The modes each job gets

| Mode | Every job | Added for |
|---|---|---|
| Idle | damage taken, Refresh or Regen, movement speed | |
| Resting | HP and MP recovered while healing | jobs with MP |
| Engaged | accuracy, Store TP, multi-attack, haste, crit | Dual Wield for NIN, DNC, THF, BLU and subjobs with it |
| Engaged DT | Engaged with damage taken weighted up | tanks: PLD, RUN, NIN |
| WS, one per weaponskill | the weaponskill's own stats from the server, attack, accuracy, WS damage; magic attack for the magical ones | every weaponskill the held weapon can use |
| Precast | fast cast | jobs that cast; song cast time for BRD, cure cast time for WHM |
| Midcast by skill | Cure (potency, MND, healing skill), Enhancing (skill, duration), Enfeebling (magic accuracy, skill, MND or INT), Elemental (magic attack, INT, magic accuracy), Burst (magic burst damage), Dark, Divine, Ninjutsu, Songs, Blue physical and magical, Geomancy | the jobs with that skill |
| Pet modes | pet accuracy, attack, haste, damage taken, pet magic | BST, SMN (blood pact delay and damage, perpetuation), PUP, DRG |
| Job abilities | the gear that enhances an ability (Berserk duration, Reward HP, Chakra) | phase 2, one table row per ability |

## Commands

Every command is `/vg <word>`. `/vg` alone opens the window; `/vg help` prints the list for
the job you are on and nothing else. What the list holds:

| Command | Shown to |
|---|---|
| `/vg` | everyone: opens the window, sets by mode, each slot with the piece and why |
| `/vg build` | everyone: rebuild now (it also happens on job change, level up and new gear) |
| `/vg dt` | everyone: damage taken over whatever else is on |
| `/vg idle regen` · `refresh` · `speed` | everyone; `refresh` only for jobs with MP |
| `/vg tp normal` · `acc` · `hybrid` | jobs that melee |
| `/vg why Engaged Ring1` | everyone: the score of the winner and the three behind it |
| `/vg lock Neck` · `/vg unlock Neck` | everyone: keep a slot as you have it |
| `/vg weapons on` · `off` | everyone |
| `/vg burst` | BLM, RDM, SCH, GEO, NIN |
| `/vg stage` | casters: precast, interim, then midcast just before the cast lands |
| `/vg pet tp` · `dt` · `acc` | BST, SMN, PUP, DRG |
| `/vg upgrades` | everyone: pieces sitting in storage that would win a slot |

Ferrin's BST commands are the model for a second kind: commands that exist because of what
the player carries. The bags decide which of these appear:

| Command | Shown when the bags hold |
|---|---|
| `/vg warp`, `/vg xp`, `/vg cp`, `/vg echad` ... | that ring: equip, wait out its delay, use it, give the slot back |
| `/vg fish on` · `off`, `/vg bait 1` | a fishing rod; the baits listed are the ones carried |
| `/vg craft alchemy` ... | crafting gear for that craft |
| `/vg lizard`, `/vg crab` ... | BST with that broth: equip it and call with Bestial Loyalty only |
| `/vg reward` | BST with pet food: the food picked by the pet's HP |

## For players coming from Windower

Ferrin, 10 Oct 2026: "lets look at making the windower people happy." Besides keeping their
imported sets (above), vanagear answers the commands and habits a GearSwap player already has
in their fingers:

| They type or press | vanagear does |
|---|---|
| `/gs c <word>` (`//gs c` in Windower) | the same as `/vg <word>`, so their macros' `gs c` lines keep working |
| `/gs equip sets.engaged`, `/gs equip idle` | puts that set on |
| `/gs validate` | lists set pieces that are not in an equippable bag |
| `/gs export` | saves what is worn as a set, the way `/lac addset` does |
| `/gs reload` | rebuilds the sets |
| Mote's modes: OffenseMode, HybridMode, IdleMode, CastingMode, WeaponskillMode, Kiting | read as vanagear's modes: `/gs c cycle OffenseMode` is `/vg tp`, IdleMode is `/vg idle`, CastingMode is `/vg nuke`, Kiting is movement gear |
| Mote's F9 to F12 keys | the same keys, bound to the matching `/vg` commands, when the player turns on `/vg keys mote` |

The keys are off until the player asks: binding them is several commands sent to the game, and
that is exactly what the zone wait exists to keep away from login. When turned on, they are bound
once, after the zone wait. The `/gs` word is caught by vanagear's own command event, the same as
`/vg`.

## What ships in phase 1

The addon, the data files and the tool that builds them, weights for all 22 jobs, the modes in
the table above except job abilities, the window, and the commands. The catalogue entry is added
to `catalog.json` and the launcher's bundled `assets\catalog.json` in the same commit, as id
`vanagear`, kind addon, repo-folder `addons/vanagear`, `copy-to-addons`, loaded with
`/addon load vanagear`, described as replacing luashitacast.

## Decided by default

- **The name is vanagear.**
- **LuAshitacast folded in, credited, replacing the separate addon** (Ferrin, 10 Oct 2026).
- **Weapons are held unless the player turns it on.**
- **Existing profiles are never overwritten.** A hand-made set always wins.
