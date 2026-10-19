# Vanadreams on Windower

Our client pieces are built for Ashita v4. This folder holds the ones ported to Windower 4, and this page
says where every piece stands. Checked against Windower's own source on 19 Oct 2026: the Lua addons and
libraries ([Windower/Lua](https://github.com/Windower/Lua), `dev` at `fafc5fc`, 17 Oct 2026) and
Windower 5's packages ([Windower/packages](https://github.com/Windower/packages), last changed Dec 2024).
Windower 4's core and launcher are not open source, so what they do is read from the addon API and not
from their code.

Windower 5 is still a pre-release with no change to its packages since Dec 2024, so everything here
targets Windower 4.

## Where each piece stands

| Piece | On Windower | Notes |
|---|---|---|
| charcapture | **Ported** (`addons/charcapture`) | Writes the same snapshot as the Ashita one. The launcher's Capture page lists and sends it, and its "Put charcapture into Windower" button installs it. Most retail players use Windower, so this is the piece that brings people over. |
| Launcher | Works beside Windower | Setup, the game install, updates, Capture and the Windower import page all work for a Windower player. Play starts Ashita; it does not start Windower. Since 0.2.48 (ashita #60) the launcher gives Windows' game registration back after play, so Windower finds the player's other copy again. |
| Connecting to Vanadreams (xiloader 2.2) | Not checked yet | Windower does not log in to private servers by itself and its makers do not support private servers. Whether a Windower 4 profile can be pointed at our xiloader needs a test on a PC with Windower; its launcher is closed source, so the source cannot answer it. |
| Client packs and DAT mods | Can work, needs a launcher step | The packs are XIPivot overlays. XIPivot has a Windower 4 build, so the same overlay folders work there once the launcher installs that build and copies the overlays into its folder. The base game itself is plain game files and works with any loader. |
| VanaGear | Needs a port, and a big one | Built on LuAshitacast (about 28,000 lines). Windower players already have GearSwap, which VanaGear copies the idea of. |
| dreamyah (AH window), crafty, vanafish | Need a port | They read and send packets, which Windower allows, but draw their windows with Ashita's ImGui. Windower has no ImGui, so each window has to be redrawn with Windower's text and image boxes. Windower already ships its own `craft` addon. |
| Vision (FFXI-Vision) | Cannot run there | An Ashita ImGui addon by Drauku with no Windower version; it would be a rewrite, not a port. |
| meritfix | Cannot run there | It patches the game's code in memory. Windower's Lua cannot write game memory; only a Windower plugin (C++) could. |
| vanachatfix | Not needed | It only keeps Ashita's chatfix off on our server. Windower has no chatfix, and our server sends the chat layout the game expects. |
| pktlog | Not ported | Our own tool for recording retail data; Ashita is enough for that. |
| The Vanadreams window look (vdstyle) | Ashita only | It is ImGui. |

## Testing charcapture off-game

```
pip install lupa
python windower/tests/test_charcapture.py path\to\a\snapshot-from-the-ashita-charcapture.json
```

It runs the addon under LuaJIT (Windower 4's Lua) with a stand-in for Windower's API, feeds it packets laid
out the way the server's structs lay them out, and checks each number lands where the importer reads it and
that the file has every key an Ashita snapshot has.
