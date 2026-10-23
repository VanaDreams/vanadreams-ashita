r"""The Rolanberry hangout home point: one new destination in every home point menu, and a home point
event in Rolanberry Fields (zone 110), for the text pack.

    python homepoint.py <untouched FINAL FANTASY XI folder> [<out folder>]

On its own it builds and proves the edits, prints the report and, given an out folder, writes the changed
ROM files there (an overlay folder laid out as ROM/...). build.py calls build() and puts the files in the
text pack.

What the client holds. Every home point crystal runs the same event script (events 8700-8704, one actor
block per crystal, 335 blocks in 67 zone event DATs). Its destinations are data tables inside the
script: per home point index (zone, #n, fee, free-travel group, expansion, the zone-list position, the
crystal's x / y / z and facing), and a zone list (zone, region) the region menu walks. Every table has
128 slots and 122 are used; the zone list has 67 of its slots used. Two constants bound the loops: 122
and 67. The menu text is in each zone's dialog DAT; destination names come from the client's own
area-name table, so the new line reads "Rolanberry Fields #1".

What this changes, in every one of those blocks:
  * home point slot 122: zone 110, #1, fee 500, zone-list position 67, x / y / z of the hangout crystal.
    Expansion (0, an original area), group (0, always charged), facing and map mark stay 0.
  * zone list slot 67: zone 110, region 11 (Derfland).
  * the loop bounds 122 and 67 become 123 and 68.
And it appends to zone 110's event DAT one copy of a standard home point block, with its message
numbers moved to the same text in zone 110's dialog DAT (lines 0-21 and "You do not have enough gil.").

How it is made safe:
  * every table is found by decoding the script's 0x9D (table read) operations and the code around
    them, never by a fixed offset, and the tables are checked against each other (the zone of every
    home point equals the zone list's entry at its zone-list position);
  * a slot is written only if it holds selector 0x8000 (constant 0, empty), and only if it lies past
    the end of every decoded instruction and inside its own table;
  * new values are appended to the block's constant list, so no existing selector changes meaning;
  * the bound constants are changed in place only after every use of their selectors in the whole
    scene is shown to be a loop bound (`if counter >= bound`) or the loop's exit assignment
    (`counter = bound`); otherwise new constants are appended and only those uses repointed;
  * every untouched block is written back from its original bytes; each patched block is compared
    with the original field by field (only the written slots and the constants may differ);
  * every patched block is decoded again with xi-tools' xi_explain and its listing diffed against the
    original's: only the expected lines may differ, in the expected way.

Needs xi-tools (D:\Workshop\Claude\Output\xi-vision\xi-tools-src\src, or XI_TOOLS_SRC).
"""

import collections
import hashlib
import os
import re
import struct
import sys

XI_TOOLS = os.environ.get("XI_TOOLS_SRC", r"D:\Workshop\Claude\Output\xi-vision\xi-tools-src\src")
if XI_TOOLS not in sys.path:
    sys.path.insert(0, XI_TOOLS)
try:
    from xi.event import xi_event as core           # noqa: E402
    from xi.event import xi_decompile as dc         # noqa: E402
    from xi.event import xi_explain as xe           # noqa: E402
    from xi.dialog import xi_dialog                 # noqa: E402
except ImportError as e:  # pragma: no cover
    sys.exit("homepoint.py needs xi-tools (set XI_TOOLS_SRC to its src folder): %s" % e)

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build import rom_path  # noqa: E402

# ---------------------------------------------------------------------------------------------
# What to add. The server's scripts/globals/homepoint.lua must say the same: [122] = { group = 0,
# fee = 1 (500 gil), dest = { 327.0, 23.62, 588.5, <rot>, ROLANBERRY_FIELDS } }.
# ---------------------------------------------------------------------------------------------
HP_EVENTS = range(8700, 8705)     # the five crystals of a zone
SLOT = 122                        # the new home point index (server homepointData[122])
LIST_SLOT = 67                    # the new zone-list position
ZONE = 110                        # Rolanberry Fields
NUMBER = 1                        # "Rolanberry Fields #1"
FEE = 500                         # gil; server fee = 1 (500 * fee)
REGION = 11                       # Derfland, item 11 + 3 of the region menu (checked below)
X, Y, Z = 327000, 23620, 588500   # the hangout crystal, x / y / z * 1000 (hangout_decor.lua)
SOURCE_ZONE = 245                 # Lower Jeuno: the standard block cloned into zone 110
# 335, not the 333 the first survey counted: Northern San d'Oria (231) and Port San d'Oria (232) each have a
# crystal whose script carries extra events, so its tables sit at odd offsets a 2-byte-step scan missed
EXPECT_BLOCKS, EXPECT_ZONES = 335, 67

