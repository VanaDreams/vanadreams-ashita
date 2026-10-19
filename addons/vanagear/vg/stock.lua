--[[
    vanagear: the rules that put the built sets on.

    These run for every job. With no profile of the player's own they are the whole profile;
    with one, they run first and the profile's own rules run after them, so anything the
    profile equips wins slot by slot and anything it leaves alone keeps the built piece.

    The order sets are laid on follows GetAwayCoxn's all-jobs retail profiles: a broad set
    first (Precast, Cure, Nuke), then the narrower ones over it (Cure_Precast, Burst, Drain).
]]--

local lists = require('vg.lists');

local stock = {};

local function wear(name)
    local set = gVana.Sets[name];
    if (set ~= nil) then
        gFunc.EquipSet(set);
        return true;
    end
    return false;
end

-- spell family sets laid over the skill set
local function family(spell)
    local name = spell.Name or '';
    if (spell.Skill == 'Healing Magic') then
        if (string.find(name, '^Cur')) then wear('Cure'); end
        if (name == 'Cursna') then wear('Cursna'); end
    elseif (spell.Skill == 'Enhancing Magic') then
        if (string.find(name, '^Stoneskin')) then wear('Stoneskin');
        elseif (string.find(name, '^Phalanx')) then wear('Phalanx');
        elseif (string.find(name, '^Regen')) then wear('Regen');
        elseif (string.find(name, '^Refresh')) then wear('Refresh'); end
    elseif (spell.Skill == 'Dark Magic') then
        if (string.find(name, '^Drain')) or (string.find(name, '^Aspir')) then wear('Drain'); end
    end
end

local skillSet = {
    ['Healing Magic'] = 'Cure',
    ['Enhancing Magic'] = 'Enhancing',
    ['Enfeebling Magic'] = 'Enfeebling',
    ['Elemental Magic'] = 'Nuke',
    ['Dark Magic'] = 'Dark',
    ['Divine Magic'] = 'Divine',
    ['Ninjutsu'] = 'Ninjutsu',
    ['Singing'] = 'Song',
    ['Geomancy'] = 'Geomancy',
};

local function overlays()
    local t = gVana.Toggles;
    if (t.DT) then wear('Dt'); end
    if (t.Kite) then wear('Movement'); end
    if (t.TH) then wear('TH'); end
end

stock.HandleDefault = function()
    local t = gVana.Toggles;
    local player = gData.GetPlayer();
    local pet = gData.GetPet();
    local petAction = gData.GetPetAction();

    if (petAction ~= nil) and (gVana.Sets.BPWard ~= nil) then
        -- a summoner's avatar: the blood pact decides the set
        local bp = petAction.Name or '';
        if (lists.BPBuff[bp]) then wear('BPWard');
        elseif (lists.BPDebuff[bp]) then wear('PetAcc');
        elseif (lists.BPMagical[bp]) then wear('PetMab');
        else wear('PetAtk'); end
        return;
    end
    if (petAction ~= nil) and (gVana.Sets.PetAtk ~= nil) then
        if (lists.EnfeebleReady[petAction.Name or ''] or t.Pet == 'acc') then
            wear('PetAcc');
        elseif (lists.MagicReady[petAction.Name or '']) then
            wear('PetMab');
        else
            wear('PetAtk');
        end
        return;
    end

    if (player.Status == 'Engaged') then
        wear('Tp_' .. (t.Tp or 'Default'));
    elseif (player.Status == 'Resting') then
        wear('Idle');
        wear('Resting');
    elseif (pet ~= nil) and (pet.Status == 'Engaged') and (gVana.Sets.PetTP ~= nil) then
        wear((t.Pet == 'dt') and 'PetDT' or 'PetTP');
    else
        wear('Idle');
        local idle = t.Idle or 'auto';
        if (idle == 'regen') or ((idle == 'auto') and (player.HPP < 60)) then wear('Idle_Regen'); end
        if (idle == 'refresh') or ((idle == 'auto') and (player.MaxMP > 0) and (player.MPP < 70)) then wear('Idle_Refresh'); end
        if (idle == 'speed') or (player.IsMoving) then wear('Movement'); end
    end
    overlays();
end

local quickDraw = { ['Fire Shot'] = 1, ['Ice Shot'] = 1, ['Wind Shot'] = 1, ['Earth Shot'] = 1, ['Thunder Shot'] = 1,
    ['Water Shot'] = 1, ['Light Shot'] = 2, ['Dark Shot'] = 2 };

