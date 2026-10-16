# Vanadreams text pack

Corrections to retail dialog lines the game client shows, shipped as the `vanadreams-text` XIPivot overlay
(`vanadreams-text-NN.zip`, dest `{ashita}/polplugins/DATs/vanadreams-text`) in the launcher's client manifest.
The launcher lists it in pivot.ini once, like the look and title packs; the Install page shows it as "Text fixes".

- `fixes.json`: each correction, by dialog file id, line number, the exact old text and its replacement, and why.
- `build.py`: reads an untouched client, changes only those lines, checks every other line is byte for byte the
  same, and writes the zip and its `.chunk.json`. Run it with no arguments for usage.

To find a line: `explain/zone_NNN.txt` in the project's `cutscenes/xi-explain-all-zones.tgz` names the event and the
line number (`print_msg2 ... <speaker> <line> "..."`). English dialog for zones 0-255 is file id 6420 + zone.

The built DATs hold Square Enix text, so they are not committed here; only the script and the fixes are.
Bump `PACK` in `build.py` for every new pack, upload the zip and append its `.chunk.json` entry as the last chunk.

## Fixes

| Zone | Line | Was | Now |
|---|---|---|---|
| Port San d'Oria (232) | 7421, Avandale's new-character intro | "...at the bottom of the staircast in Northern San d'Oria." | "...at the bottom of the staircase leading to Northern San d'Oria." |

Teilsa stands in Port San d'Oria (F-9), at the foot of the stairs up to Northern San d'Oria; the Japanese line says
exactly that (北サンドリア方面の階段下). The English line had a typo and put her in the wrong city.