REF = 0x8000
BITS = 128                        # the server's home point teleport bits (4 x u32): no index reaches 128


def event_fid(zone):
    return 5820 + zone if zone < 256 else 84735 + zone


def dialog_fid(zone):
    return 6420 + zone if zone < 256 else 85335 + zone


def npc_fid(zone):
    return 6720 + zone if zone < 256 else 86235 + zone


def fail(msg):
    sys.exit("homepoint: " + msg)


def u16(b, p):
    return struct.unpack_from("<H", b, p)[0]


def is_reg(sel):
    return not sel & REF


def s32(v):
    return v - (1 << 32) if v & 0x80000000 else v


# ---------------------------------------------------------------------------------------------
# Decoding one block
# ---------------------------------------------------------------------------------------------
class Block:
    """One home point actor block, decoded: reachable instructions, its 0x9D tables and their roles."""

    def __init__(self, where, actor):
        self.where = where
        self.actor = actor
        self.scene = bytes(actor.scene_data)
        self.refs = list(actor.references)
        sc = self.scene
        ops = set()
        for off in actor.event_offsets:
            blocks, _, _ = dc.walk(sc, off)
            for o in blocks.values():
                ops.update(o)
        self.ops = sorted(ops)
        self.size = {p: core._opcode_size(sc[p], sc[p + 1]) for p in self.ops}
        self.code = bytearray(len(sc))           # 1 where a decoded instruction sits
        for p in self.ops:
            for i in range(self.size[p]):
                self.code[p + i] = 1
        self.nd = []                             # (pos, sub, table, dst, idx) per 0x9D in code order
        for p in self.ops:
            if sc[p] == 0x9D:
                if self.size[p] != 8:
                    fail("%s: 0x9D at +%04x is %d bytes, not 8" % (where, p, self.size[p]))
                self.nd.append((p, sc[p + 1], u16(sc, p + 2), u16(sc, p + 4), u16(sc, p + 6)))
        self.next_op = {self.ops[i]: self.ops[i + 1] for i in range(len(self.ops) - 1)}
        self.tables = sorted({t for _, _, t, _, _ in self.nd})
        # an empty slot is the selector of the block's one zero constant: 8000 in the standard script,
        # 8002 in Northern San d'Oria's longer variant, whose constant list starts with two others
        zeros = [k for k, v in enumerate(self.refs) if v == 0]
        if len(zeros) != 1:
            fail("%s: %d zero constants (want one)" % (where, len(zeros)))
        self.empty = REF | zeros[0]
        self.find_roles()

    def find_roles(self):
        sc, w = self.scene, self.where
        reads = [x for x in self.nd if x[1] == 0]
        by_pos = {x[0]: x for x in reads}

        # maximal runs of 0x9D reads, each the very next instruction, all with the same index register
        runs, cur = [], []
        for x in reads:
            if cur and self.next_op.get(cur[-1][0]) == x[0] and cur[-1][4] == x[4]:
                cur.append(x)
            else:
                if cur:
                    runs.append(cur)
                cur = [x]
        if cur:
            runs.append(cur)
        # zone, #n, fee: the run of three (the cost line, straight into param1-3);
        # x, y, z, facing: the run of four (the same-zone hop)
        triples = {tuple(t[2] for t in r) for r in runs if len(r) == 3 and len({t[2] for t in r}) == 3}
        quads = {tuple(t[2] for t in r) for r in runs if len(r) == 4 and len({t[2] for t in r}) == 4}
        if any(len(r) > 4 for r in runs) or len(triples) != 1 or len(quads) != 1:
            fail("%s: zone/#n/fee reads %s, x/y/z/facing reads %s (want one of each)" % (w, triples, quads))
        self.ZONE, self.NUM, self.FEE = triples.pop()
        self.X, self.Y, self.Z, self.DIR = quads.pop()
        # x / y / z: the hop then steps the player one yalm along the facing, adding to x and z, never to
        # the height; that tells the middle read (y) from the other two
        quad = next(r for r in runs if len(r) == 4)
        adds, p = set(), self.next_op.get(quad[-1][0])
        while p is not None and sc[p] != 0x1B:
            if sc[p] == 0x07:
                adds.add(u16(sc, p + 1))
            p = self.next_op.get(p)
        if not ({quad[0][3], quad[2][3]} <= adds) or quad[1][3] in adds:
            fail("%s: the hop does not step x and z and leave y (adds to %s)" % (w, sorted(adds)))
        # expansion: a read whose result is the bit tested by the next instruction (0x3E bit_branch)
        exp = {x[2] for x in reads if sc[self.next_op.get(x[0], 0)] == 0x3E and u16(sc, self.next_op[x[0]] + 3) == x[3]}
        # zone-list position -> region: a read whose result indexes the very next read
        # (the favourites table also feeds the zone read this way; the per-home-point tables are not regions)
        per_hp = {self.ZONE, self.NUM, self.FEE, self.X, self.Y, self.Z, self.DIR}
        pos_rg = {(x[2], by_pos[self.next_op[x[0]]][2]) for x in reads
                  if self.next_op.get(x[0]) in by_pos and by_pos[self.next_op[x[0]]][4] == x[3]
                  and by_pos[self.next_op[x[0]]][2] not in per_hp}
        if len(exp) != 1 or len(pos_rg) != 1:
            fail("%s: expansion reads %s, position->region reads %s" % (w, exp, pos_rg))
        self.EXP = exp.pop()
        self.POS, self.RG = pos_rg.pop()
        # zone list: the read just before each region read that shares its index (the zone-list walk)
        zl = set()
        after_pos = {self.next_op[x[0]] for x in reads if x[2] == self.POS}
        for x in reads:
            if x[2] != self.RG or x[0] in after_pos:
                continue
            before = [y for y in reads if y[0] < x[0] and y[4] == x[4] and y[2] not in (self.RG, self.POS)]
            if not before:
                fail("%s: region read at +%04x has no zone-list read before it" % (w, x[0]))
            zl.add(before[-1][2])
        if len(zl) != 1:
            fail("%s: zone-list reads %s" % (w, zl))
        self.ZL = zl.pop()
        # group: the same table read twice in a row (destination, then here), other than the zone table
        grp = {x[2] for x in reads if self.next_op.get(x[0]) in by_pos and by_pos[self.next_op[x[0]]][2] == x[2]
               and x[2] != self.ZONE}
        if len(grp) != 1:
            fail("%s: group reads %s" % (w, grp))
        self.GROUP = grp.pop()
        roles = [self.ZONE, self.NUM, self.FEE, self.X, self.Y, self.Z, self.DIR, self.EXP, self.POS, self.RG,
                 self.ZL, self.GROUP]
        if len(set(roles)) != len(roles):
            fail("%s: two roles share a table: %s" % (w, ["%04x" % t for t in roles]))

    def reads_dst_of(self, table):
        return {x[3] for x in self.nd if x[1] == 0 and x[2] == table}

    # ---- tables -------------------------------------------------------------------------
    def capacity(self, t):
        """Slots from t to the next table (or the end of the scene); none of them may be code."""
        nxt = min([u for u in self.tables if u > t] + [len(self.scene)])
        return (nxt - t) // 2

    def sel(self, t, i):
        return u16(self.scene, t + 2 * i)

    def val(self, sel):
        if sel & REF:
            k = sel & 0x7FFF
            if k >= len(self.refs):
                fail("%s: selector %04x points past the constants" % (self.where, sel))
            return self.refs[k]
        return "R%04x" % sel

    def used(self, t):
        n = 0
        while n < self.capacity(t) and self.sel(t, n) != self.empty:
            n += 1
        return n

    def values(self, t, n=None):
        return [self.val(self.sel(t, i)) for i in range(self.capacity(t) if n is None else n)]

    # ---- readers of a constant --------------------------------------------------------
    def owner(self, pos):
        for p in self.ops:
            if p <= pos < p + self.size[p]:
                return p
        return None

    def bound_sites(self, k):
        """Every place selector 0x8000|k sits in the scene, and whether each is a loop bound."""
        s = struct.pack("<H", REF | k)
        hits, at = [], self.scene.find(s)
        while at >= 0:
            hits.append(at)
            at = self.scene.find(s, at + 1)
        bounds, exits, other = [], [], []
        counters = set()
        for h in hits:
            p = self.owner(h)
            if p is not None and self.scene[p] == 0x02 and h == p + 3 and (self.scene[p + 5] & 0x0F) == 3 \
                    and is_reg(u16(self.scene, p + 1)):
                bounds.append(h)
                counters.add(u16(self.scene, p + 1))
        for h in hits:
            if h in bounds:
                continue
            p = self.owner(h)
            if p is not None and self.scene[p] == 0x03 and h == p + 3 and u16(self.scene, p + 1) in counters:
                exits.append(h)
            else:
                other.append((h, p))
        return hits, bounds, exits, other