stock.HandleAbility = function()
    local action = gData.GetAction();
    local name = action.Name or '';
    -- a set built for this very ability (jobs\<JOB>.lua Abilities) comes first
    if (wear((string.gsub(name, '[^%w]', '')))) then
        return;
    end
    if (string.find(action.Type or '', '^Blood Pact')) then
        wear('BPDelay');
    elseif (string.find(name, ' Roll$')) or (name == 'Double-Up') then
        wear('PhantomRoll');
    elseif (quickDraw[name] ~= nil) then
        -- Light Shot sleeps and Dark Shot dispels: they only need to land
        wear((quickDraw[name] == 2 or gVana.Toggles.Nuke == 'acc') and 'QuickDrawAcc' or 'QuickDraw');
    elseif (action.Type == 'Ready') or (name == 'Sic') then
        wear('ReadyPrecast');
    elseif (name == 'Reward') then
        wear('Reward');
    elseif (name == 'Charm') then
        wear('Charm');
    elseif (name == 'Call Beast') or (name == 'Bestial Loyalty') then
        wear('Call');
    end
end

stock.HandleItem = function()
end

stock.HandlePrecast = function()
    local spell = gData.GetAction();
    wear('Precast');
    if (spell.Skill == 'Healing Magic') and (string.find(spell.Name or '', '^Cur')) then
        wear('Cure_Precast');
    elseif (spell.Skill == 'Singing') then
        wear('Song_Precast');
    end
    if (gVana.Toggles.Stage) and (spell.CastTime ~= nil) and (gVana.Sets.Interim ~= nil) then
        -- the built Precast set's fast cast; a low guess would land the midcast gear late
        local fastCast = math.min(0.8, (gVana.FastCast or 0) / 100);
        local delay = ((spell.CastTime * (1 - fastCast)) / 1000) - 0.1;
        if (delay >= 0.4) then
            gFunc.SetMidDelay(delay);
        end
    end
end

stock.HandleMidcast = function()
    local t = gVana.Toggles;
    local spell = gData.GetAction();
    if (t.Stage) and (gVana.Sets.Interim ~= nil) then
        gFunc.InterimEquipSet(gVana.Sets.Interim);
    end
    if (spell.Skill == 'Blue Magic') then
        wear(lists.BluePhysical[spell.Name or ''] and 'BluePhysical' or 'BlueMagical');
    elseif (skillSet[spell.Skill] ~= nil) then
        wear(skillSet[spell.Skill]);
    end
    -- the set built for this very spell, under the toggles (burst, accuracy, obi) laid on next;
    -- a set of your own for it goes on last, over everything
    local spellSet = (string.gsub(spell.Name or '', '[^%w]', ''));
    local yours = (gVana.Own ~= nil) and (gVana.Own[spellSet] ~= nil) and (next(gVana.Own[spellSet]) ~= nil);
    if (not yours) then wear(spellSet); end
    if (spell.Skill == 'Elemental Magic') then
        if (t.Nuke == 'acc') then wear('NukeACC'); end
        if (t.Burst) then wear('Burst'); end
        local env = gData.GetEnvironment();
        if (spell.Element == env.WeatherElement) or (spell.Element == env.DayElement) then
            if (gVana.Carries('Hachirin-no-Obi')) then gFunc.Equip('Waist', 'Hachirin-no-Obi'); end
        end
    elseif (spell.Skill == 'Enfeebling Magic') and (t.Nuke == 'acc') then
        wear('Macc');
    end
    family(spell);
    -- a set of your own named for this very spell (as Cure IV or CureIV)
    if (yours) then wear(spellSet); end
    if (t.TH) then wear('TH'); end
end

stock.HandlePreshot = function()
    wear('Preshot');
end

stock.HandleMidshot = function()
    wear('Midshot');
    if (gVana.Toggles.TH) then wear('TH'); end
end

stock.HandleWeaponskill = function()
    local ws = gData.GetAction();
    if (not wear((string.gsub(ws.Name or '', '[^%w]', '')))) then
        wear('Ws_Default');
    end
end

-- The profile a job with no profile file gets: the rules above, its sets the built ones.
stock.Profile = function()
    return {
        Sets = gVana.Sets,
        Packer = {},
        IsVanagearStock = true,
        OnLoad = function() end,
        OnUnload = function() end,
        HandleCommand = function() end,
        HandleDefault = function() end,
        HandleAbility = function() end,
        HandleItem = function() end,
        HandlePrecast = function() end,
        HandleMidcast = function() end,
        HandlePreshot = function() end,
        HandleMidshot = function() end,
        HandleWeaponskill = function() end,
    };
end

return stock;
