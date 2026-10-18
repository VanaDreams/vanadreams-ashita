# Game-file packs

Packs here ship to every player as part of the client download. Merging one into `main` runs the
**Ship client packs** workflow, which uploads it to the `vanadreams-client` R2 bucket, reads it back, and adds it to
`fairywitch.ca/download/client/<client_ver>/manifest.json` (the old manifest is kept as `manifest.backup-<time>.json`).
The launcher picks it up on the player's next Play or Install/Patch.

- `<client_ver>/<name>.zip` unpacks over the game folder.
- `<client_ver>/<name>.zip.json` (optional) can name `"dest"`, a folder under the install such as
  `{ashita}/polplugins/DATs/<name>`, and `"sha256"` to pin the zip.

Changing a zip replaces its entry; an unchanged zip is skipped. The run needs the `CLOUDFLARE_API_TOKEN` secret.
After a game update moves the client to a new version, move the packs to the new version's folder (rebuild any that
patch the game exe or dll).

Current packs: `30260904_1/vanadreams-hair-01.zip`, pink and platinum hair for the Stylist Moogle (see
`client/hair-pack/README.md`). Keep `xi.stylist.HAIR_COLOURS_OPEN` off on the server until players have it.