# ---------------------------------------------------------------------------------------------
# Patching one block
# ---------------------------------------------------------------------------------------------
def patch_block(b, report):
    """Returns (new scene, new refs, proof dict)."""
    w, sc = b.where, bytearray(b.scene)
    refs = list(b.refs)
    hp_tables = [b.ZONE, b.NUM, b.FEE, b.POS, b.X, b.Y, b.Z, b.EXP, b.GROUP, b.DIR]
    n_hp = b.used(b.ZONE)
    n_zl = b.used(b.ZL)
    if n_hp != SLOT or n_zl != LIST_SLOT:
        fail("%s: %d home points and %d listed zones; this build adds slot %d and list slot %d"
             % (w, n_hp, n_zl, SLOT, LIST_SLOT))
    # every per-home-point table has room up to the server's 128 teleport bits, all empty past the last
    # home point (an unread data block may follow a table, so its room is not "up to the next table")
    for t in hp_tables:
        if b.capacity(t) < BITS:
            fail("%s: table %04x holds %d slots" % (w, t, b.capacity(t)))
        if any(b.sel(t, i) != b.empty for i in range(SLOT, BITS)):
            fail("%s: table %04x slots %d-%d are not all empty" % (w, t, SLOT, BITS - 1))
    if b.capacity(b.ZL) <= LIST_SLOT or any(b.sel(b.ZL, i) != b.empty for i in range(LIST_SLOT, b.capacity(b.ZL))):
        fail("%s: zone list %04x has no empty slot %d" % (w, b.ZL, LIST_SLOT))
    if b.capacity(b.RG) <= LIST_SLOT or b.sel(b.RG, LIST_SLOT) != b.empty:
        fail("%s: region table %04x has no empty slot %d" % (w, b.RG, LIST_SLOT))
    # the tables agree with each other
    zones, pos, zl = b.values(b.ZONE, n_hp), b.values(b.POS, n_hp), b.values(b.ZL, n_zl)
    if any(zl[pos[i]] != zones[i] for i in range(n_hp)):
        fail("%s: a home point's zone is not the zone list's entry at its position" % w)
    if len(set(zl)) != n_zl or ZONE in zl or ZONE in zones:
        fail("%s: zone list repeats a zone, or zone %d is already listed" % (w, ZONE))
    if not all(v in (500, 1000) for v in b.values(b.FEE, n_hp)):
        fail("%s: fee table holds something other than 500 / 1000" % w)
    if not all(1 <= v <= 9 for v in b.values(b.NUM, n_hp)):
        fail("%s: #n table holds something outside 1-9" % w)
    if not all(-2000000 < s32(v) < 2000000 for t in (b.X, b.Y, b.Z) for v in b.values(t, n_hp)):
        fail("%s: a coordinate table holds an implausible value" % w)

    # the two bounds
    k_hp = [k for k, v in enumerate(refs) if v == n_hp]
    k_zl = [k for k, v in enumerate(refs) if v == n_zl]
    if len(k_hp) != 1 or len(k_zl) != 1:
        fail("%s: constant %d at %s, %d at %s (want one each)" % (w, n_hp, k_hp, n_zl, k_zl))
    proof = {"block": w, "bounds": {}}
    repoint = {}                  # scene position -> new constant index (fallback)
    in_place = {}                 # constant index -> new value
    for k, old, new in ((k_hp[0], n_hp, n_hp + 1), (k_zl[0], n_zl, n_zl + 1)):
        hits, bounds, exits, other = b.bound_sites(k)
        if not bounds:
            fail("%s: no loop is bounded by constant %d" % (w, old))
        proof["bounds"][old] = {"selector": "%04x" % (REF | k), "loop_bounds": ["+%04x" % (h - 3) for h in bounds],
                                "loop_exits": ["+%04x" % (h - 3) for h in exits],
                                "other_readers": ["%04x in op @%s" % (h, "%04x" % p if p is not None else "data")
                                                  for h, p in other]}
        if not other:
            in_place[k] = new
            proof["bounds"][old]["how"] = "constant changed in place (every reader is a bound or exit)"
        else:
            proof["bounds"][old]["how"] = "new constant appended, bounds and exits repointed"
            for h in bounds + exits:
                repoint[h] = ("new", new)
    for k, v in in_place.items():
        refs[k] = v

    changed_consts = set(in_place)
    appended = {}                 # value -> index of a constant this build appended

    def const(value):
        """An unchanged existing constant with this value, else one appended (once per value)."""
        value &= 0xFFFFFFFF
        for k, v in enumerate(b.refs):
            if v == value and k not in changed_consts:
                return k
        if value not in appended:
            refs.append(value)
            appended[value] = len(refs) - 1
        return appended[value]

    for h, (_, new) in repoint.items():
        struct.pack_into("<H", sc, h, REF | const(new))

    writes = [(b.ZONE, SLOT, ZONE), (b.NUM, SLOT, NUMBER), (b.FEE, SLOT, FEE), (b.POS, SLOT, LIST_SLOT),
              (b.X, SLOT, X), (b.Y, SLOT, Y), (b.Z, SLOT, Z), (b.ZL, LIST_SLOT, ZONE), (b.RG, LIST_SLOT, REGION)]
    written = []
    for t, slot, value in writes:
        at = t + 2 * slot
        if u16(b.scene, at) != b.empty or b.code[at] or b.code[at + 1]:
            fail("%s: slot %d of table %04x (+%04x) is not an empty data slot" % (w, slot, t, at))
        struct.pack_into("<H", sc, at, REF | const(value))
        written.append(at)
    proof["written"] = ["+%04x" % a for a in written]
    proof["repointed"] = ["+%04x" % h for h in repoint]
    proof["appended_constants"] = refs[len(b.refs):]
    proof["tables"] = {"zone": "%04x" % b.ZONE, "number": "%04x" % b.NUM, "fee": "%04x" % b.FEE,
                       "zone_list_position": "%04x" % b.POS, "x": "%04x" % b.X, "y": "%04x" % b.Y,
                       "z": "%04x" % b.Z, "facing": "%04x" % b.DIR, "expansion": "%04x" % b.EXP,
                       "group": "%04x" % b.GROUP, "zone_list": "%04x" % b.ZL, "region": "%04x" % b.RG}
    return bytes(sc), refs, proof, written + list(repoint)


