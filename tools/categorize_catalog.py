"""Assign display sections to the launcher catalog while preserving item details."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
groups = {
    "Launcher & setup": "affinity thirdparty autologin config freemem hideconsole logincmd move winefix",
    "Gear & inventory": "itemwatch blusets find oseem bellhop packer legacyac luashitacast findall dressme equipmon invmon quicksets porter ahcolors ahgo instantah nomad auctioneer",
    "Combat & targeting": "distance tparty recast blumon checker petinfo nokb watchexp pbar pupatt status autojoin blucheck cfhblock debuff fastswap macrofix mobdb sequencer castdelay ttimers statustimers fancytrusts points lootwhore lotomatic",
    "Maps & travel": "mapdot minimap filterscan paranormal allmaps cartographer casper gateway minimapmon peekaboo skeletonkey truesight watchdog scenthound zonename boussole",
    "Interface & camera": "fps pivot xi-vision activemon aspect chamcham cleancs clearcolor clock crosshair drawdistance enternity hideparty hideui imguistyle mipmap namecolors noname renamer sexchange singlerace stepdialog trimspells hardwaremouse tcrossbar thotbar xiui balloon",
    "Chat & communication": "timestamp chatmon filterless links multisend autorespond changecall chatfix chime filters ime instantchat logs onevent stfu tokens hush fancychat simplelog",
    "Crafting": "craftmon synplicity",
    "Screenshots & streaming": "screenshot hideobs",
    "Tools & automation": "repeater actionparse captain",
}

category_by_id = {}
for category, ids in groups.items():
    for item_id in ids.split():
        if item_id in category_by_id:
            raise SystemExit("Duplicate category for " + item_id)
        category_by_id[item_id] = category

primary = root / "catalog.json"
catalog = json.loads(primary.read_text(encoding="utf-8"))
if not any(item["id"] == "vanacam" for item in catalog["items"]):
    catalog["items"].append({
        "id": "vanacam", "name": "VanaCam", "kind": "addon", "maintainer": "Vanadreams",
        "source": {"type": "repo-folder", "repo": "VanaDreams/vanadreams-ashita", "path": "addons/vanacam", "branch": "main"},
        "version": "0.2.0", "install": "copy-to-addons", "load": "/addon load vanacam",
        "docs": "https://github.com/VanaDreams/vanadreams-ashita/tree/main/addons/vanacam",
        "description": "Camera controls in game: close/adventure/far presets, normal and combat distance sliders, close zoom in tight spaces, and automatic screen-aspect matching for widescreen displays. Type /vc ui."
    })

unknown = []
for item in catalog["items"]:
    category = "VanaDreams" if item.get("maintainer", "").lower() == "vanadreams" else category_by_id.get(item["id"])
    if category is None:
        unknown.append(item["id"])
    else:
        item["category"] = category
if unknown:
    raise SystemExit("Uncategorized items: " + ", ".join(unknown))

content = json.dumps(catalog, ensure_ascii=False, indent=2) + "\n"
for target in (primary, root / "launcher/VanadreamsLauncher/assets/catalog.json"):
    target.write_text(content, encoding="utf-8", newline="\n")
