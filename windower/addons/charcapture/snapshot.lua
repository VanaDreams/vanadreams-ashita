--[[
    The parts of charcapture for Windower that need no game: reading the packets the server sends and
    writing the snapshot as JSON. Kept apart so they can be checked off-game (windower/tests).

    The snapshot is the same file the Ashita charcapture writes (schema 1), so the launcher's Capture
    page and the importer take either one. Where Ashita reads its memory managers, this reads the
    packets those managers are filled from; every offset below is 1-based into the packet as it
    arrived, header included, which is how both Windower and Ashita hand packets to an addon.
]]

local M = {};

-- ---- reading bytes ------------------------------------------------------------------------

local function u8(s, pos) return s:byte(pos) or 0; end
local function u16(s, pos) local a, b = s:byte(pos, pos + 1); return (a or 0) + (b or 0) * 0x100; end
local function u32(s, pos) return u16(s, pos) + u16(s, pos + 2) * 0x10000; end
local function i32(s, pos) local v = u32(s, pos); if v >= 0x80000000 then v = v - 0x100000000; end return v; end
local function i16(s, pos) local v = u16(s, pos); if v >= 0x8000 then v = v - 0x10000; end return v; end
local function bits(v, shift, count) return math.floor(v / 2 ^ shift) % 2 ^ count; end
M.u8, M.u16, M.u32, M.i32, M.bits = u8, u16, u32, i32, bits;

local READ = { B = u8, H = u16, i = i32, I = u32 };
local SIZE = { B = 1, H = 2, i = 4, I = 4 };

function M.hex(s)
    if s == nil then return ''; end
    return (s:gsub('.', function (c) return ('%02x'):format(c:byte()); end));
end

-- ---- JSON ---------------------------------------------------------------------------------

-- Windower's json library only reads, so the snapshot is written here. A table made with M.array()
-- is a JSON list even when empty; any other table is an object with its keys as strings.
local ARRAY = {};
function M.array(t) return setmetatable(t or {}, ARRAY); end

local function quote(s)
    return '"' .. s:gsub('[%c"\\]', function (c)
        if c == '"' then return '\\"'; elseif c == '\\' then return '\\\\';
        elseif c == '\n' then return '\\n'; elseif c == '\r' then return '\\r'; elseif c == '\t' then return '\\t'; end
        return ('\\u%04x'):format(c:byte());
    end) .. '"';
end

