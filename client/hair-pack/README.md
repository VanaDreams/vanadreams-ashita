# Hair colour pack (vanadreams-hair-01)

Pink (face byte 32-47) and platinum blond (48-63) hair for the Stylist Moogle. It was only ever copied by hand onto
Ferrin's test client, so nobody else could see it. This ships it to everyone.

Contents of `vanadreams-hair-01.zip` (261 files, unpacks into the game folder over the originals):

- `FFXiMain.dll` with the per-race face table pointed at the new face ids (the table can't be an XIPivot overlay,
  so this pack is not an overlay and is not listed on the Install page's switches)
- `FTABLE.DAT`, `VTABLE.DAT`, `ROM9/FTABLE9.DAT` and `ROM9/100/*.DAT`: the recoloured face DATs, ids 54445-54700

SHA-256 `daec3d8434bab1dd4411e089c03e80434747eed8369834a8218dc6a2d0883993`, 19,361,778 bytes.

## Shipping

1. Copy the zip to the client download folder for the current client version
   (`fairywitch.ca/download/client/<client_ver>/`).
2. `python tools/clientpack/add_chunk.py <that folder>/manifest.json vanadreams-hair-01.zip`
3. Upload the changed `manifest.json`. The launcher records each chunk by name and hash, so every player gets it on
   their next Install/Patch (Play runs the check).
4. Only after everyone has it, set `xi.stylist.HAIR_COLOURS_OPEN = true` on the server: an unpatched client can't draw
   face bytes 32-63 and may crash.

The patched dll matches the Aug-2026 client the pack was made on. After a game update, rebuild the pack.
