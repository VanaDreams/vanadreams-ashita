# Addons

One folder per addon, laid out the way Ashita v4 expects:

```
addons/
  <name>/
    <name>.lua
```

Each addon's folder is copied whole into `Ashita-v4beta\addons\`.

## The Vanadreams window look

Every addon window here (and in vanatunes and grampsshop) uses `vdstyle.lua`: the launcher's
night-and-gold box, gold buttons for the main action, a gold pointer on the picked row, a moon in
the title bar and a **see-through / solid** switch beside it, remembered per window in
`config\addons\<addon>\vdstyle.txt`.

The one to edit is `_shared/vdstyle.lua`. Each addon folder carries a copy, because the launcher
installs addon folders one by one. After a change:

```
python scripts/sync-vdstyle.py ../vanatunes/vanatunes ../grampsshop/grampsshop
python addons/_shared/test_vdstyle.py
```

then bump each addon's version in `catalog.json` and `launcher/VanadreamsLauncher/assets/catalog.json`.
