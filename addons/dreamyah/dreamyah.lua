addon.name    = 'dreamyah'
addon.author  = 'VanaDreams'
addon.version = '1.0'
addon.desc    = 'Searchable VanaDreams Auction House interface.'
addon.link    = ''

require('common')

local ffi      = require('ffi')
local protocol = require('src/protocol')
local catalogs = require('src/catalog')
local ui       = require('src/ui')
local menu     = require('src/menu')

local Operation = protocol.Operation
local Status    = protocol.Status

local settings = require('settings')

local catalog = catalogs.new()

-- User settings live in config/addons/dreamyah/<character>/settings.lua, outside this folder,
-- so updating the addon files never overwrites them.
local defaults = T{
    ahMatch = menu.AhMatch,
}
local config = defaults

ashita.events.register('load', 'dreamyah_load_cb', function()
    config = settings.load(defaults)
    menu.AhMatch = config.ahMatch
end)

ashita.events.register('unload', 'dreamyah_unload_cb', function()
    settings.save()
end)

local state = {
    open             = { false },
    buttonOpen       = { true },
    search          = { '' },
    resultText       = 'Open DreamyAH and search for an item.',
    selected         = nil,
    selectedItemId   = 0,
    category         = 0,
    stackSize        = 1,
    singleCount      = 0,
    stackCount       = 0,
    singleHistory    = {},
    stackHistory     = {},
    selectedForm     = 0,
    bidAmount        = { 1 },
    -- 8-byte request id strings; nil never matches a server reply.
    catalogRequestId = nil,
    inspectRequestId = nil,
    bidRequestId     = nil,
    -- Catalog arrives in fragments; fragments[i] is an array of raw entries, seen[i] marks arrival.
    fragments        = {},
    seen             = {},
}

-- Search results are cached until the query or the catalog changes.
local matchCache = { query = nil, version = 0, items = {} }
local catalogVersion = 0

local function matches()
    local query = state.search[1]
    if matchCache.query ~= query or matchCache.version ~= catalogVersion then
        matchCache.query   = query
        matchCache.version = catalogVersion
        matchCache.items   = catalog:search(query)
    end
    return matchCache.items
end

local function clearSelection()
    state.selected       = nil
    state.selectedItemId = 0
    state.singleHistory  = {}
    state.stackHistory   = {}
end

-- Resource name arrays are 1-based in Lua. The plugin preferred C++ Name[2] and fell back to Name[0],
-- which are Lua Name[3] and Name[1].
local function itemName(itemId)
    local res = AshitaCore:GetResourceManager():GetItemById(itemId)
    if res == nil then
        return nil
    end
    for _, index in ipairs({ 3, 1 }) do
        local ok, name = pcall(function() return res.Name[index] end)
        if ok and type(name) == 'string' and name ~= '' then
            return name
        end
    end
    return nil
end

local function requestCatalog()
    clearSelection()
    state.fragments  = {}
    state.seen       = {}
    state.resultText = 'Synchronizing Auction House catalog...'
    state.catalogRequestId = protocol.sendRequest(Operation.CatalogRequest)
end

local function requestInspect(itemId)
    state.selectedItemId = itemId
    state.singleHistory  = {}
    state.stackHistory   = {}
    state.resultText     = 'Loading availability and sales history...'
    state.inspectRequestId = protocol.sendRequest(Operation.InspectRequest, itemId)
end

local function submitBid()
    local bid = state.bidAmount[1]
    if state.selectedItemId == 0 or bid < 1 or bid > 999999999 then
        state.resultText = 'Enter a bid between 1 and 999,999,999 gil.'
        return
    end
    if state.selectedForm == 1 and state.stackSize <= 1 then
        state.resultText = 'This item cannot be purchased as a stack.'
        return
    end
    state.resultText = 'Submitting bid...'
    local form = state.selectedForm == 1 and protocol.ItemForm.Stack or protocol.ItemForm.Single
    state.bidRequestId = protocol.sendRequest(Operation.BidRequest, state.selectedItemId, form, bid)
end

local function rebuildCatalog()
    local items = {}
    for _, fragment in ipairs(state.fragments) do
        for _, entry in ipairs(fragment) do
            local name = itemName(entry.itemId)
            if name ~= nil then
                items[#items + 1] = {
                    id             = entry.itemId,
                    category       = entry.category,
                    stackSize      = entry.stackSize,
                    name           = name,
                    normalizedName = catalogs.normalize(name),
                }
            end
        end
    end
    catalog:set(items)
    catalogVersion = catalogVersion + 1
    state.resultText = string.format('Catalog synchronized: %d AH-supported items.', catalog:size())
end

