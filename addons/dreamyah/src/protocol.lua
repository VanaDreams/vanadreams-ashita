--[[
    DreamyAH wire protocol (packet 0x120).

    Mirrors server/src/common/dreamy_ah_protocol.h byte for byte. If that header
    changes, change ProtocolVersion here too.
]]

local ffi = require('ffi')

-- Redefining a cdef on addon reload raises an error, so guard it.
pcall(ffi.cdef, [[
    typedef struct __attribute__((packed)) {
        uint16_t header;
        uint16_t sync;
        uint8_t  version;
        uint8_t  operation;
        uint8_t  form;
        uint8_t  reserved;
        uint8_t  requestId[8];
        uint16_t itemId;
        uint16_t reserved2;
        uint32_t bidAmount;
    } dreamyah_request_t;

    typedef struct __attribute__((packed)) {
        uint8_t  version;
        uint8_t  operation;
        uint8_t  status;
        uint8_t  form;
        uint8_t  requestId[8];
        uint16_t itemId;
        uint8_t  category;
        uint8_t  reserved;
        uint16_t stackSize;
        uint16_t singleCount;
        uint16_t stackCount;
        uint16_t fragmentIndex;
        uint16_t fragmentCount;
        uint16_t entryCount;
    } dreamyah_response_t;

    typedef struct __attribute__((packed)) {
        uint16_t itemId;
        uint8_t  category;
        uint8_t  reserved;
        uint16_t stackSize;
    } dreamyah_catalog_entry_t;

    typedef struct __attribute__((packed)) {
        uint32_t price;
        uint32_t timestamp;
        char     seller[16];
        char     buyer[16];
    } dreamyah_history_entry_t;
]])

assert(ffi.sizeof('dreamyah_request_t') == 24)
assert(ffi.sizeof('dreamyah_response_t') == 28)
assert(ffi.sizeof('dreamyah_catalog_entry_t') == 6)
assert(ffi.sizeof('dreamyah_history_entry_t') == 40)

local protocol = {}

protocol.ProtocolVersion = 1
protocol.PacketId        = 0x120
protocol.GameHeaderSize  = 4

protocol.Operation = {
    CatalogRequest  = 0x01,
    InspectRequest  = 0x02,
    BidRequest      = 0x03,
    CatalogFragment = 0x81,
    InspectSummary  = 0x82,
    History         = 0x83,
    BidResult       = 0x84,
}

protocol.ItemForm = {
    Single = 0,
    Stack  = 1,
}

protocol.Status = {
    Success                         = 0,
    NoMatchingListing               = 1,
    InsufficientGil                 = 2,
    InventoryFull                   = 3,
    RareConflict                    = 4,
    InvalidItem                     = 5,
    NotAuctionable                  = 6,
    InvalidForm                     = 7,
    AhUnavailable                   = 8,
    RateLimited                     = 9,
    DuplicateRequestPayloadMismatch = 10,
    TransactionFailed               = 11,
    InvalidProtocol                 = 12,
}

local statusText = {
    [protocol.Status.Success]                         = 'Bid successful. The item was delivered.',
    [protocol.Status.NoMatchingListing]               = 'No listing matched that bid.',
    [protocol.Status.InsufficientGil]                 = 'You do not have enough gil.',
    [protocol.Status.InventoryFull]                   = 'Your inventory is full.',
    [protocol.Status.RareConflict]                    = 'You cannot possess another copy of this Rare item.',
    [protocol.Status.InvalidItem]                     = 'The selected item is invalid.',
    [protocol.Status.NotAuctionable]                  = 'This item is not supported by the Auction House.',
    [protocol.Status.InvalidForm]                     = 'The selected Single/Stack form is invalid.',
    [protocol.Status.AhUnavailable]                   = 'The Auction House is unavailable from your current state or zone.',
    [protocol.Status.RateLimited]                     = 'Please wait briefly before submitting another bid.',
    [protocol.Status.DuplicateRequestPayloadMismatch] = 'The server rejected a reused request identifier.',
    [protocol.Status.InvalidProtocol]                 = 'DreamyAH protocol mismatch.',
}

function protocol.statusText(status)
    return statusText[status] or 'The Auction House transaction failed safely.'
end

-- Request ids are 64-bit; Lua numbers cannot hold them, so they travel as 8-byte strings.
local counter = ffi.new('uint64_t[1]')
counter[0] = (ffi.new('uint64_t', os.time()) * 1000ULL) * 65536ULL + 1ULL

local function nextRequestId()
    counter[0] = counter[0] + 1ULL
    return ffi.string(counter, 8)
end

-- Same arithmetic as MakeDreamyAhPacketHeader in protocol.h.
local function packetHeader(bytes)
    local aligned = bit.band(bytes + 3, bit.bnot(3))
    return bit.bor(bit.band(protocol.PacketId, 0x1FF), bit.lshift(aligned / 2, 9))
end

-- Builds and queues a request. Returns the request id (8-byte string) the server will echo.
function protocol.sendRequest(operation, itemId, form, bidAmount)
    local req = ffi.new('dreamyah_request_t')
    req.header    = packetHeader(ffi.sizeof(req))
    req.version   = protocol.ProtocolVersion
    req.operation = operation
    req.form      = form or protocol.ItemForm.Single
    req.itemId    = itemId or 0
    req.bidAmount = bidAmount or 0

    local requestId = nextRequestId()
    ffi.copy(req.requestId, requestId, 8)

    AshitaCore:GetPacketManager():AddOutgoingPacket(protocol.PacketId, ffi.string(req, ffi.sizeof(req)):totable())
    return requestId
end

-- Splits a raw incoming 0x120 packet. Returns nil when it is too short to hold a response header.
-- The returned pointers are only valid while `data` is alive, so callers must keep it referenced.
function protocol.parseResponse(data)
    local headerEnd = protocol.GameHeaderSize + ffi.sizeof('dreamyah_response_t')
    if data == nil or #data < headerEnd then
        return nil
    end

    local base    = ffi.cast('const uint8_t*', data)
    local header  = ffi.cast('const dreamyah_response_t*', base + protocol.GameHeaderSize)
    local payload = base + headerEnd
    return header, payload, #data - headerEnd
end

function protocol.requestIdOf(header)
    return ffi.string(header.requestId, 8)
end

return protocol