def check_patched(b, scene, refs, touched):
    """Field-by-field: only the touched scene bytes and the bound constants changed; the rest is the original."""
    w = b.where
    if len(scene) != len(b.scene):
        fail("%s: scene length changed" % w)
    diff = [i for i in range(len(scene)) if scene[i] != b.scene[i]]
    allowed = {p + d for p in touched for d in (0, 1)}
    if any(i not in allowed for i in diff):
        fail("%s: scene bytes changed outside the written slots: %s" % (w, diff[:8]))
    for k in range(len(b.refs)):
        if refs[k] != b.refs[k] and b.refs[k] not in (SLOT, LIST_SLOT):
            fail("%s: constant %d changed from %d" % (w, k, b.refs[k]))


# ---------------------------------------------------------------------------------------------
# Re-decoding with xi_explain
# ---------------------------------------------------------------------------------------------
def explain(scene, refs, actor, blobs):
    lst = xe.explain_actor(scene, refs, actor.event_ids, actor.event_offsets, blobs)
    return lst.lines


def render_end(scene, refs, off):
    """Where xi_explain's table rendering (ActorExplainer.table) stops reading, exclusive."""
    pos, n = off, 0
    while pos + 2 <= len(scene) and n < 200:
        v = u16(scene, pos)
        rv = refs[v & 0x7FFF] if v & REF and (v & 0x7FFF) < len(refs) else None
        if (rv is None and v == 0) or rv == 0:
            return pos + 2
        n += 1
        pos += 2
    return pos


