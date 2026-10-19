"""Off-game check of charcapture for Windower.

    python windower/tests/test_charcapture.py [path to a snapshot from the Ashita charcapture]

Runs the addon under LuaJIT (Windower 4's Lua) with a stand-in for Windower's API, feeds it packets laid out
the way the server's own structs lay them out (src/map/packets/s2c), takes a snapshot, and checks that

  * every number lands where the importer (tools/vanadreams/import_capture.py) reads it, and
  * the file has the same shape as one the Ashita charcapture wrote (every key the Ashita file has, at
    every level, is there too), when such a file is given.

Needs: pip install lupa
"""

import json
import os
import struct
import sys
import tempfile

import lupa.luajit21 as luajit

HERE = os.path.dirname(os.path.abspath(__file__))
ADDON = os.path.join(HERE, "..", "addons", "charcapture")


def packet(pid, size, fields):
    """A packet of size bytes (header included) with each (offset, struct format, value) written in."""
    b = bytearray(size)
    b[0], b[1] = pid & 0xFF, ((pid >> 8) & 0x01) | ((size // 4) << 1)
    for off, fmt, val in fields:
        struct.pack_into("<" + fmt, b, off, val)
    return bytes(b)


def packets():
    p = {}
    # 0x061 CLISTATUS: RDM75/WHM37, exp 1234 of 56000, title 951, rank 10 with 305 points, home 231, nation 1
    p[0x061] = packet(0x061, 0x70, [(0x0C, "B", 5), (0x0D, "B", 75), (0x0E, "B", 3), (0x0F, "B", 37),
                                    (0x10, "h", 1234), (0x12, "h", 32000), (0x44, "H", 951), (0x46, "H", 10),
                                    (0x48, "H", 305), (0x4A, "H", 231), (0x50, "B", 1)])
    # 0x01B JOB_INFO: job_lev2[job] at 0x48, job_mastery_levels[job] at 0x6C
    lv = [(0x48 + j, "B", 10 + j) for j in range(1, 23)] + [(0x6C + 5, "B", 7)]
    p[0x01B] = packet(0x01B, 0x88, lv)
    # 0x062: sword (3) 230 not capped, enfeebling (35) 389 capped; fishing (craft 0) level 55 rank 5, capped
    p[0x062] = packet(0x062, 0x100, [(0x80 + 3 * 2, "H", 230), (0x80 + 35 * 2, "H", 389 | 0x8000),
                                     (0xE0, "H", 5 | (55 << 5) | 0x8000)])
    # 0x063 type 5: RDM (5) 1500 CP, 12 JP, 300 spent
    jp = packet(0x063, 0xA0, [(0x04, "H", 5), (0x0C + 5 * 6, "H", 1500), (0x0C + 5 * 6 + 2, "H", 12),
                              (0x0C + 5 * 6 + 4, "H", 300)])
    # 0x063 type 2: 9999 limit points, 17 merits (BLU bonus bits set above them), max 75
    merits = packet(0x063, 0x10, [(0x04, "H", 2), (0x08, "H", 9999), (0x0A, "H", 17 | (3 << 7)), (0x0C, "B", 75)])
    # 0x00A LOGIN: face 13, race 7 at 0x44, head..ranged with their high nibble; size 2 (large), female
    look = [(0x44, "B", 13), (0x45, "B", 7)] + [(0x46 + i * 2, "H", (i + 1) * 0x1000 + 100 + i) for i in range(8)]
    zone = packet(0x00A, 0x104, look + [(0x21, "B", 128 + 4)])
    # 0x08C: two merits, one with upgrades
    mer = packet(0x08C, 0x14, [(0x04, "H", 2), (0x08, "H", 64), (0x0B, "B", 3), (0x0C, "H", 66), (0x0F, "B", 0)])
    # 0x08D: RDM (5) job point 2 at level 10
    jpu = packet(0x08D, 0x10, [(0x04, "H", 2 | (5 << 5)), (0x06, "H", 10 << 10)])
    # 0x056: a quest log block for port 0x0050
    log = packet(0x056, 0x28, [(0x04, "I", 0xDEADBEEF), (0x24, "H", 0x0050)])
    # 0x113: sandoria_cp at 4, login_points at 170, plans packed at 216
    plans = 5 | (6 << 9) | (7 << 18) | (8 << 27) | (9 << 36)
    cur1 = packet(0x113, 0x100, [(4, "i", 123456), (170, "H", 4321), (216, "Q", plans)])
    return p, jp, merits, zone, mer, jpu, log, cur1


LUA_HARNESS = r"""
local addon_dir, out_dir, store = ...
package.path = addon_dir .. '/?.lua;' .. package.path
local handlers = {}
local said = {}
local bags = {}
for c = 0, 16 do bags[c] = { max = 0, enabled = false } end
bags[0] = { max = 80, enabled = true, [1] = { id = 514, count = 1, status = 0, bazaar = 0, extdata = string.rep('\1', 24) },
            [5] = { id = 16480, count = 1, status = 5, bazaar = 0, extdata = string.rep('\0', 24) } }
bags[8] = { max = 80, enabled = true, [3] = { id = 23761, count = 1, status = 5, bazaar = 0, extdata = string.rep('\0', 24) } }
windower = {
    addon_path = out_dir .. '/',
    add_to_chat = function (_, msg) said[#said + 1] = msg end,
    register_event = function (name, fn) handlers[name] = fn end,
    dir_exists = function () return true end,
    create_dir = function () return true end,
    packets = { last_incoming = function (id) return store[id] end },
    ffxi = {
        get_player = function () return { name = 'Testy', id = 1234567, main_job_id = 5, main_job_level = 75, sub_job_id = 3, sub_job_level = 37 } end,
        get_info = function () return { server = 2 } end,
        get_spells = function () return { [1] = true, [2] = false, [57] = true } end,
        get_abilities = function () return { job_abilities = { 98, 35 }, weapon_skills = { 32 }, job_traits = { 1 } } end,
        get_key_items = function () return { 3000, 3500 } end,
        get_items = function (bag, index)
            if bag == nil then return { gil = 98765, equipment = { main = 5, main_bag = 0, head = 3, head_bag = 8, body = 0, body_bag = 0 } } end
            if index ~= nil then return bags[bag][index] or { id = 0 } end
            return bags[bag]
        end,
    },
}
_addon = {}
coroutine.schedule = function (fn) fn() end
package.preload.resources = function () return { servers = { [2] = { id = 2, en = 'Undine' } } } end
dofile(addon_dir .. '/charcapture.lua')
handlers['load']()
return handlers, said
"""


def run(snapshot_from_ashita=None):
    lua = luajit.LuaRuntime(unpack_returned_tuples=True, encoding=None)
    p, jp, merits, zone, mer, jpu, log, cur1 = packets()
    store = lua.table_from(p)
    out = tempfile.mkdtemp()
    os.makedirs(os.path.join(out, "data"))
    handlers, said = lua.execute(LUA_HARNESS.encode(), os.path.abspath(ADDON).encode(), out.encode(), store)
    chunk = handlers[b"incoming chunk"]
    for pid, data in [(0x063, jp), (0x063, merits), (0x00A, zone), (0x08C, mer), (0x08D, jpu), (0x056, log), (0x113, cur1)]:
        chunk(pid, data)
    handlers[b"addon command"]()
    lines = [v.decode("utf-8", "replace") for v in said.values()]
    path = os.path.join(out, "data", "Testy.json")
    assert os.path.exists(path), "no snapshot written: " + " | ".join(lines)
    snap = json.load(open(path, encoding="utf-8"))

    def eq(got, want, what):
        assert got == want, f"{what}: got {got!r}, want {want!r}"

    eq(snap["schema"], 1, "schema")
    eq(snap["captured_on"], "retail (Undine)", "captured_on")
    ch = snap["character"]
    for k, v in {"name": "Testy", "server_id": 1234567, "race": 7, "face": 13, "size": 2, "nation": 1, "rank": 10,
                 "rank_points": 305, "title": 951, "homepoint": 231}.items():
        eq(ch[k], v, "character." + k)
    jobs = snap["jobs"]
    eq((jobs["main"], jobs["main_level"], jobs["sub"], jobs["sub_level"]), (5, 75, 3, 37), "jobs")
    eq(jobs["levels"]["1"], 11, "WAR level")
    eq(jobs["levels"]["22"], 32, "RUN level")
    eq(jobs["master_levels"]["5"], 7, "RDM master level")
    eq(jobs["job_points"]["5"], {"capacity": 1500, "points": 12, "spent": 300}, "RDM job points")
    eq(jobs["merits"], {"points": 17, "max": 75}, "merits")
    eq(jobs["limit_points"], 9999, "limit points")
    eq(jobs["exp"], {"current": 1234, "needed": 32000}, "exp")
    eq(snap["skills"]["combat"]["3"], {"skill": 230, "rank": 0, "capped": False}, "sword")
    eq(snap["skills"]["combat"]["35"], {"skill": 389, "rank": 0, "capped": True}, "enfeebling")
    eq(snap["skills"]["craft"]["0"], {"skill": 55, "rank": 5, "capped": True}, "fishing")
    eq(snap["skills"]["by_job"]["5/3"]["combat"]["35"]["skill"], 389, "by_job reading")
    eq(snap["spells"], [1, 57], "spells")
    eq(snap["abilities"], [512 + 35, 512 + 98], "abilities, numbered as Ashita numbers them")
    eq(snap["weaponskills"], [32], "weaponskills")
    eq(snap["key_items"], [3000, 3500], "key items")
    eq(snap["gil"], 98765, "gil")
    eq(snap["inventory"]["0"][0], {"slot": 1, "id": 514, "count": 1, "flags": 0, "price": 0, "extra": "01" * 24}, "first item")
    eq(snap["inventory"]["9"], [], "a bag never opened is an empty list")
    eq(snap["equipment"], {"0": {"container": 0, "slot": 5, "id": 16480}, "4": {"container": 8, "slot": 3, "id": 23761}}, "equipment")
    eq(snap["look"]["head"], 0x1000 + 100, "head model")
    eq(snap["look"]["ranged"], 0x8000 + 107, "ranged model")
    eq(snap["merit_upgrades"], {"64": 3}, "merit upgrades")
    eq(snap["job_point_upgrades"], {"5": {"2": 10}}, "job point upgrades")
    eq(snap["quest_mission_packets"]["0050"][:8], "efbeadde", "quest log block")
    eq(snap["currencies"]["sandoria_cp"], 123456, "conquest points")
    eq(snap["currencies"]["login_points"], 4321, "login points")
    eq([snap["currencies"][k] for k in ("bloodshed_plans", "umbrage_plans", "ritualistic_plans", "tutelary_plans", "primacy_plans")],
       [5, 6, 7, 8, 9], "plans")

    if snapshot_from_ashita:
        ashita = json.load(open(snapshot_from_ashita, encoding="utf-8"))

        def shape(a, b, where):
            if isinstance(a, dict) and isinstance(b, dict):
                fixed = {"character", "jobs", "skills", "look", "merits", "exp"}
                for k in a:
                    if where.split(".")[-1] in fixed or where == "":
                        assert k in b, f"{where}.{k} is in the Ashita snapshot but not here"
                        shape(a[k], b[k], f"{where}.{k}" if where else k)
            elif isinstance(a, list):
                assert isinstance(b, list), f"{where}: a list there, {type(b).__name__} here"
        shape(ashita, snap, "")
    return lines


if __name__ == "__main__":
    lines = run(sys.argv[1] if len(sys.argv) > 1 else None)
    print("\n".join(lines))
    print("ok")
