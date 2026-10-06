# DreamyAH (addon)

Searchable VanaDreams Auction House window. Optional: load it with `/addon load dreamyah`.

- An **AH Search** button appears on screen while the Auction House menu is open. Drag it where you want it;
  the position is remembered. Click it to open or close the search window.
- `/dreamyah` or `/ahsearch` also toggles the window from anywhere.
- `/dreamyah menu` prints the name of the menu currently open. Use it at the AH if the button does not appear.
- `/dreamyah match <text>` sets which menu names count as the Auction House (default `auc`). This is saved
  in `config/addons/dreamyah/`, not in the addon files, so updates never overwrite it.

## Updating without losing settings
Only replace the `addons/dreamyah/` folder. Never ship or overwrite `config/`; that is where every
player's settings live. Do not tell players to edit the addon's `.lua` files for settings.

Talks to the server over packet `0x120`; the protocol lives in `src/protocol.lua` and must match
`server/src/common/dreamy_ah_protocol.h`.

Do not run this together with the `DreamyAH.dll` plugin; both consume packet `0x120`.
