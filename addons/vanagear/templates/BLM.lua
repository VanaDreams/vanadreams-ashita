--[[
    vanagear template: Black Mage. vanagear writes your BLM profile from this the first time you load BLM.

    The file is in sections, in the order a profile runs, so each thing has an obvious place:
      1. built       the sets vanagear builds from your bags (it rewrites this; leave it alone)
      2. sets        your sets: the /vg window's drop-downs, "Take what I wear" and /vg addset write here
      3. lists       names your own rules look things up in
      4. state       your toggles and modes
      5. commands    /vg <word> lines, made in the window's Your commands tab
      6. your code   functions of your own: chains, timers, anything
      7. handlers    what goes on, when; each has a place marked for your own lines

    What a Black Mage does decides which sections are filled (the Vanadreams wiki's Black Mage guide): it
    casts elemental, enfeebling and dark magic of its own, so the spell handlers are its main work. A nuke
    is Nuke, then Burst over it with /vg burst (a magic burst is worked out when the cast finishes, inside
    the skillchain window), NukeACC over it with /vg nuke acc, and Obi when the element matches the day or
    weather; Drain and Aspir have their own sets; it rests for MP (Clear Mind works only while resting).
    One of its abilities lasts and changes what you take:
    - Mana Wall (5 minutes): half the damage you take is paid in MP, and gear adds to it (Taranus's
      Cape); ManaWall goes over standing and fighting while it is up.
    It swings a club, dagger, scythe or staff, and its subjob can bring more (Scholar's Arts, a pet on /SMN).
]]--

local profile = {};

-- 1. built --------------------------------------------------------------------------------------------
-- vanagear's sets from your bags: one for standing, fighting, each job ability, weapon skill and spell
-- you have at your level. Rewritten at each rebuild; change gear in sets below, never here.
local built = {
};
profile.Built = built;

-- 2. sets ---------------------------------------------------------------------------------------------
-- Yours. A set named like a built one fills over it slot by slot; a slot you leave out stays built.
-- A set named after a job ability, weapon skill or spell goes on for it (Berserk, SavageBlade, Utsusemi).
local sets = {
    -- sets vanagear does not build, here so they show in the /vg window to fill:
    Stoneskin_Precast = {}, -- over Precast for Stoneskin
    ManaWall = {},          -- over standing and fighting while Mana Wall is up
};
profile.Sets = sets;

-- 3. lists --------------------------------------------------------------------------------------------
-- Names your rules check against: weapon skills that want a different set, items, zones.
local lists = {
};

-- 4. state --------------------------------------------------------------------------------------------
-- Your toggles and modes. /vg dt, /vg kite, /vg th and /vg tp are vanagear's and need nothing here.
local state = {
};

-- 5. commands -----------------------------------------------------------------------------------------
-- Your commands: /vg <word> runs the lines in order. "wait 2" pauses two seconds, "then /ws ..." goes
-- when the action before it has finished (a skillchain), "burst /ma ..." the moment it has.
local commands = {
};
profile.Commands = commands;

profile.Packer = {
};

-- 6. your code ----------------------------------------------------------------------------------------
-- Functions of your own go here, above the handlers that call them.

local vg = gVanaInclude;   -- vanagear's checks and toggles, the same for every job

-- a set by name, put on only if you have one by that name
local function wear(name)
    if (sets[name] ~= nil) then gFunc.EquipSet(sets[name]); end
end

-- the set named after an action (Blood Rage -> sets.BloodRage), if there is one
local function setFor(name)
    return sets[string.gsub(name or '', '[^%w]', '')];
end

-- each kind of magic's own set, worn under a spell's own set (whatever job brings the spell)
local skillSets = {
    ['Healing Magic'] = 'Cure', ['Enhancing Magic'] = 'Enhancing', ['Enfeebling Magic'] = 'Enfeebling',
    ['Elemental Magic'] = 'Nuke', ['Dark Magic'] = 'Dark', ['Divine Magic'] = 'Divine', ['Ninjutsu'] = 'Ninjutsu',
    ['Singing'] = 'Song', ['Blue Magic'] = 'BlueMagical', ['Geomancy'] = 'Geomancy',
};

-- vanagear's helpers, the same in every job -------------------------------------------------------------

-- a set you have put gear in yourself (the /vg window, Take what I wear, /vg addset)
local function yours(name)
    return (gVana.Own ~= nil) and (gVana.Own[name] ~= nil) and (next(gVana.Own[name]) ~= nil);
end

-- spells that share a set by name: Regen II wears Regen, Aspir III wears Drain (any job's spells)
local families = {
    { skill = 'Enhancing Magic', match = '^Phalanx', set = 'Phalanx' },
    { skill = 'Enhancing Magic', match = '^Stoneskin', set = 'Stoneskin' },
    { skill = 'Enhancing Magic', match = '^Regen', set = 'Regen' },
    { skill = 'Enhancing Magic', match = '^Refresh', set = 'Refresh' },
    { skill = 'Healing Magic', match = '^Cursna', set = 'Cursna' },
    { skill = 'Dark Magic', match = '^Drain', set = 'Drain' },
    { skill = 'Dark Magic', match = '^Aspir', set = 'Drain' },
    { skill = 'Elemental Magic', match = 'helix', set = 'Helix' },
};
local function familyOf(spell)
    for _, f in ipairs(families) do
        if (spell.Skill == f.skill) and (string.find(spell.Name, f.match)) then return f.set; end
    end
    return nil;
end

-- enhancing spells whose strength comes from enhancing skill (Bar spells, Temper, Gain-, enspells)
local function bySkill(name)
    return (string.find(name, '^Bar') ~= nil) or (string.find(name, '^Temper') ~= nil) or (string.find(name, '^Gain%-') ~= nil)
        or (string.find(name, '^En%a+$') ~= nil) or (string.find(name, '^En%a+ II$') ~= nil);
end

-- cancels: a Stoneskin or Sneak already on you blocks the new one, so it comes off and the spell goes
-- again a moment later; Sneak comes off before Spectral Jig the same way
local function cancels(action)
    local target = gData.GetActionTarget();
    local onMe = (target ~= nil) and (target.Name == gData.GetPlayer().Name);
    local chat = AshitaCore:GetChatManager();
    local function again(line, delay) (function() chat:QueueCommand(1, line); end):once(delay); end
    if (action.Name == 'Spectral Jig') and (gData.GetBuffCount('Sneak') > 0) then
        gFunc.CancelAction(); chat:QueueCommand(1, '/cancel Sneak'); again('/ja "Spectral Jig" <me>', 2);
    elseif (action.Name == 'Sneak') and onMe and (gData.GetBuffCount('Sneak') > 0) then
        gFunc.CancelAction(); chat:QueueCommand(1, '/cancel Sneak'); again('/ma "Sneak" <me>', 1);
    elseif (action.Name == 'Stoneskin') and (gData.GetBuffCount('Stoneskin') > 0) then
        gFunc.CancelAction(); chat:QueueCommand(1, '/cancel Stoneskin'); again('/ma "Stoneskin" <me>', 1);
    end
end

-- weapon skills that reach from a distance (ranged and magical); every other one needs you close
local distant = {
    ['Flaming Arrow'] = true, ['Piercing Arrow'] = true, ['Dulling Arrow'] = true, ['Sidewinder'] = true,
    ['Blast Arrow'] = true, ['Arching Arrow'] = true, ['Empyreal Arrow'] = true, ['Refulgent Arrow'] = true,
    ['Apex Arrow'] = true, ['Namas Arrow'] = true, ['Jishnu\'s Radiance'] = true, ['Hot Shot'] = true,
    ['Split Shot'] = true, ['Sniper Shot'] = true, ['Slug Shot'] = true, ['Blast Shot'] = true, ['Heavy Shot'] = true,
    ['Detonator'] = true, ['Numbing Shot'] = true, ['Last Stand'] = true, ['Coronach'] = true, ['Wildfire'] = true,
    ['Trueflight'] = true, ['Leaden Salute'] = true, ['Myrkr'] = true, ['Dagan'] = true, ['Moonlight'] = true,
    ['Starlight'] = true,
};
local reach = 4.7;   -- yalms, for the ones that need you close

-- the weapon skill will not land: too far, under 1,000 TP, or asleep, petrified, stunned, terrorized,
-- amnesiac or charmed; it is cancelled before any gear swaps
local function wsBailout(ws)
    local target = gData.GetActionTarget();
    if (not distant[ws.Name]) and (target ~= nil) and (tonumber(target.Distance) or 0) > reach then
        gFunc.Message('Too far for ' .. ws.Name .. '.');
        return true;
    end
    if (gData.GetPlayer().TP < 1000) then return true; end
    for _, b in ipairs({ 'Sleep', 'Petrification', 'Stun', 'Terror', 'Amnesia', 'Charm' }) do
        if (gData.GetBuffCount(b) > 0) then return true; end
    end
    return false;
end

-- 7. handlers -----------------------------------------------------------------------------------------

profile.OnLoad = function()
    gSettings.AllowAddSet = true;
end

profile.OnUnload = function()
end

-- /vg fwd <word> ... arrives here
profile.HandleCommand = function(args)
end

-- Every moment nothing is going off: a pet's move (/BST), standing, fighting, resting.
profile.HandleDefault = function()
    local player = gData.GetPlayer();
    local pet = gData.GetPet();
    local petAction = gData.GetPetAction();

    -- the pet's move, while it goes off: its own set, else PetAtk
    if (petAction ~= nil) then
        if (setFor(petAction.Name) ~= nil) then gFunc.EquipSet(setFor(petAction.Name)); else wear('PetAtk'); end

        -- yours:
        return;
    end

    wear('Idle');
    if (pet ~= nil) and (pet.Status == 'Engaged') and (player.Status ~= 'Engaged') then
        wear('PetTP');
    end
    if (player.Status == 'Engaged') then
        wear('Tp_' .. (gVana.Toggles.Tp or 'Default'));
    elseif (player.Status == 'Resting') then
        wear('Resting');
    end
    vg.Default();   -- movement, regen and refresh when low, DT, TH, town, the rings in use
    -- Mana Wall, while it is up
    if (gData.GetBuffCount('Mana Wall') > 0) then wear('ManaWall'); end

    -- yours:
end

-- A job ability as you press it (the subjob's too: Call Beast, Reward, Ready on /BST): its own set.
profile.HandleAbility = function()
    local ability = gData.GetAction();
    local own = setFor(ability.Name);
    if (own ~= nil) then gFunc.EquipSet(own); end
    cancels(ability);

    -- yours:
end

profile.HandleItem = function()
    -- yours:
end

-- The subjob's spells: fast cast before the spell starts (and its kind's own, Cure_Precast, Song_Precast)...
profile.HandlePrecast = function()
    local spell = gData.GetAction();
    wear('Precast');
    local kind = skillSets[spell.Skill];
    if (kind ~= nil) then wear(kind .. '_Precast'); end
    local family = familyOf(spell);
    if (family ~= nil) then wear(family .. '_Precast'); end
    cancels(spell);

    -- yours:
end

-- ...then its kind's set, and the spell's own over it, while it casts.
profile.HandleMidcast = function()
    local spell = gData.GetAction();
    local kind = skillSets[spell.Skill];
    if (kind ~= nil) then wear(kind); end
    -- its family's set (Regen II wears Regen), then the spell's own if it has none or you made one
    local family = familyOf(spell);
    if (family ~= nil) then wear(family); end
    local spellSet = string.gsub(spell.Name, '[^%w]', '');
    if (family == nil) or (yours(spellSet)) then wear(spellSet); end
    if (spell.Skill == 'Enhancing Magic') and (bySkill(spell.Name)) then wear('Enhancing_Skill'); end
    -- vanagear's toggles: /vg burst puts Burst over a nuke; /vg nuke acc puts NukeACC over a nuke and Macc
    -- over an enfeeble
    if (spell.Skill == 'Elemental Magic') then
        if (gVana.Toggles.Nuke == 'acc') then wear('NukeACC'); end
        if (gVana.Toggles.Burst) then wear('Burst'); end
    elseif (spell.Skill == 'Enfeebling Magic') and (gVana.Toggles.Nuke == 'acc') then
        wear('Macc');
    end
    -- the obi over a spell whose element is the day's or the weather's: that element's obi if you have
    -- one (Obi_Fire...), else Obi (Hachirin-no-Obi)
    local env = gData.GetEnvironment();
    if (spell.Element ~= nil) and ((spell.Element == env.DayElement) or (spell.Element == env.WeatherElement)) then
        local own = sets['Obi_' .. spell.Element];
        if (own ~= nil) and (next(own) ~= nil) then gFunc.EquipSet(own); else wear('Obi'); end
    end
    -- cast on yourself: the kind's _Self set over it (Cure_Self, Enhancing_Self)
    local target = gData.GetActionTarget();
    if (target ~= nil) and (target.Name == gData.GetPlayer().Name) then
        if (kind ~= nil) then wear(kind .. '_Self'); end
        if (family ~= nil) then wear(family .. '_Self'); end
    end

    -- yours:
end

-- A ranged attack: snapshot gear before the shot...
profile.HandlePreshot = function()
    wear('Preshot');

    -- yours:
end

-- ...then the shot's own gear while it flies.
profile.HandleMidshot = function()
    wear('Midshot');

    -- yours:
end

-- A weapon skill: Ws_Default, then its own set over it.
profile.HandleWeaponskill = function()
    local ws = gData.GetAction();
    if (wsBailout(ws)) then
        gFunc.CancelAction();
        return;
    end
    wear('Ws_Default');
    local own = setFor(ws.Name);
    if (own ~= nil) then gFunc.EquipSet(own); end

    -- yours:
end

return profile;