MSG_LINE = re.compile(r"^(\s+\+[0-9a-f]{4}\s+\S+\s+(?:sub 0x0[02] [^\d\"]*)?)(\d+)(\s.*\".*)$")


def remap_msg_line(line, msg_map):
    """A listing line of a text instruction (its message number, then the quoted text) with the number
    moved to the zone 110 line; other lines unchanged."""
    m = MSG_LINE.match(line)
    if m and int(m.group(2)) in msg_map:
        return m.group(1) + str(msg_map[int(m.group(2))]) + m.group(3), True
    return line, False


def redecode_diff(b, scene, refs, blobs, proof):
    """xi_explain the original and the patched block; the patched listing must equal the original with only
    the expected substitutions. Returns (lines compared, lines that differ)."""
    old = explain(b.scene, b.refs, b.actor, blobs)
    new = explain(scene, refs, b.actor, blobs)
    ex_old = xe.ActorExplainer(b.scene, b.refs, blobs)
    ex_new = xe.ActorExplainer(scene, refs, blobs)
    # xi_explain shows a table by reading on until a zero entry, so a table that runs straight into a
    # patched one (the menu's register table runs into the zone list) shows more entries too. Every table
    # whose rendering changed must have read through a written slot.
    written = [int(p[1:], 16) for p in proof["written"]]
    renders = []
    for t in b.tables:
        a, c = ex_old.table(t), ex_new.table(t)
        if a != c:
            end = render_end(scene, refs, t)
            if not any(t <= x < end for x in written):
                fail("%s: table %04x reads differently but holds no written slot" % (b.where, t))
            renders.append(("table@%04x %s" % (t, a), "table@%04x %s" % (t, c)))
    site_lines = {}
    for old_v, info in proof["bounds"].items():
        for p in info["loop_bounds"] + info["loop_exits"]:
            site_lines["    " + p] = (old_v, old_v + 1)
    expect = []
    for line in old:
        e = line
        for a, c in renders:
            e = e.replace(a, c)
        key = e[:9]
        if key in site_lines:
            a, c = site_lines[key]
            e2, n = re.subn(r"(?<![\w\[])%d(?![\w\]])" % a, str(c), e)
            if n != 1:
                fail("%s: bound line %r does not show %d once" % (b.where, line, a))
            e = e2
        expect.append(e)
    if expect != new:
        bad = [(i, a, c) for i, (a, c) in enumerate(zip(expect, new)) if a != c][:5]
        fail("%s: re-decode differs from the expected listing (%d vs %d lines): %s"
             % (b.where, len(expect), len(new), bad))
    return len(old), sum(1 for a, c in zip(old, new) if a != c)


