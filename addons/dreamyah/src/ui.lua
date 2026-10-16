--[[
    DreamyAH window. Port of DreamyAhPlugin::DrawUi.

    ui.draw(state, actions) draws one frame. `state` is owned by dreamyah.lua; `actions` is a table of
    callbacks { requestCatalog, inspect, submitBid, searchChanged }.
]]

require('common')
local imgui = require('imgui')
local vd = require('vdstyle')

local ui = {}

local TITLE = 'Dreamy A.H. - Your wishlist is here'

local function drawHistory(title, history)
    imgui.Separator()
    vd.Eyebrow(title)
    if #history == 0 then
        imgui.Text('No completed sales recorded.')
        return
    end
    for _, sale in ipairs(history) do
        imgui.Text(string.format('%u gil | %s | %s -> %s', sale.price, os.date('%Y-%m-%d %H:%M', sale.timestamp), sale.seller, sale.buyer))
    end
end

-- Small draggable button shown only while the Auction House menu is open.
-- Its position is remembered between sessions by ImGui.
function ui.drawButton(state, actions)
    imgui.SetNextWindowPos({ 20, 200 }, ImGuiCond_FirstUseEver)
    local flags = bit.bor(ImGuiWindowFlags_NoTitleBar, ImGuiWindowFlags_AlwaysAutoResize, ImGuiWindowFlags_NoCollapse)
    if vd.Begin('##dreamyah_button', state.buttonOpen, flags) then
        if vd.Button(state.open[1] and 'Close AH Search' or 'AH Search', nil, true) then
            actions.toggleWindow()
        end
    end
    vd.End()
end

function ui.draw(state, actions)
    if not state.open[1] then
        return
    end

    imgui.SetNextWindowSize({ 760, 620 }, ImGuiCond_FirstUseEver)
    if vd.Begin(TITLE, state.open) then
        if imgui.InputText('##dreamyah_search', state.search, 128) then
            actions.searchChanged()
        end
        imgui.SameLine()
        if vd.Button('Refresh Catalog') then
            actions.requestCatalog()
        end
        imgui.Text(state.resultText)
        imgui.Separator()

        local matches = actions.matches()

        imgui.BeginChild('dreamyah_results', { 300, 0 }, ImGuiChildFlags_Borders)
        vd.Eyebrow(string.format('Results (%u)', #matches))
        for _, item in ipairs(matches) do
            if vd.Selectable(string.format('%s##%d', item.name, item.id), state.selectedItemId == item.id) then
                actions.inspect(item)
            end
        end
        imgui.EndChild()

        imgui.SameLine()

        imgui.BeginChild('dreamyah_details', { 0, 0 }, ImGuiChildFlags_Borders)
        if state.selectedItemId == 0 then
            imgui.TextWrapped('Search and select an Auction House item.')
        else
            local name = state.selected and state.selected.name or 'Selected item'
            vd.Eyebrow(name)
            imgui.Text(string.format('Item ID: %u   AH category: %u   Stack size: %u', state.selectedItemId, state.category, state.stackSize))
            imgui.Text(string.format('Available Singles: %u', state.singleCount))
            if state.stackSize > 1 then
                imgui.Text(string.format('Available Stacks: %u', state.stackCount))
            end

            imgui.Separator()
            vd.Eyebrow('Bid')
            if imgui.RadioButton('Single', state.selectedForm == 0) then
                state.selectedForm = 0
            end
            if state.stackSize > 1 then
                imgui.SameLine()
                if imgui.RadioButton('Stack', state.selectedForm == 1) then
                    state.selectedForm = 1
                end
            end
            imgui.InputInt('Bid amount', state.bidAmount, 1, 1000)
            state.bidAmount[1] = math.max(1, math.min(999999999, state.bidAmount[1]))
            if vd.Button('Bid', nil, true) then
                actions.submitBid()
            end

            drawHistory('Recent Single Sales', state.singleHistory)
            if state.stackSize > 1 then
                drawHistory('Recent Stack Sales', state.stackHistory)
            end
        end
        imgui.EndChild()
    end
    vd.End()
end

return ui
