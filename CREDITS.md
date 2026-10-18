# Credits

## Music

**"The Lanterns Are Lit"** — the launcher's theme. Made for Vanadreams with [SoundBreak](https://soundbreak.ai) on 13 September 2026 and used under SoundBreak's royalty-free licence. It plays quietly while the launcher is open and fades out when the game starts. Turn it off on the Settings page.

No Square Enix audio is shipped or played by anything in this repository. FINAL FANTASY XI is a registered trademark of Square Enix Holdings Co., Ltd.

## Guide pictures

The FFXI client guide shows Square Enix's own pictures of the download page and the installer, taken from their public install page at https://www.playonline.com/ff11us/download/media/install_win.html, so that what a player sees in the guide is what they see on screen. Square Enix owns them. The PlayOnline Viewer and Windows pictures are our own screenshots.

## Code

**vanagear** carries [LuAshitacast](https://github.com/ThornyFFXI/LuAshitacast) 2.14 by ThornyFFXI, used under its MIT licence (Copyright (c) 2021 ThornyFFXI), which ships with it as `addons/vanagear/LICENSE-LuAshitacast`. Its set names and the order sets are laid on for casting follow GetAwayCoxn's [Luashitacast-Profiles](https://github.com/GetAwayCoxn/Luashitacast-Profiles) (MIT, Copyright (c) 2022 GetAwayCoxn), and its Blue Magic lists come from there. Motenten's F9-F12 keys are the ones [Mote-libs](https://github.com/Kinematics/Mote-libs) binds.

The launcher carries [**XIPivot**](https://github.com/HealsCodes/XIPivot) 4.3.001 for Ashita v4 by Renee Koecher (HealsCodes), Copyright © 2019-2022, used under its BSD 3-Clause licence, which ships with it as `docs/pivot/LICENSE.txt`. It loads the modern look pack and the title pack as overlays, so they can be switched off and ordered without touching the game's files.

The launcher's **window switcher** follows the approach of HaxPrime's [XI Focus](https://github.com/HaxPrime/XI-Focus) (MIT licence): finding the game by its window class, naming each slot from the window title, borrowing the foreground window's input to change focus, and saved window layouts. The launcher's code is its own.

## Art

The moon, the stars and the colours are the fairywitch.ca artwork, used with permission of its owners.

## The modern look

The HD faces, zone and gear textures are Ashenbubs' HD texture work for FINAL FANTASY XI.

The post-processing runs on [ReShade](https://reshade.me) by crosire (BSD 3-Clause licence) through crosire's [d3d8to9](https://github.com/crosire/d3d8to9) wrapper (BSD 2-Clause licence). Its effects are MultiLUT by Marty McFly and Otis, AdaptiveSharpen by bacondither, Vignette from SweetFX, and MagicBloom by luluco250. Each licence ships beside them in `bootloader\reshade-licenses`.

The colour grade takes its inspiration from Prabs' Prettypaws Vibrant Vana'diel.

## The maps

**XI-Vision maps** on the Addons page are [Drauku's FFXI-Vision](https://github.com/Drauku/FFXI-Vision), a fork of Caradog's [XI-Vision](https://github.com/Caradog/XI-Vision), the redrawn zone maps. The repo carries no licence, so we do not host or repackage it: the launcher fetches the files straight from Drauku's repo onto the player's PC and loads them through XIPivot. Its `ROM/119/51.DAT` interface file comes from an older client and can crash current ones, so the launcher does not copy it: it lays Vision's own interface textures (arrows, buttons, font, gauge, menu dots) onto the player's own current file on their PC and writes the result into the overlay (see `catalog.json`, `skip` and `patch`). Nothing of Square Enix's is hosted.

**XI View interface** on the Addons page is [KenshiDRK's XiView](https://github.com/KenshiDRK/XiView) (XI View 2.0, widescreen version), the cleaner menu skins, fonts and status and menu icons. Like the maps it has no licence, so it is fetched straight from the author's repo onto the player's PC and loaded through XIPivot; we host nothing.

