# vanafish

A fishing bot written for Vanadreams, as an Ashita v4 addon. It runs on any server. The fight numbers it reads are the ones every LandSandBoat server sends, so it behaves the same elsewhere; how a given server judges the fight is that server's business.

## Install

Copy this folder to `Ashita-v4beta\addons\vanafish\`, then in game:

```
/addon load vanafish
```

or add that line to your startup script. The Vanadreams Launcher does both from its Addons page.

## Use

Stand at a fishing spot with a rod in the ranged slot and bait in the ammo slot, then:

```
/vanafish start
```

`/vanafish` opens and closes the window, `/vanafish stop` stops, `/vanafish status` prints the counters.

## What it does

1. Casts with `/fish`.
2. Lets the game run the bite timer and waits for the server's bite packet, which carries the fish's stamina, time limit and the value the server uses to check the fight.
3. Reads the bite message and the feeling, and decides catch or release by your rules: small fish, large fish, items, monsters, release when the game says you lack the skill, and an optional share of bites released anyway.
4. Ends the fight at the packet level after a delay you set, inside the fish's own time limit. The server then runs its catch roll exactly as it would for a played gauge.
5. Waits between casts, then casts again.

## It stops itself when

Inventory is down to your chosen free slots, there is no usable replacement rod or bait, you zone, you move, a tell arrives, a cast or minute limit is reached, your skill reaches a target, or nothing has bitten for a run of casts. It can run a command of your choice when it stops, for example `/echo fishing stopped`.

## Rod and bait recovery

**Auto-equip next rod or bait** is on by default. If a rod breaks or bait runs out, VanaFish looks in inventory and Wardrobes 1–4 for fishing gear that can be equipped in the missing slot, equips the preferred replacement, waits for the client to confirm it, then continues. It does not use Satchel, Sack, Case, or Storage because the game cannot equip directly from them.

If there are several choices, add their exact in-game names—best first—to `PREFERENCE` near the top of `vanafish.lua`. Unlisted choices are used after listed ones. Turn the setting off in **Stop when** if you want VanaFish to stop rather than change gear.

## Catch log

With logging on, every result goes to `config\vanafish\catchlog.csv`: time, result, what, bite type, feeling, skill.

## Settings

Kept per character by Ashita's settings library under `config\vanafish\`.
