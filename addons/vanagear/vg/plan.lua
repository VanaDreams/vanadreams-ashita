--[[
    vanagear: which sets to build for the job the player is on, and what each one values.
]]--

local roles = require('vg.roles');
local vdata = require('vg.data');

local plan = {};

local function copy(t)
    if (type(t) ~= 'table') then return t; end
    local c = {};
    for k, v in pairs(t) do c[k] = copy(v); end
    return c;
end

-- roles.lua's mode with the job file's tweaks laid over it, stat by stat
local function mode(name, tweaks)
    local m = copy(roles.Modes[name]) or {};
    m.Name = name;
    local t = tweaks and tweaks[name];
    if (t ~= nil) then
        for _, part in ipairs({ 'Stats', 'Caps', 'Pet', 'PetCaps' }) do
            if (t[part] ~= nil) then
                m[part] = m[part] or {};
                for k, v in pairs(t[part]) do m[part][k] = v; end
            end
        end
    end
    return m;
end

local jobCache = {};
plan.Job = function(abbr)
    if (jobCache[abbr] == nil) then
        local path = string.format('%saddons\\vanagear\\jobs\\%s.lua', AshitaCore:GetInstallPath(), abbr);
        local chunk = loadfile(path);
        jobCache[abbr] = chunk and chunk() or { Fighter = true };
    end
    return jobCache[abbr];
end
plan.Forget = function() jobCache = {}; end

-- the server weapon skill of the item worn in a slot, or nil
local function wornSkill(equipment, slotName)
    local e = equipment[slotName];
    if (e == nil) or (e.Item == nil) then return nil; end
    local s = vdata.Items()[e.Item.Id];
    return s and s.skill or nil;
end

-- One weaponskill's mode, from the stats the server's script reads.
local function wsMode(ws, displayName)
    local m = { Name = displayName, Stats = {} };
    local s = m.Stats;
    local scale = 2;
    for stat, v in pairs(ws.wsc) do
        s[stat] = v * scale;
    end
    if (ws.kind == 'magical') then
        s.MATT = 2; s.MACC = 0.6; s.MAGIC_DAMAGE = 1.2;
        s.ALL_WSDMG_FIRST_HIT = 3; s.TP_BONUS = 0.008;
    elseif (ws.kind == 'ranged') then
        s.RATT = 0.6; s.RACC = 0.5; s.ALL_WSDMG_FIRST_HIT = 2.5; s.TP_BONUS = 0.008;
        s.AGI = (s.AGI or 0) + 0.2;
    else
        s.ATT = 0.5; s.ACC = 0.35; s.TP_BONUS = 0.008; s.CRIT_DMG_INCREASE = 0.3;
        if (ws.hits > 1) then
            s.ALL_WSDMG_ALL_HITS = 3; s.ALL_WSDMG_FIRST_HIT = 1.5; s.ANY_FTP_BONUS = 0.1;
            s.DOUBLE_ATTACK = 0.8; s.TRIPLE_ATTACK = 1.2;
        else
            s.ALL_WSDMG_ALL_HITS = 3; s.ALL_WSDMG_FIRST_HIT = 2.5; s.ANY_FTP_BONUS = 0.04;
        end
    end
    return m;
end

-- The set name a weaponskill's set goes by: its name with spaces and punctuation taken out,
-- as the starter profiles already name them (Savage Blade -> SavageBlade).
plan.WsSetName = function(name)
    return (string.gsub(name, '[^%w]', ''));
end

--[[
    Returns a list of { Name = set name, Mode = mode table, Slots = { slot indexes } } and the
    reasons slots were held, for the job, subjob and weapons the player has right now.
    opts.Weapons: let the builder choose Main, Sub and Range.
]]--
plan.For = function(who, equipment, opts)
    local job = plan.Job(who.JobName);
    local tweaks = job.Tweaks or {};
    local names, seen = {}, {};
    local function add(list)
        for _, n in ipairs(list) do
            if (not seen[n]) then seen[n] = true; names[#names + 1] = n; end
        end
    end

    add(roles.Always);
    if (job.Fighter ~= false) then add(roles.Fighter); end

    local skills, haveSkill = {}, {};
    for _, s in ipairs(job.Skills or {}) do
        if (not haveSkill[s]) then haveSkill[s] = true; skills[#skills + 1] = s; end
    end
    for _, s in ipairs(roles.SubSkills[who.SubName] or {}) do
        if (not haveSkill[s]) then haveSkill[s] = true; skills[#skills + 1] = s; end
    end
    if (#skills > 0) then
        add(roles.Caster);
        for _, s in ipairs(skills) do add(roles.SkillSets[s] or {}); end
    end

    local rangeSkill = wornSkill(equipment, 'Range');
    local shooting = (rangeSkill == 25) or (rangeSkill == 26) or (rangeSkill == 27);
    if (job.Shooter) or (shooting) then add(roles.Shooter); end
    if (job.Pet) then add(roles.Pet); end

    -- slots: weapons held unless asked; ammo held under a bow or gun, which shoots it
    local slots, held = {}, {};
    for i = 1, 16 do
        local keep = false;
        if (i <= 3) and (not opts.Weapons) then
            keep = true; held[i] = 'weapons are held (/vg weapons on lets vanagear choose them)';
        elseif (i == 4) and ((rangeSkill == 25) or (rangeSkill == 26)) then
            keep = true; held[i] = 'ammo stays with the bow or gun';
        elseif (opts.Locked ~= nil) and (opts.Locked[i]) then
            keep = true; held[i] = 'locked with /vg lock';
        end
        if (not keep) then slots[#slots + 1] = i; end
    end

    -- dual wield is worth something only with a weapon in the off hand
    local subSkill = wornSkill(equipment, 'Sub');
    local dualWield = (subSkill ~= nil) and (subSkill >= 1) and (subSkill <= 12);

    local out = {};
    for _, n in ipairs(names) do
        local m = mode(n, tweaks);
        if (dualWield) and (string.sub(n, 1, 3) == 'Tp_') then
            m.Stats.DUAL_WIELD = m.Stats.DUAL_WIELD or 1.2;
            m.Caps = m.Caps or {};
            m.Caps.DUAL_WIELD = m.Caps.DUAL_WIELD or 35;
        end
        out[#out + 1] = { Name = n, Mode = m, Slots = slots };
    end

    -- a set for every weaponskill the weapons in hand can use
    local skillsInHand = {};
    local main = wornSkill(equipment, 'Main');
    if (main ~= nil) then skillsInHand[main] = true; end
    if (rangeSkill ~= nil) then skillsInHand[rangeSkill] = true; end
    local resources = AshitaCore:GetResourceManager();
    local player = AshitaCore:GetMemoryManager():GetPlayer();
    local function skillOf(skillId)
        if (player.GetCombatSkill == nil) then return 999; end
        local s = player:GetCombatSkill(skillId);
        return (s ~= nil) and s:GetSkill() or 999;
    end
    for id, ws in pairs(vdata.Weaponskills()) do
        -- only the weaponskills the weapons in hand can use at the skill the player has
        if (skillsInHand[ws.skill]) and (next(ws.wsc) ~= nil) and (ws.level <= skillOf(ws.skill)) then
            local res = resources:GetAbilityById(id);
            local display = (res ~= nil) and res.Name[1] or ws.key;
            local m = wsMode(ws, display);
            out[#out + 1] = { Name = plan.WsSetName(display), Mode = m, Slots = slots, Weaponskill = display };
        end
    end

    return out, held;
end

return plan;
