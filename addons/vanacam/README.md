# VanaCam

VanaCam sets the third-person camera distance in both normal play and engaged combat. It starts at 15 and remembers your last setting per character.

| Command | Effect |
| --- | --- |
| `/vc` or `/vc cycle` | Cycle through 8, 15, and 20. |
| `/vc 20` or `/vc set 20` | Set a distance from 4 to 40. |
| `/vc status` | Show the saved distance and whether the camera patch is active. |
| `/vc help` | List the commands in game. |

The preset `0` is intentionally absent. Camera distance zero is not the game's first-person switch; this patch keeps the normal three-unit zoom-wheel range by calculating a minimum from the selected maximum, so zero would create a negative minimum.

For a manual test, copy the `vanacam` folder into your Ashita v4 `addons` folder. In a running game, use `/addon load vanacam`, then `/vc status` and `/vc 20`. To load it at each launch, add `/addon load vanacam` to your Ashita startup script. Unloading the addon restores the original camera code when it still owns the changed operands.

Camera memory patches come from [XICamera](https://github.com/Hokuten85/XICamera), `xicamera_core.lua` at upstream commit `24fb527f1f76964acf61723a5a6781b2b9be2aef`. Its BSD license is included in `LICENSE.xicamera.txt`. VanaCam's commands and launcher integration are maintained by VanaDreams. The upstream signatures were documented against the May 2026 retail client; check `/vc status` on each newer client before treating it as working. If any required normal or battle patch group is missing, VanaCam unloads the patches and reports that it is unavailable.
