--[[
    Searchable AH catalog. Port of the plugin's catalog.cpp.

    Ranking: exact name (0), name prefix (1), substring (2); ties by normalized name, then item id.
]]

local catalog = {}
catalog.__index = catalog

function catalog.new()
    return setmetatable({ items = {} }, catalog)
end

-- Lowercase, trim, and collapse runs of whitespace/underscores to a single space.
function catalog.normalize(value)
    local out = value:lower():gsub('[%s_]+', ' ')
    out = out:gsub('^ ', ''):gsub(' $', '')
    return out
end

-- items: array of { id, category, stackSize, name, normalizedName }. Sorted by id, duplicates dropped.
function catalog:set(items)
    table.sort(items, function(a, b) return a.id < b.id end)

    local unique = {}
    for _, item in ipairs(items) do
        if #unique == 0 or unique[#unique].id ~= item.id then
            unique[#unique + 1] = item
        end
    end
    self.items = unique
end

function catalog:size()
    return #self.items
end

function catalog:search(query, limit)
    limit = limit or 200

    local needle = catalog.normalize(query)
    if needle == '' then
        return {}
    end

    local matches = {}
    for _, item in ipairs(self.items) do
        local pos = item.normalizedName:find(needle, 1, true)
        if pos ~= nil then
            local rank = 2
            if item.normalizedName == needle then
                rank = 0
            elseif pos == 1 then
                rank = 1
            end
            matches[#matches + 1] = { rank = rank, item = item }
        end
    end

    table.sort(matches, function(a, b)
        if a.rank ~= b.rank then return a.rank < b.rank end
        if a.item.normalizedName ~= b.item.normalizedName then return a.item.normalizedName < b.item.normalizedName end
        return a.item.id < b.item.id
    end)

    local result = {}
    for i = 1, math.min(limit, #matches) do
        result[i] = matches[i].item
    end
    return result
end

return catalog
