--[[
    Reads the name of the game's current menu so the addon knows when the Auction House is open.

    Same pointer walk the autologin addon uses: menu pointer -> object -> header -> name at +0x46.
]]

local menu = {}

-- Any menu whose name contains this counts as the Auction House.
-- If the button never appears, run `/dreamyah menu` at the AH and put the printed name's key part here.
menu.AhMatch = 'auc'

-- Returns the current menu name, trimmed and lowercased, or '' when no menu is open.
function menu.currentName()
    local ok, name = pcall(function()
        local ptr = AshitaCore:GetPointerManager():Get('menu')
        if ptr == 0 then return '' end
        ptr = ashita.memory.read_uint32(ptr)
        if ptr == 0 then return '' end
        ptr = ashita.memory.read_uint32(ptr)
        if ptr == 0 then return '' end
        ptr = ashita.memory.read_uint32(ptr + 0x04)
        if ptr == 0 then return '' end
        return ashita.memory.read_string(ptr + 0x46, 16)
    end)

    if not ok or type(name) ~= 'string' then
        return ''
    end
    return (name:gsub('%z', ''):gsub('^%s+', ''):gsub('%s+$', ''):lower())
end

function menu.isAuctionHouseOpen()
    return menu.currentName():find(menu.AhMatch, 1, true) ~= nil
end

return menu