local function processCatalog(header, payload, bytes)
    local entrySize = ffi.sizeof('dreamyah_catalog_entry_t')
    local count = header.entryCount
    if protocol.requestIdOf(header) ~= state.catalogRequestId or header.fragmentCount == 0
        or header.fragmentIndex >= header.fragmentCount or bytes < count * entrySize then
        return
    end

    if #state.fragments ~= header.fragmentCount then
        state.fragments = {}
        state.seen      = {}
        for i = 1, header.fragmentCount do
            state.fragments[i] = {}
            state.seen[i]      = false
        end
    end

    local entries  = ffi.cast('const dreamyah_catalog_entry_t*', payload)
    local fragment = {}
    for i = 0, count - 1 do
        fragment[i + 1] = { itemId = entries[i].itemId, category = entries[i].category, stackSize = entries[i].stackSize }
    end
    state.fragments[header.fragmentIndex + 1] = fragment
    state.seen[header.fragmentIndex + 1]      = true

    for i = 1, #state.seen do
        if not state.seen[i] then
            return
        end
    end
    rebuildCatalog()
end

local function processSummary(header)
    if protocol.requestIdOf(header) ~= state.inspectRequestId or header.itemId ~= state.selectedItemId then
        return
    end
    if header.status ~= Status.Success then
        state.resultText = protocol.statusText(header.status)
        return
    end
    state.category    = header.category
    state.stackSize   = header.stackSize
    state.singleCount = header.singleCount
    state.stackCount  = header.stackCount
    if state.stackSize <= 1 then
        state.selectedForm = 0
    end
    state.resultText = 'Availability and history loaded.'
end

local function processHistory(header, payload, bytes)
    local entrySize = ffi.sizeof('dreamyah_history_entry_t')
    local count = header.entryCount
    if protocol.requestIdOf(header) ~= state.inspectRequestId or header.itemId ~= state.selectedItemId
        or bytes < count * entrySize then
        return
    end

    local key = header.form == protocol.ItemForm.Stack and 'stackHistory' or 'singleHistory'
    if header.fragmentIndex == 0 then
        state[key] = {}
    end

    local entries = ffi.cast('const dreamyah_history_entry_t*', payload)
    for i = 0, count - 1 do
        local entry = entries[i]
        table.insert(state[key], {
            price     = entry.price,
            timestamp = entry.timestamp,
            seller    = ffi.string(entry.seller, 16):match('^[^%z]*'),
            buyer     = ffi.string(entry.buyer, 16):match('^[^%z]*'),
        })
    end
end

local function processBidResult(header)
    if protocol.requestIdOf(header) ~= state.bidRequestId then
        return
    end
    state.resultText = protocol.statusText(header.status)
    if header.status == Status.Success then
        requestInspect(header.itemId)
        state.resultText = 'Bid successful. Item delivered; refreshing AH information...'
    end
end

local function toggleWindow()
    state.open[1] = not state.open[1]
    if state.open[1] and catalog:size() == 0 then
        requestCatalog()
    end
end

local actions = {
    toggleWindow   = toggleWindow,
    requestCatalog = requestCatalog,
    inspect        = function(item) state.selected = item; requestInspect(item.id) end,
    submitBid      = submitBid,
    searchChanged  = clearSelection,
    matches        = matches,
}

ashita.events.register('command', 'dreamyah_command_cb', function(e)
    local args = e.command:args()
    if #args == 0 then
        return
    end
    local word = args[1]:lower()
    if word ~= '/dreamyah' and word ~= '/ahsearch' then
        return
    end

    e.blocked = true

    -- `/dreamyah menu` prints the current menu name, to check what the AH is called on this client.
    if args[2] ~= nil and args[2]:lower() == 'menu' then
        print(string.format('[dreamyah] current menu: "%s" (AH match "%s", open = %s)',
            menu.currentName(), menu.AhMatch, tostring(menu.isAuctionHouseOpen())))
        return
    end

    -- `/dreamyah match <text>` changes which menu names count as the Auction House, and keeps it.
    if args[2] ~= nil and args[2]:lower() == 'match' and args[3] ~= nil then
        config.ahMatch = args[3]:lower()
        menu.AhMatch   = config.ahMatch
        settings.save()
        print(string.format('[dreamyah] AH menu match set to "%s"', menu.AhMatch))
        return
    end

    toggleWindow()
end)

ashita.events.register('packet_in', 'dreamyah_packet_in_cb', function(e)
    if e.id ~= protocol.PacketId then
        return
    end

    -- Never pass DreamyAH packets to the stock client.
    e.blocked = true

    local header, payload, bytes = protocol.parseResponse(e.data)
    if header == nil then
        return
    end
    if header.version ~= protocol.ProtocolVersion then
        state.resultText = 'DreamyAH server/plugin protocol mismatch.'
        return
    end

    local op = header.operation
    if op == Operation.CatalogFragment then
        processCatalog(header, payload, bytes)
    elseif op == Operation.InspectSummary then
        processSummary(header)
    elseif op == Operation.History then
        processHistory(header, payload, bytes)
    elseif op == Operation.BidResult then
        processBidResult(header)
    end
end)

ashita.events.register('d3d_present', 'dreamyah_d3d_present_cb', function()
    if menu.isAuctionHouseOpen() then
        ui.drawButton(state, actions)
    end
    ui.draw(state, actions)
end)
