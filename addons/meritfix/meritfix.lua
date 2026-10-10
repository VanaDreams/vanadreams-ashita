--[[
    meritfix - makes the game's own merit lines show the real numbers past 127.

    The game keeps the merit count in 7 bits (0-127) and the maximum in 8 bits (0-255), so a count of 130 reads
    "2". The Vanadreams server sends the true count and the true maximum in three spare bytes of the same packet.
    This changes the game's code in memory, in the three places that read those numbers, so they show (and
    compare) the real values:

        1. the "<points> / <max> P" line of the status window,
        2. the stand-alone merit number,
        3. the check that greys out a merit you cannot afford yet.

    It loads by itself from the startup script. Unloading it puts the game's original code back. Nothing is sent
    to the server; on a server that does not send the spare bytes it shows the game's own numbers, as before.

    /meritfix   prints whether the three patches are applied and the numbers it holds.
]]

addon.name    = 'meritfix';
addon.author  = 'Vanadreams';
addon.version = '0.1.0';
addon.desc    = 'Shows merit points past 127 in the game\'s own merit lines.';

require('common');

local function say(msg) print(('\30\08[meritfix]\30\01 %s'):format(msg)); end

-- Two dwords the patched code reads: [0] the merit points, [1] the maximum.
local vars = ashita.memory.alloc(8);
local patches = {};   -- { name, addr, original (table) }

local function dword(n)
    return { n % 256, math.floor(n / 256) % 256, math.floor(n / 65536) % 256, math.floor(n / 16777216) % 256 };
end

local function cat(...)
    local out = {};
    for _, t in ipairs({ ... }) do for _, b in ipairs(t) do out[#out + 1] = b; end end
    return out;
end

local function pad(t, n)
    while #t < n do t[#t + 1] = 0x90; end
    return t;
end

-- Where the game's code reads the numbers. Each pattern is the exact code found in FFXiMain.dll (taken from a dump of
-- the running game); if it is not found, that patch is skipped and the game shows its own number.
local sites = {
    {
        name = 'status line "points / max P"',
        pattern = '668B47028A4F048AD0660FB6F983E27F663BD7' .. '7205BDA060208081E1FF00000083E07F5150',
        -- mov edx,[points]; mov ecx,[max]; cmp edx,ecx; jb +5; mov ebp,0x802060A0; mov eax,edx; push ecx; push eax
        build = function(p, m)
            return pad(cat({ 0x8B, 0x15 }, dword(p), { 0x8B, 0x0D }, dword(m),
                { 0x3B, 0xD1, 0x72, 0x05, 0xBD, 0xA0, 0x60, 0x20, 0x80, 0x8B, 0xC2, 0x51, 0x50 }), 37);
        end,
        size = 37,
    },
    {
        name = 'stand-alone merit number',
        pattern = '8A40028D4C240883E07F5068',
        -- mov eax,[points]; lea ecx,[esp+8]; nop     (the push eax / push format after it are left alone)
        build = function(p, m)
            return pad(cat({ 0xA1 }, dword(p), { 0x8D, 0x4C, 0x24, 0x08 }), 10);
        end,
        size = 10,
    },
    {
        name = 'affordability check',
        pattern = '8A55028B4C244883E27F3BCA7F1A',
        -- mov edx,[points]; mov ecx,[esp+0x48]     (the cmp / jg after it are left alone)
        build = function(p, m)
            return cat({ 0x8B, 0x15 }, dword(p), { 0x8B, 0x4C, 0x24, 0x48 });
        end,
        size = 10,
    },
};

local function write(addr, bytes)
    local ok, prot = ashita.memory.unprotect(addr, #bytes);
    if not ok then return false; end
    ashita.memory.write_array(addr, bytes);
    ashita.memory.protect(addr, #bytes, prot);
    return true;
end

local function apply()
    if vars == nil or vars == 0 then say('could not allocate memory'); return; end
    for _, s in ipairs(sites) do
        local addr = ashita.memory.find('FFXiMain.dll', 0, s.pattern, 0, 0);
        if addr == nil or addr == 0 then
            say(('patch skipped, code not found: %s'):format(s.name));
        else
            local original = ashita.memory.read_array(addr, s.size);
            local patched  = s.build(vars, vars + 4);
            if #patched ~= s.size then
                say(('patch skipped, wrong size: %s'):format(s.name));
            elseif write(addr, patched) then
                patches[#patches + 1] = { name = s.name, addr = addr, original = original };
            end
        end
    end
    say(('%d of %d patches applied'):format(#patches, #sites));
end

local function restore()
    for _, p in ipairs(patches) do write(p.addr, p.original); end
    patches = {};
end

-- Start from what the game already holds (the 7-bit values), so the lines are right before the first packet.
local function seed()
    local player = AshitaCore:GetMemoryManager():GetPlayer();
    ashita.memory.write_uint32(vars, player:GetMeritPoints() or 0);
    ashita.memory.write_uint32(vars + 4, player:GetMeritPointsMax() or 0);
end

-- 0x063 type 0x02. Layout: type u16 at 0x04, limit points u16 at 0x08, merit bitfield u16 at 0x0A (count in the
-- low 7 bits), max u8 at 0x0C, then three spare bytes at 0x0D: (0xA << 20) | (max << 10) | count.
ashita.events.register('packet_in', 'meritfix_packet_in', function (e)
    if e.id ~= 0x0063 or #e.data < 0x10 then return; end
    if struct.unpack('H', e.data, 0x04 + 1) ~= 0x0002 then return; end
    local lo, mid, hi = struct.unpack('BBB', e.data, 0x0D + 1);
    local spare = lo + mid * 256 + hi * 65536;
    local pts, mx;
    if math.floor(spare / 1048576) == 0xA then
        pts = spare % 1024;
        mx  = math.floor(spare / 1024) % 1024;
    else
        pts = struct.unpack('H', e.data, 0x0A + 1) % 128;
        mx  = struct.unpack('B', e.data, 0x0C + 1);
    end
    ashita.memory.write_uint32(vars, pts);
    ashita.memory.write_uint32(vars + 4, mx);
end);

ashita.events.register('load', 'meritfix_load', function ()
    pcall(seed);
    apply();
end);

ashita.events.register('unload', 'meritfix_unload', function ()
    restore();
    if vars ~= nil and vars ~= 0 then ashita.memory.dealloc(vars); end
end);

ashita.events.register('command', 'meritfix_command', function (e)
    local args = e.command:args();
    if #args == 0 or args[1]:lower() ~= '/meritfix' then return; end
    e.blocked = true;
    say(('%d patches applied. Holding %d merit points, maximum %d.'):format(
        #patches, ashita.memory.read_uint32(vars), ashita.memory.read_uint32(vars + 4)));
    for _, p in ipairs(patches) do say(('  %s at 0x%08X'):format(p.name, p.addr)); end
end);
