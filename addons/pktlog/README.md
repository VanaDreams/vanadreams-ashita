# pktlog

A read-only Ashita v4 research addon for recording the game packets and structured evidence needed
to reproduce retail FFXI behavior on VanaDreams.

It records one set of files per zone under `Ashita\\logs\\pktlog`:

- raw incoming and outgoing packets as hexadecimal;
- entities and their positions/look data;
- widescan entries;
- kills and treasure-pool drops;
- event IDs, parameters, updates, and menu choices;
- items and quantity changes in the player's bags.

## Install

Copy this folder to `Ashita-v4beta\\addons\\pktlog`, then load it in game:

```text
/addon load pktlog
```

The VanaDreams setup normally binds `Ctrl+Alt+F12` to load it and `Ctrl+Alt+F11` to unload it.
It keeps recording across zone changes until unloaded.

## Capture handling

Packet logs can contain character names and detailed gameplay activity. Review captures before
sharing them, keep the original files as evidence, and do not commit generated logs to this
repository.