function M.encode(v)
    local t = type(v);
    if v == nil then return 'null';
    elseif t == 'boolean' then return tostring(v);
    elseif t == 'number' then
        if v ~= v or v == math.huge or v == -math.huge then return 'null'; end
        if v == math.floor(v) and math.abs(v) < 2 ^ 53 then return ('%d'):format(v); end
        return ('%.14g'):format(v);
    elseif t == 'string' then return quote(v);
    elseif t == 'table' then
        local out = {};
        if getmetatable(v) == ARRAY then
            for i = 1, #v do out[i] = M.encode(v[i]); end
            return '[' .. table.concat(out, ',') .. ']';
        end
        local keys = {};
        for k in pairs(v) do keys[#keys + 1] = tostring(k); end
        table.sort(keys);
        local by_name = {};
        for k, x in pairs(v) do by_name[tostring(k)] = x; end
        for _, k in ipairs(keys) do out[#out + 1] = quote(k) .. ':' .. M.encode(by_name[k]); end
        return '{' .. table.concat(out, ',') .. '}';
    end
    return 'null';
end

-- ---- what the menus send (the same packets the Ashita charcapture keeps) --------------------

-- Spent merits and job point upgrades only arrive when the Merit Points and Job Points menus are opened.
--   0x08C: count at 0x04, then count entries of { u16 merit id, u8 next cost, u8 upgrades } from 0x08
--   0x08D: 64 entries of { u16 index:5 | job:11, u16 next:10 | level:6 } from 0x04
function M.read_merits(data, into)
    local count = u16(data, 0x05);
    if count > 61 then return false; end
    for i = 0, count - 1 do
        local at = 0x09 + i * 4;
        if #data < at + 3 then break; end
        local id, upgrades = u16(data, at), u8(data, at + 3);
        if id > 0 and upgrades > 0 then into[tostring(id)] = upgrades; end
    end
    return true;
end

function M.read_job_points(data, into)
    for i = 0, 63 do
        local at = 0x05 + i * 4;
        if #data < at + 3 then break; end
        local a, b = u16(data, at), u16(data, at + 2);
        if a == 0 then break; end
        local index, job, level = bits(a, 0, 5), bits(a, 5, 11), bits(b, 10, 6);
        if job > 0 and level > 0 then
            into[tostring(job)] = into[tostring(job)] or {};
            into[tostring(job)][tostring(index)] = level;
        end
    end
    return true;
end

-- Quest and mission logs arrive as 0x056 when you zone: a 32-byte block at 0x04 and a 'port' at 0x24
-- naming the log. Kept raw by port; the importer knows the layout.
function M.read_log(data, into)
    if #data < 0x28 then return false; end
    into[('%04x'):format(u16(data, 0x25))] = M.hex(data:sub(5, 36));
    return true;
end

-- Your own chocobo: one u32 the game sends only while you ride it, in 0x067 (type 0x02 at 0x04, your id at
-- 0x08, the u32 at 0x18) and in 0x00D (your id at 0x04, chocobo index 2 in bits 5-7 of 0x20, the u32 at 0x34).
function M.read_chocobo_sync(data, own_id)
    if #data < 0x1C or u8(data, 0x05) ~= 0x02 or u32(data, 0x09) ~= own_id then return nil; end
    local props = u32(data, 0x19);
    return props ~= 0 and props or nil;
end

function M.read_chocobo_update(data, own_id)
    if #data < 0x38 or u32(data, 0x05) ~= own_id then return nil; end
    if bits(u8(data, 0x21), 5, 3) ~= 2 then return nil; end
    local props = u32(data, 0x35);
    return props ~= 0 and props or nil;
end

-- The Currencies tabs of the Profile menu: 0x113 (Currencies 1) and 0x118 (Currencies 2), each number read at
-- the offset the server's own packet uses and kept under its Vanadreams database column. Offsets are 0-based
-- into the packet (header included). Copied from the Ashita charcapture, which generated them from
-- src/map/packets/s2c/0x113_currencies_1.h and 0x118_currencies_2.h.
local CURRENCY_1 = {
    { 4, 'i', 'sandoria_cp' }, { 8, 'i', 'bastok_cp' }, { 12, 'i', 'windurst_cp' }, { 16, 'H', 'beastman_seal' },
    { 18, 'H', 'kindred_seal' }, { 20, 'H', 'kindred_crest' }, { 22, 'H', 'high_kindred_crest' },
    { 24, 'H', 'sacred_kindred_crest' }, { 26, 'H', 'ancient_beastcoin' }, { 28, 'H', 'valor_point' },
    { 30, 'H', 'scyld' }, { 32, 'i', 'guild_fishing' }, { 36, 'i', 'guild_woodworking' },
    { 40, 'i', 'guild_smithing' }, { 44, 'i', 'guild_goldsmithing' }, { 48, 'i', 'guild_weaving' },
    { 52, 'i', 'guild_leathercraft' }, { 56, 'i', 'guild_bonecraft' }, { 60, 'i', 'guild_alchemy' },
    { 64, 'i', 'guild_cooking' }, { 68, 'i', 'cinder' }, { 72, 'B', 'fire_fewell' }, { 73, 'B', 'ice_fewell' },
    { 74, 'B', 'wind_fewell' }, { 75, 'B', 'earth_fewell' }, { 76, 'B', 'lightning_fewell' },
    { 77, 'B', 'water_fewell' }, { 78, 'B', 'light_fewell' }, { 79, 'B', 'dark_fewell' },
    { 80, 'i', 'ballista_point' }, { 84, 'i', 'fellow_point' }, { 88, 'H', 'chocobuck_sandoria' },
    { 90, 'H', 'chocobuck_bastok' }, { 92, 'H', 'chocobuck_windurst' }, { 96, 'i', 'research_mark' },
    { 100, 'B', 'tunnel_worm' }, { 101, 'B', 'morion_worm' }, { 102, 'B', 'phantom_worm' },
    { 104, 'i', 'moblin_marble' }, { 108, 'H', 'infamy' }, { 110, 'H', 'prestige' }, { 112, 'i', 'legion_point' },
    { 116, 'i', 'spark_of_eminence' }, { 120, 'i', 'shining_star' }, { 124, 'i', 'imperial_standing' },
    { 128, 'i', 'leujaoam_assault_point' }, { 132, 'i', 'mamool_assault_point' },
    { 136, 'i', 'lebros_assault_point' }, { 140, 'i', 'periqia_assault_point' }, { 144, 'i', 'ilrusi_assault_point' },
    { 148, 'i', 'nyzul_isle_assault_point' }, { 152, 'i', 'zeni_point' }, { 156, 'i', 'jetton' },
    { 160, 'i', 'therion_ichor' }, { 164, 'i', 'allied_notes' }, { 168, 'H', 'aman_vouchers' },
    { 170, 'H', 'login_points' }, { 172, 'i', 'cruor' }, { 176, 'i', 'resistance_credit' },
    { 180, 'i', 'dominion_note' }, { 184, 'B', 'fifth_echelon_trophy' }, { 185, 'B', 'fourth_echelon_trophy' },
    { 186, 'B', 'third_echelon_trophy' }, { 187, 'B', 'second_echelon_trophy' }, { 188, 'B', 'first_echelon_trophy' },
    { 189, 'B', 'cave_points' }, { 190, 'B', 'id_tags' }, { 191, 'B', 'op_credits' }, { 192, 'i', 'traverser_stones' },
    { 196, 'i', 'voidstones' },
    { 200, 'i', 'kupofried_corundums' }, { 204, 'B', 'pheromone_sacks' }, { 206, 'B', 'rems_ch1' },
    { 207, 'B', 'rems_ch2' }, { 208, 'B', 'rems_ch3' }, { 209, 'B', 'rems_ch4' }, { 210, 'B', 'rems_ch5' },
    { 211, 'B', 'rems_ch6' }, { 212, 'B', 'rems_ch7' }, { 213, 'B', 'rems_ch8' }, { 214, 'B', 'rems_ch9' },
    { 215, 'B', 'rems_ch10' }, { 224, 'H', 'reclamation_marks' }, { 228, 'i', 'unity_accolades' },
    { 232, 'H', 'fire_crystals' }, { 234, 'H', 'ice_crystals' }, { 236, 'H', 'wind_crystals' },
    { 238, 'H', 'earth_crystals' }, { 240, 'H', 'lightning_crystals' }, { 242, 'H', 'water_crystals' },
    { 244, 'H', 'light_crystals' }, { 246, 'H', 'dark_crystals' }, { 248, 'H', 'deeds' },
};
local CURRENCY_2 = {
    { 4, 'i', 'bayld' }, { 8, 'H', 'kinetic_unit' }, { 10, 'B', 'imprimaturs' }, { 11, 'B', 'mystical_canteen' },
    { 12, 'i', 'obsidian_fragment' }, { 16, 'H', 'lebondopt_wing' }, { 18, 'H', 'pulchridopt_wing' },
    { 20, 'i', 'mweya_plasm' }, { 24, 'B', 'ghastly_stone' }, { 25, 'B', 'ghastly_stone_1' },
    { 26, 'B', 'ghastly_stone_2' }, { 27, 'B', 'verdigris_stone' }, { 28, 'B', 'verdigris_stone_1' },
    { 29, 'B', 'verdigris_stone_2' }, { 30, 'B', 'wailing_stone' }, { 31, 'B', 'wailing_stone_1' },
    { 32, 'B', 'wailing_stone_2' }, { 33, 'B', 'snowslit_stone' }, { 34, 'B', 'snowslit_stone_1' },
    { 35, 'B', 'snowslit_stone_2' }, { 36, 'B', 'snowtip_stone' }, { 37, 'B', 'snowtip_stone_1' },
    { 38, 'B', 'snowtip_stone_2' }, { 39, 'B', 'snowdim_stone' }, { 40, 'B', 'snowdim_stone_1' },
    { 41, 'B', 'snowdim_stone_2' }, { 42, 'B', 'snoworb_stone' }, { 43, 'B', 'snoworb_stone_1' },
    { 44, 'B', 'snoworb_stone_2' }, { 45, 'B', 'leafslit_stone' }, { 46, 'B', 'leafslit_stone_1' },
    { 47, 'B', 'leafslit_stone_2' }, { 48, 'B', 'leaftip_stone' }, { 49, 'B', 'leaftip_stone_1' },
    { 50, 'B', 'leaftip_stone_2' }, { 51, 'B', 'leafdim_stone' }, { 52, 'B', 'leafdim_stone_1' },
    { 53, 'B', 'leafdim_stone_2' }, { 54, 'B', 'leaforb_stone' }, { 55, 'B', 'leaforb_stone_1' },
    { 56, 'B', 'leaforb_stone_2' }, { 57, 'B', 'duskslit_stone' }, { 58, 'B', 'duskslit_stone_1' },
    { 59, 'B', 'duskslit_stone_2' }, { 60, 'B', 'dusktip_stone' }, { 61, 'B', 'dusktip_stone_1' },
    { 62, 'B', 'dusktip_stone_2' }, { 63, 'B', 'duskdim_stone' }, { 64, 'B', 'duskdim_stone_1' },
    { 65, 'B', 'duskdim_stone_2' }, { 66, 'B', 'duskorb_stone' }, { 67, 'B', 'duskorb_stone_1' },
    { 68, 'B', 'duskorb_stone_2' }, { 69, 'B', 'pellucid_stone' }, { 70, 'B', 'fern_stone' },
    { 71, 'B', 'taupe_stone' }, { 74, 'H', 'escha_beads' }, { 76, 'i', 'escha_silt' }, { 80, 'i', 'potpourri' },
    { 84, 'i', 'current_hallmarks' }, { 88, 'i', 'total_hallmarks' }, { 92, 'i', 'gallantry' },
    { 96, 'i', 'crafter_points' }, { 100, 'B', 'fire_crystal_set' }, { 101, 'B', 'ice_crystal_set' },
    { 102, 'B', 'wind_crystal_set' }, { 103, 'B', 'earth_crystal_set' }, { 104, 'B', 'lightning_crystal_set' },
    { 105, 'B', 'water_crystal_set' }, { 106, 'B', 'light_crystal_set' }, { 107, 'B', 'dark_crystal_set' },
    { 108, 'B', 'mc_s_sr01_set' }, { 109, 'B', 'mc_s_sr02_set' }, { 110, 'B', 'mc_s_sr03_set' },
    { 111, 'B', 'liquefaction_spheres_set' }, { 112, 'B', 'induration_spheres_set' },
    { 113, 'B', 'detonation_spheres_set' }, { 114, 'B', 'scission_spheres_set' },
    { 115, 'B', 'impaction_spheres_set' }, { 116, 'B', 'reverberation_spheres_set' },
    { 117, 'B', 'transfixion_spheres_set' }, { 118, 'B', 'compression_spheres_set' },
    { 119, 'B', 'fusion_spheres_set' }, { 120, 'B', 'distortion_spheres_set' },
    { 121, 'B', 'fragmentation_spheres_set' }, { 122, 'B', 'gravitation_spheres_set' },
    { 123, 'B', 'light_spheres_set' }, { 124, 'B', 'darkness_spheres_set' }, { 128, 'i', 'silver_aman_voucher' },
    { 132, 'i', 'domain_points' }, { 136, 'i', 'domain_points_daily' }, { 140, 'i', 'mog_segments' },
    { 144, 'i', 'gallimaufry' }, { 148, 'H', 'is_accolades' }, { 152, 'i', 'temenos_units' },
    { 156, 'i', 'apollyon_units' },
};

-- The five 9-bit counts packed into the 64 bits at 216 of 0x113 (Bloodshed, Umbrage, Ritualistic,
-- Tutelary and Primacy plans), low bits first.
local PLANS = { { 0, 'bloodshed_plans' }, { 9, 'umbrage_plans' }, { 18, 'ritualistic_plans' }, { 27, 'tutelary_plans' }, { 36, 'primacy_plans' } };

local function read_currency_table(data, rows, into)
    for _, r in ipairs(rows) do
        local off, fmt, col = r[1], r[2], r[3];
        if #data >= off + SIZE[fmt] then into[col] = READ[fmt](data, off + 1); end
    end
end

function M.read_currencies_1(data, into)
    read_currency_table(data, CURRENCY_1, into);
    if #data >= 224 then
        local lo, hi = u32(data, 217), u32(data, 221);
        for _, p in ipairs(PLANS) do
            local shift, col = p[1], p[2];
            if shift + 9 <= 32 then into[col] = bits(lo, shift, 9);
            elseif shift >= 32 then into[col] = bits(hi, shift - 32, 9);
            else into[col] = (bits(lo, shift, 32 - shift) + bits(hi, 0, shift + 9 - 32) * 2 ^ (32 - shift)) % 0x200; end
        end
    end
    return true;
end

function M.read_currencies_2(data, into)
    read_currency_table(data, CURRENCY_2, into);
    return true;
end

-- ---- what Ashita reads from memory, from the packets that fill it ---------------------------

local JOB_COUNT = 22;      -- 1 WAR .. 22 RUN
M.JOB_COUNT = JOB_COUNT;

-- 0x062, the skill list of the job you are on: 48 combat skills from 0x80 { level:15, capped:1 } and
-- 10 crafts from 0xE0 { rank:5, level:10, capped:1 }, the same numbering Ashita uses.
function M.read_skills(data)
    local combat, craft = {}, {};
    if #data < 0xF4 then return nil; end
    for i = 0, 47 do
        local v = u16(data, 0x81 + i * 2);
        combat[tostring(i)] = { skill = bits(v, 0, 15), rank = 0, capped = bits(v, 15, 1) == 1 };
    end
    for i = 0, 9 do
        local v = u16(data, 0xE1 + i * 2);
        craft[tostring(i)] = { skill = bits(v, 5, 10), rank = bits(v, 0, 5), capped = bits(v, 15, 1) == 1 };
    end
    return combat, craft;
end

-- 0x01B, job info: every job's level at 0x48 + job, master levels at 0x6C + job.
function M.read_job_info(data)
    if #data < 0x6C + JOB_COUNT + 1 then return nil; end
    local levels, master = {}, {};
    for job = 1, JOB_COUNT do
        levels[tostring(job)] = u8(data, 0x49 + job);
        master[tostring(job)] = u8(data, 0x6D + job);
    end
    return levels, master;
end

-- 0x061, character status: jobs, exp, title, nation rank and rank points, home point, nation.
function M.read_status(data)
    if #data < 0x51 then return nil; end
    return {
        main = u8(data, 0x0D), main_level = u8(data, 0x0E), sub = u8(data, 0x0F), sub_level = u8(data, 0x10),
        exp_current = i16(data, 0x11), exp_needed = i16(data, 0x13),
        title = u16(data, 0x45), rank = u16(data, 0x47), rank_points = u16(data, 0x49),
        homepoint = u16(data, 0x4B), nation = u8(data, 0x51),
    };
end

-- 0x063 type 2 (merits): limit points at 0x08, merit points in the low 7 bits at 0x0A, the maximum at 0x0C.
-- 0x063 type 5 (job points): 24 entries from 0x0C, by job, of { capacity, job points, spent }.
function M.read_misc(data, into)
    if #data < 0x0E then return false; end
    local kind = u16(data, 0x05);
    if kind == 0x02 then
        into.limit_points = u16(data, 0x09);
        into.merit_points = bits(u16(data, 0x0B), 0, 7);
        into.merit_points_max = u8(data, 0x0D);
        return true;
    elseif kind == 0x05 and #data >= 0x0C + 24 * 6 then
        into.job_points = {};
        for job = 1, JOB_COUNT do
            local at = 0x0D + job * 6;
            into.job_points[tostring(job)] = { capacity = u16(data, at), points = u16(data, at + 2), spent = u16(data, at + 4) };
        end
        return true;
    end
    return false;
end

-- What you look like: face, race and the nine model ids, as 0x051 sends them on every gear change and 0x00A
-- on every zone (the model ids keep their slot's high nibble, as Ashita's do). 0x00A also carries your size:
-- byte 0x21 is gender * 128 + (1 << size).
local LOOK = { 'head', 'body', 'hands', 'legs', 'feet', 'main', 'sub', 'ranged' };

local function read_grap(data, at)
    local look = { face = u8(data, at), race = u8(data, at + 1) };
    for i, slot in ipairs(LOOK) do look[slot] = u16(data, at + i * 2); end
    return look;
end

function M.read_look_update(data)
    if #data < 0x16 then return nil; end
    return read_grap(data, 0x05);
end

function M.read_zone_in(data)
    if #data < 0x56 then return nil; end
    local look = read_grap(data, 0x45);
    local size = ({ [1] = 0, [2] = 1, [4] = 2 })[u8(data, 0x22) % 0x80];
    return look, size;
end

return M;
