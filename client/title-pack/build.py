r"""Build the Vanadreams title pack: the title screen with the crew, and the title music.

    python build.py <vanilla 50.DAT> <crew folder> <xi-tools folder> <out folder>

<vanilla 50.DAT> is ROM/119/50.DAT from an untouched client (the title screen's art and layout).
<crew folder> holds chocobo.png, moogle.png, mandragora.png, goblin2.png and orc.png, transparent
cut-outs of the crew (Art Factory Drafts\Vanadreams: VD-CHOCO-001, VD-MOOGLE-001, VD-EMOTE-005,
VD-BEAST-001, VD-BEAST-006). layout.json beside this script says where each one stands on the
1024x768 title screen; it is what the live mockup page saves.

Writes <out>/vanadreams-title-NN.zip (ROM/119/50.DAT and sound/win/music/data/music034.bgw, for the
"FINAL FANTASY XI" folder) and its manifest entry <out>/vanadreams-title-NN.chunk.json.

The title file is changed with xi-tools (github.com/vekien/xi-tools):
  - the crew and the welcome are drawn into one 1024x768 picture, stored as the ex1us texture at
    1024x1024 (xi ui tex si --hd-only ex1us) and shown by ex1us sprite 0 over the whole screen;
  - the other expansion banners (ex1us 1-4, ex2us 0-4, ex5us 0) and the Final Fantasy XI logo
    (titlwin 0 and 3) are hidden.
Every other texture in the file stays byte for byte as it was; the script checks that.

xi-tools turns PNG into DDS with DirectXTex's texconv on Windows. Elsewhere, put texconv.py from
this folder on PATH as `texconv`: it packs DXT3 alpha itself, since Pillow's own DXT3 alpha is wrong.

The music needs no conversion: music034.bgw is already the game's own format (ADPCM, 44.1 kHz
stereo, looped), and `xi audio info` / `xi audio decode` read it. To replace the track, encode a
new wav with xi-tools' import_music (xi.audio.xi_encode) as music id 34.
"""

import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
W, H = 1024, 768
PACK = 1  # bump for every rebuilt pack, so the launcher sees a new chunk
MUSIC = os.path.join(HERE, "sound", "win", "music", "data", "music034.bgw")

SERIF = ["georgiab.ttf", "LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf"]
SANS = ["segoeuib.ttf", "DejaVuSans-Bold.ttf", "arialbd.ttf"]
FONT_DIRS = [r"C:\Windows\Fonts", "/usr/share/fonts/truetype/liberation", "/usr/share/fonts/truetype/dejavu"]


def font(names, size):
    for d in FONT_DIRS:
        for n in names:
            p = os.path.join(d, n)
            if os.path.exists(p):
                return ImageFont.truetype(p, size)
    sys.exit("no font found for " + ", ".join(names))


def paste(canvas, im, x, y):
    layer = Image.new("RGBA", (W + 2000, H + 2000), (0, 0, 0, 0))
    layer.paste(im, (x + 1000, y + 1000))
    return Image.alpha_composite(canvas, layer.crop((1000, 1000, W + 1000, H + 1000)))


def overlay(crew_dir):
    layout = json.load(open(os.path.join(HERE, "layout.json")))
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for c in layout["crew"]:
        if c.get("hidden"):
            continue
        im = Image.open(os.path.join(crew_dir, c["id"] + ".png")).convert("RGBA")
        im = im.crop(im.getbbox()).resize((round(c["w"]), round(c["h"])), Image.LANCZOS)
        if c.get("flip"):
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
        x, y = round(c["x"]), round(c["y"])
        shadow = Image.new("RGBA", im.size, (0, 0, 0, 0))
        shadow.putalpha(im.getchannel("A").point(lambda v: int(v * 0.55)))
        ov = Image.alpha_composite(ov, paste(Image.new("RGBA", (W, H)), shadow, x, y + 5).filter(ImageFilter.GaussianBlur(5)))
        ov = paste(ov, im, x, y)
    # the welcome, in the launcher's colours: gold small caps over cream Georgia
    title, eyebrow = font(SERIF, 86), font(SANS, 16)
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).text((512, 128), "Vanadreams", font=title, fill=(241, 205, 120, 120), anchor="mm")
    ov = Image.alpha_composite(glow.filter(ImageFilter.GaussianBlur(14)), ov)
    d = ImageDraw.Draw(ov)
    for text, f, fill, y in (("W E L C O M E   T O", eyebrow, (241, 205, 120, 255), 66),
                             ("Vanadreams", title, (255, 246, 223, 255), 128)):
        d.text((512, y + 3), text, font=f, fill=(0, 0, 0, 230), anchor="mm")
        d.text((512, y), text, font=f, fill=fill, anchor="mm")
    return ov