# ---------------------------------------------------------------------------------------------
# Dialog
# ---------------------------------------------------------------------------------------------
def dialog_lines(game, zone):
    raw = open(os.path.join(game, *rom_path(game, dialog_fid(zone)).split("/")), "rb").read()
    blobs, _ = xi_dialog.raw_entry_blobs(raw)
    return [bytes(x) for x in blobs]


def message_refs(b):
    """Constant indexes the block uses as dialog line numbers (xi_explain's text operands)."""
    sc, out = b.scene, {}
    for p in b.ops:
        op = sc[p]
        at = None
        if op in xe.TEXT_OPS:
            at = p + (5 if op == 0x2B else 1)
        elif op == 0xD4 and sc[p + 1] in (0x00, 0x02):
            at = p + 2
        if at is not None:
            s = u16(sc, at)
            if s & REF:
                out.setdefault(s & 0x7FFF, []).append(at)
    for k, ats in out.items():
        s = struct.pack("<H", REF | k)
        hits, h = [], sc.find(s)
        while h >= 0:
            hits.append(h)
            h = sc.find(s, h + 1)
        if sorted(hits) != sorted(ats):
            fail("%s: message constant %d is also read as something other than a line number" % (b.where, k))
    return out


# ---------------------------------------------------------------------------------------------
# The whole build
# ---------------------------------------------------------------------------------------------
def build(game, log=print):
    """Returns ({rel ROM path: new bytes}, report dict). Exits on anything it does not recognise."""
    report = {"blocks": [], "files": {}}
    found = collections.OrderedDict()     # rel -> (zone, original bytes, actors, [block indexes])
    for zone in range(0, 300):
        try:
            rel = rom_path(game, event_fid(zone))
        except SystemExit:
            continue
        p = os.path.join(game, *rel.split("/"))
        if not os.path.exists(p):
            continue
        data = open(p, "rb").read()
        try:
            actors = core.parse_raw_actors(data)
        except Exception:
            continue
        hits = [i for i, a in enumerate(actors) if any(e in HP_EVENTS for e in a.event_ids)]
        if hits:
            if core.build_event_dat(actors) != data:
                fail("%s does not round-trip byte for byte; not touching it" % rel)
            found[rel] = (zone, data, actors, hits)
    n_blocks = sum(len(v[3]) for v in found.values())
    if n_blocks != EXPECT_BLOCKS or len(found) != EXPECT_ZONES:
        fail("found %d home point blocks in %d zones, expected %d in %d (a retail update?)"
             % (n_blocks, len(found), EXPECT_BLOCKS, EXPECT_ZONES))

    out = {}
    hashes = collections.Counter()
    source = None
    rounds = {"compared_lines": 0, "differing_lines": 0}
    for rel, (zone, data, actors, hits) in found.items():
        blobs = dialog_lines(game, zone)
        new_actors = list(actors)
        for i in hits:
            a = actors[i]
            b = Block("zone %d %s block %d actor %08X" % (zone, rel, i, a.actor_id), a)
            scene, refs, proof, touched = patch_block(b, report)
            check_patched(b, scene, refs, touched)
            n, d = redecode_diff(b, scene, refs, blobs, proof)
            rounds["compared_lines"] += n
            rounds["differing_lines"] += d
            hashes[hashlib.sha1(b.scene).hexdigest()] += 1
            # serialized with the block's own padding bytes (some retail blocks pad with ff, not 00); the
            # scene keeps its length, so the padding keeps its length too
            na = core.RawActor(a.actor_id, list(a.event_offsets), list(a.event_ids), refs, scene, a.block_pad, b"")
            na.raw_block = core.serialize_actor(na)
            new_actors[i] = na
            proof["redecode"] = {"lines": n, "differ": d}
            report["blocks"].append(proof)
            if zone == SOURCE_ZONE and 8700 in a.event_ids:
                source = (b, scene, refs, blobs)
        blob = core.build_event_dat(new_actors)
        verify_file(rel, data, blob, actors, set(hits), extra=0)
        out[rel] = blob
        report["files"][rel] = {"zone": zone, "blocks": len(hits), "old": len(data), "new": len(blob)}
    common = hashes.most_common(1)[0]
    report["scene_variants"] = len(hashes)
    if source is None or hashlib.sha1(source[0].scene).hexdigest() != common[0]:
        fail("the source block (zone %d, event 8700) is not the standard variant" % SOURCE_ZONE)
    report["standard_variant_blocks"] = common[1]
    report["redecode"] = rounds

    # ---- zone 110 ---------------------------------------------------------------------
    b, scene, refs, src_blobs = source
    rel = rom_path(game, event_fid(ZONE))
    data = open(os.path.join(game, *rel.split("/")), "rb").read()
    actors = core.parse_raw_actors(data)
    if core.build_event_dat(actors) != data:
        fail("%s does not round-trip byte for byte" % rel)
    if any(e in HP_EVENTS for a in actors for e in a.event_ids):
        fail("zone %d already has a home point event" % ZONE)
    lines = dialog_lines(game, ZONE)
    msgs = message_refs(b)
    msg_map, used = {}, {}
    for k in sorted(msgs):
        m = b.refs[k]
        want = src_blobs[m]
        cands = [i for i, s in enumerate(lines) if s == want]
        if not cands:
            fail("zone %d dialog has no line equal to line %d of zone %d" % (ZONE, m, SOURCE_ZONE))
        used[m] = cands
    # where a line repeats, take the copy in the zone's own home point block (the run the unique ones form)
    unique = {m: c[0] for m, c in used.items() if len(c) == 1}
    offs = collections.Counter(c - m for m, c in unique.items())
    base, _ = offs.most_common(1)[0]
    for m, c in used.items():
        if len(c) == 1:
            msg_map[m] = c[0]
        elif m + base in c:
            msg_map[m] = m + base
        else:
            fail("zone %d: line %d of zone %d matches lines %s" % (ZONE, m, SOURCE_ZONE, c))
    # the region menu's choice is region + 3 (none, favourites, current region come first)
    region_menu = [m for m in msg_map if b"[Derfland/" in src_blobs[m]]
    if len(region_menu) != 1:
        fail("cannot find the region menu")
    opts = src_blobs[region_menu[0]].split(b"\x07")
    first = next(i for i, s in enumerate(opts) if s.startswith(b"\x0b"))
    if b"[Derfland/" not in opts[first + 3 + REGION]:
        fail("region %d is not Derfland in the region menu" % REGION)
    clone_refs = list(refs)
    for k in msgs:
        clone_refs[k] = msg_map[b.refs[k]]
    names = core.parse_entity_names(open(os.path.join(game, *rom_path(game, npc_fid(ZONE)).split("/")), "rb").read())
    raw_npc = open(os.path.join(game, *rom_path(game, npc_fid(ZONE)).split("/")), "rb").read()
    npc_ids = [struct.unpack_from("<I", raw_npc, o + 28)[0] for o in range(0, len(raw_npc) // 32 * 32, 32)]
    new_id = max(i for i in npc_ids if (i >> 12) == (0x01000000 | ZONE << 12) >> 12) + 1
    if new_id in npc_ids or any(a.actor_id == new_id for a in actors):
        fail("actor id %08X is taken" % new_id)
    clone = core.RawActor(new_id, list(b.actor.event_offsets), list(b.actor.event_ids), clone_refs, scene, b"",
                          b"", dirty=True)
    blob = core.build_event_dat(list(actors) + [clone])
    verify_file(rel, data, blob, actors, set(), extra=1)
    back = core.parse_raw_actors(blob)[-1]
    if (back.actor_id, back.event_ids, back.event_offsets, back.references, bytes(back.scene_data)) != \
            (new_id, list(b.actor.event_ids), list(b.actor.event_offsets), clone_refs, scene):
        fail("the new zone %d block does not read back as written" % ZONE)
    # re-decode: the clone with zone 110's dialog against the patched source with its own dialog
    tmp = core.RawActor(b.actor.actor_id, list(b.actor.event_offsets), list(b.actor.event_ids), refs, scene, b"", b"")
    old = explain(scene, refs, tmp, src_blobs)
    new = explain(bytes(back.scene_data), list(back.references), back, lines)
    expect, remapped = [], 0
    for line in old:
        line, hit = remap_msg_line(line, msg_map)
        remapped += hit
        expect.append(line)
    if expect != new:
        bad = [(i, a, c) for i, (a, c) in enumerate(zip(expect, new)) if a != c][:5]
        fail("zone %d clone re-decodes differently from its source: %s" % (ZONE, bad))
    out[rel] = blob
    report["files"][rel] = {"zone": ZONE, "blocks": 1, "old": len(data), "new": len(blob), "new_block": True}
    report["zone110"] = {"rel": rel, "actor_id": "%08X" % new_id, "events": [e for e in b.actor.event_ids if e != 0xFFFF],
                         "message_map": {str(m): msg_map[m] for m in sorted(msg_map)},
                         "redecode_lines": len(old),
                         "redecode_differ": sum(1 for a, c in zip(old, new) if a != c),
                         "redecode_message_lines_remapped": remapped,
                         "last_npc": "%08X %s" % (new_id - 1, names.get(new_id - 1, ""))}
    return out, report


def verify_file(rel, old, new, actors, patched, extra):
    """Every block not patched is byte for byte the original; patched blocks keep their header fields."""
    back = core.parse_raw_actors(new)
    if len(back) != len(actors) + extra:
        fail("%s: %d blocks, expected %d" % (rel, len(back), len(actors) + extra))
    for i, a in enumerate(actors):
        if i in patched:
            n = back[i]
            if (n.actor_id, n.event_ids, n.event_offsets) != (a.actor_id, a.event_ids, a.event_offsets):
                fail("%s block %d: header changed" % (rel, i))
            if n.references[:len(a.references)] != [v if v not in (SLOT, LIST_SLOT) else n.references[k]
                                                     for k, v in enumerate(a.references)]:
                fail("%s block %d: an existing constant changed" % (rel, i))
            if bytes(n.block_pad) != bytes(a.block_pad):
                fail("%s block %d: padding changed" % (rel, i))
        elif back[i].raw_block != a.raw_block:
            fail("%s block %d changed but was not patched" % (rel, i))
    if not patched and not extra and new != old:
        fail("%s changed with no patched block" % rel)


def main():
    if len(sys.argv) not in (2, 3):
        sys.exit(__doc__)
    import json
    out, report = build(sys.argv[1])
    print(json.dumps({k: v for k, v in report.items() if k != "blocks"}, indent=1))
    print("blocks patched:", len(report["blocks"]))
    if len(sys.argv) == 3:
        for rel, blob in out.items():
            p = os.path.join(sys.argv[2], *rel.split("/"))
            os.makedirs(os.path.dirname(p), exist_ok=True)
            open(p, "wb").write(blob)
        json.dump(report, open(os.path.join(sys.argv[2], "homepoint-report.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
