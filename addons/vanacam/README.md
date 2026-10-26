# VanaCam

VanaCam gives players an in-game camera panel. Type `/vc ui` to open it. It starts at distance 15 and remembers each character's choices. The panel offers Close (8), Adventure (15), and Far (20) buttons, a normal-distance slider, and an optional separate combat-distance slider.

VanaCam reads the game window width and height that the launcher passes to Ashita and matches the game's aspect to that screen. This is on by default and can be switched off in the panel. It corrects the horizontal view on widescreen and ultrawide monitors; it is not a separate lens-angle multiplier. If Ashita's bundled `aspect` addon is also loaded, both calculate the same screen aspect.

The chosen distance is the far zoom limit. VanaCam keeps the game's original near zoom limits, so the mouse wheel and the game's close-camera behavior can move the camera forward in narrow passages. It does not make walls transparent or change world draw distance.

| Command | Effect |
| --- | --- |
| `/vc` or `/vc cycle` | Cycle through 8, 15, and 20. |
| `/vc ui` | Open or close the camera panel. |
| `/vc 20` or `/vc set 20` | Set normal distance from 4 to 40. |
| `/vc battle 20` | Give combat its own distance. |
| `/vc link` | Make combat follow normal distance again. |
| `/vc aspect on` or `/vc aspect off` | Toggle matching the view to the screen shape. |
| `/vc status` | Show distances, screen aspect, and patch status. |
| `/vc help` | List the commands in game. |

The preset `0` is intentionally absent. Camera distance zero is not the game's first-person switch. The selected value is the far limit; the near limit stays at the game's original value.

For a manual test, copy the `vanacam` folder into your Ashita v4 `addons` folder. In a running game, use `/addon load vanacam`, then `/vc ui`. To load it at each launch, add `/addon load vanacam` to your Ashita startup script. Unloading the addon restores the original camera code when it still owns the changed operands; it also restores the screen aspect it changed when the game still holds that value.

Camera memory patches come from [XICamera](https://github.com/Hokuten85/XICamera), `xicamera_core.lua` at upstream commit `24fb527f1f76964acf61723a5a6781b2b9be2aef`. Its BSD license is included in `LICENSE.xicamera.txt`. VanaCam's commands and launcher integration are maintained by VanaDreams. The upstream signatures were documented against the May 2026 retail client; check `/vc status` on each newer client before treating it as working. If any required normal or battle patch group is missing, VanaCam unloads the patches and reports that it is unavailable.
