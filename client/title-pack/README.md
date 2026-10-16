# Vanadreams title pack

The title and character select screen: "Welcome to Vanadreams" with the crew (chocobo, moogle,
mandragora, goblin, orc) standing in the scene, plus the Vanadreams title music ("The Lanterns Are Lit").
It ships as a client chunk (`vanadreams-title-NN.zip`, dest `FINAL FANTASY XI`) in the launcher's client
manifest, the same way the modern look pack does.

- `layout.json`: where each crew member stands on the 1024x768 screen (saved from the live mockup).
- `build.py`: draws the picture and writes it into `ROM/119/50.DAT` with xi-tools, then zips the chunk.
  Run it with no arguments for usage.
- `sound/win/music/data/music034.bgw`: the title music, already in the game's own format. It used to be
  the `vanadreams-music` XIPivot add-on; the launcher now removes that add-on once the game folder holds
  this exact file (`Services/RetiredOverlays.cs`), so nobody loses the music in between.
- `texconv.py`: stand-in for DirectXTex texconv off Windows (put it on PATH as `texconv`).
- `xi-tools-hd-only.patch`: lets `xi ui tex si --hd-only NAME --source-dir DIR` import only that texture.

The built `50.DAT` holds Square Enix textures, so it is not committed here; only the scripts, layout
and our own music are. Rebuild from an untouched client's `ROM/119/50.DAT`, bump `PACK` in `build.py`
for every new pack, upload the zip and append its `.chunk.json` entry as the last chunk.

xi-tools: https://github.com/vekien/xi-tools (GPL-3), used as a tool, not bundled.
