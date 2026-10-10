--[[
    vanagear: GearSwap's set names and vanagear's.

    The launcher's From Windower page writes a player's GearSwap sets under GearSwap's own
    names (precast.FC, midcast.Cure, engaged.Acc, precast.WS.Savage Blade). This reads those
    names as vanagear's sets, for /gs equip and for showing which of a player's sets stands in
    for which built one.
]]--

local gsnames = {};

-- Checked in order; the first pattern that matches the lower-cased name wins.
local map = {
    { '^precast%.ws%.(.+)$', function(ws) return (string.gsub(ws, '[^%w]', '')); end },
    { '^precast%.ws$', 'Ws_Default' },
    { '^ws$', 'Ws_Default' },
    { '^precast%.fc%.cure', 'Cure_Precast' },
    { '^precast%.fc%.healing magic$', 'Cure_Precast' },
    { '^precast%.fc%.bardsong', 'Song_Precast' },
    { '^precast%.fc', 'Precast' },
    { '^precast%.fastcast', 'Precast' },
    { '^fastcast$', 'Precast' },
    { '^precast%.ra', 'Preshot' },
    { '^midcast%.ra', 'Midshot' },
    { '^midcast%.cure', 'Cure' },
    { '^midcast%.cursna', 'Cursna' },
    { '^midcast%.healing magic', 'Cure' },
    { '^midcast%.stoneskin', 'Stoneskin' },
    { '^midcast%.phalanx', 'Phalanx' },
    { '^midcast%.regen', 'Regen' },
    { '^midcast%.refresh', 'Refresh' },
    { '^midcast%.enhancing magic', 'Enhancing' },
    { '^midcast%.enfeebling magic', 'Enfeebling' },
    { '^midcast%.elemental magic%.mb', 'Burst' },
    { '^midcast%.elemental magic%.resistant', 'NukeACC' },
    { '^midcast%.elemental magic', 'Nuke' },
    { '^midcast%.drain', 'Drain' },
    { '^midcast%.aspir', 'Drain' },
    { '^midcast%.dark magic', 'Dark' },
    { '^midcast%.divine magic', 'Divine' },
    { '^midcast%.ninjutsu', 'Ninjutsu' },
    { '^midcast%.bardsong', 'Song' },
    { '^midcast%.singing', 'Song' },
    { '^midcast%.blue magic%.physical', 'BluePhysical' },
    { '^midcast%.blue magic', 'BlueMagical' },
    { '^midcast%.geomancy', 'Geomancy' },
    { '^midcast%.fastrecast', 'Interim' },
    { '^engaged%.acc', 'Tp_Acc' },
    { '^engaged%.dt', 'Tp_Hybrid' },
    { '^engaged%.hybrid', 'Tp_Hybrid' },
    { '^engaged%.pdt', 'Tp_Hybrid' },
    { '^engaged', 'Tp_Default' },
    { '^idle%.regen', 'Idle_Regen' },
    { '^idle%.refresh', 'Idle_Refresh' },
    { '^idle', 'Idle' },
    { '^resting', 'Resting' },
    { '^defense%.', 'Dt' },
    { '^pdt$', 'Dt' },
    { '^dt$', 'Dt' },
    { '^kiting$', 'Movement' },
    { '^treasurehunter$', 'TH' },
    { '^th$', 'TH' },
};

-- vanagear's name for a GearSwap set name, or nil.
gsnames.ToVanagear = function(name)
    local lower = string.lower(name or '');
    lower = string.gsub(lower, '^sets%.', '');
    for _, m in ipairs(map) do
        local captured = string.match(lower, m[1]);
        if (captured ~= nil) then
            if (type(m[2]) == 'function') then
                -- keep the weaponskill's own capitals from the original name
                local original = string.match(name, '[Ww][Ss]%.(.+)$') or captured;
                return m[2](original);
            end
            return m[2];
        end
    end
    return nil;
end

return gsnames;