def textures(dat, xi):
    sys.path.insert(0, os.path.join(xi, "src"))
    from xi.ui.xi_core import parse_textures
    return {t.name: bytes(t.pixels) for t in parse_textures(open(dat, "rb").read())}


def run(xi, *args):
    subprocess.run(["uv", "run", "xi", *args], cwd=xi, check=True)


def main():
    if len(sys.argv) != 5:
        sys.exit(__doc__)
    vanilla, crew_dir, xi, out = sys.argv[1:]
    os.makedirs(out, exist_ok=True)
    work = tempfile.mkdtemp(prefix="vd-title-")
    try:
        root = os.path.join(work, "ffxi")
        dat = os.path.join(root, "ROM", "119", "50.DAT")
        os.makedirs(os.path.dirname(dat))
        shutil.copyfile(vanilla, dat)
        os.environ["FFXI_DIR"] = root

        # ex1us at 1024x1024: the screen-sized picture in the top 768 rows. The game draws UI alpha at
        # half scale (0x80 is opaque), so the picture's alpha is scaled to match the retail textures.
        pics = os.path.join(work, "png")
        os.makedirs(pics)
        tex = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
        tex.paste(overlay(crew_dir), (0, 0))
        r, g, b, a = tex.split()
        Image.merge("RGBA", (r, g, b, a.point(lambda v: round(v * 136 / 255)))).save(os.path.join(pics, "ex1us.png"))
        run(xi, "ui", "tex", "si", dat, "--hd-only", "ex1us", "--source-dir", pics)

        sprite = lambda *a: run(xi, "title", "sprite", dat, *a)
        sprite("--owner", "ex1us", "--index", "0", "--src-xy", "0,0", "--src-wh", "1024,768", "--dest-tl", "0,0", "--dest-br", "1024,768")
        for owner, n in (("ex1us", range(1, 5)), ("ex2us", range(5)), ("ex5us", range(1))):
            for i in n:
                sprite("--owner", owner, "--index", str(i), "--hide")
        for i in (0, 3):
            sprite("--owner", "titlwin", "--index", str(i), "--hide")

        before, after = textures(vanilla, xi), textures(dat, xi)
        changed = sorted(n for n in before if before[n] != after.get(n))
        if changed != ["ex1us"]:
            sys.exit("textures changed besides ex1us: " + ", ".join(changed))

        name = "vanadreams-title-%02d.zip" % PACK
        zpath = os.path.join(out, name)
        files = [("ROM/119/50.DAT", dat), ("sound/win/music/data/music034.bgw", MUSIC)]
        with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            for rel, full in files:
                info = zipfile.ZipInfo(rel, date_time=(2026, 10, 16, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                z.writestr(info, open(full, "rb").read())
        blob = open(zpath, "rb").read()
        entry = {"name": name, "size": len(blob), "sha256": hashlib.sha256(blob).hexdigest(), "files": len(files),
                 "unpacked": sum(os.path.getsize(f) for _, f in files), "dest": "FINAL FANTASY XI"}
        json.dump(entry, open(os.path.join(out, name.replace(".zip", ".chunk.json")), "w"))
        print(json.dumps(entry))
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
