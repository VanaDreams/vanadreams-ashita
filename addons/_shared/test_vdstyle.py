#!/usr/bin/env python3
"""Checks vdstyle.lua off-game, under LuaJIT (pip install lupa).

    python addons/_shared/test_vdstyle.py [more .lua files to syntax-check ...]

A stub imgui records every push and pop, so the test proves vd.Begin/vd.End, vd.Button and
vd.Selectable leave ImGui's style stacks exactly as they found them (ImGui asserts and Ashita
drops the frame otherwise), whether the window is shown, collapsed, nested, or the draw list fails.
Also compiles each addon file that uses it.
"""
import pathlib
import sys

from lupa import luajit21 as lupa

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent.parent
USERS = ['vanagear/vg/window.lua', 'vanagear/config.lua', 'vanagear/setdisplay.lua',
         'crafty/crafty.lua', 'vanafish/vanafish.lua', 'dreamyah/src/ui.lua']

STUB = r"""
local calls = {}
local depth = { color = 0, var = 0, clip = 0 }
local stub = { calls = calls, depth = depth, shown = true, failDraw = false, scrollMax = 0 }
local function rec(name) calls[#calls + 1] = name end
local dl = {}
for _, m in ipairs({ 'PushClipRect', 'PopClipRect', 'AddRectFilledMultiColor', 'AddLine', 'AddRect',
                     'AddCircleFilled', 'AddTriangleFilled' }) do
    dl[m] = function(self, ...)
        assert(self == dl, m .. ' must be called with :')
        if stub.failDraw then error('no draw list here') end
        rec(m)
    end
end
local imgui = {
    PushStyleColor = function(id, c) assert(type(id) == 'number' and type(c) == 'table' and #c == 4); depth.color = depth.color + 1; if id == ImGuiCol_WindowBg then stub.lastBg = c[4] end end,
    PopStyleColor = function(n) depth.color = depth.color - (n or 1); assert(depth.color >= 0, 'popped too many colours') end,
    PushStyleVar = function(id, v) assert(type(id) == 'number' and v ~= nil); depth.var = depth.var + 1 end,
    PopStyleVar = function(n) depth.var = depth.var - (n or 1); assert(depth.var >= 0, 'popped too many vars') end,
    Begin = function(title, open, flags) rec('Begin'); return stub.shown end,
    End = function() rec('End') end,
    GetWindowDrawList = function() return dl end,
    GetWindowPos = function() return 100, 50 end,
    GetWindowSize = function() return 400, 300 end,
    GetFrameHeight = function() return 19 end,
    GetFontSize = function() return 13 end,
    GetScrollMaxY = function() return stub.scrollMax end,
    GetStyle = function() return { ScrollbarSize = 10, FramePadding = { x = 4, y = 3 } } end,
    GetColorU32 = function(c) assert(type(c) == 'table' and #c == 4); return 0xFFFFFFFF end,
    Button = function(label, size) rec('Button'); return label == 'click' end,
    SmallButton = function(label) rec('SmallButton'); return stub.clickSee and label:find('##vdstyle_see', 1, true) ~= nil end,
    CalcTextSize = function(t) return #t * 7, 13 end,
    SetCursorScreenPos = function(p) assert(type(p) == 'table' and #p == 2) end,
    PushClipRect = function(a, b, i) depth.clip = depth.clip + 1 end,
    PopClipRect = function() depth.clip = depth.clip - 1; assert(depth.clip >= 0) end,
    SetTooltip = function(t) end,
    Selectable = function(label, sel) rec('Selectable'); return false end,
    IsItemHovered = function() return true end,
    GetCursorScreenPos = function() return 110, 80 end,
    GetCursorPosX = function() return 8 end,
    SetCursorPosX = function(x) end,
    GetTextLineHeight = function() return 13 end,
    TextColored = function(c, t) assert(#c == 4) end,
    TextDisabled = function(t) end,
}
local n = 0
for _, k in ipairs({ 'Text', 'TextDisabled', 'WindowBg', 'ChildBg', 'PopupBg', 'Border', 'BorderShadow',
    'FrameBg', 'FrameBgHovered', 'FrameBgActive', 'TitleBg', 'TitleBgActive', 'TitleBgCollapsed', 'MenuBarBg',
    'ScrollbarBg', 'ScrollbarGrab', 'ScrollbarGrabHovered', 'ScrollbarGrabActive', 'CheckMark', 'SliderGrab',
    'SliderGrabActive', 'Button', 'ButtonHovered', 'ButtonActive', 'Header', 'HeaderHovered', 'HeaderActive',
    'Separator', 'SeparatorHovered', 'SeparatorActive', 'ResizeGrip', 'ResizeGripHovered', 'ResizeGripActive',
    'Tab', 'TabHovered', 'TabSelected', 'PlotHistogram', 'PlotHistogramHovered', 'TableHeaderBg',
    'TableBorderStrong', 'TableBorderLight', 'TableRowBgAlt', 'TextSelectedBg', 'DragDropTarget' }) do
    _G['ImGuiCol_' .. k] = n; n = n + 1   -- ImGuiCol_TabActive left out on purpose: a missing name is skipped
end
for _, k in ipairs({ 'WindowRounding', 'ChildRounding', 'FrameRounding', 'PopupRounding', 'ScrollbarRounding',
    'GrabRounding', 'WindowBorderSize', 'ChildBorderSize', 'FrameBorderSize', 'PopupBorderSize', 'ScrollbarSize',
    'FramePadding' }) do
    _G['ImGuiStyleVar_' .. k] = n; n = n + 1
end
ImGuiWindowFlags_NoTitleBar = 1
bit = { band = function(a, b) return (a % (2 * b) >= b) and b or 0 end }
package.loaded['imgui'] = imgui
local cfgdir = ...
AshitaCore = { GetInstallPath = function() return cfgdir end }
ashita = { fs = { exists = function(p) return true end, create_directory = function(p) return true end } }
addon = { name = 'testaddon' }
stub.bgAlpha = function()
    -- the WindowBg colour is the 3rd colour pushed by Begin
    return stub.lastBg
end
package.loaded['common'] = true
return stub
"""

TEST = r"""
local stub, vd = ...
local d = stub.depth
local function balanced(what) assert(d.color == 0 and d.var == 0 and d.clip == 0, what .. ': stacks left at ' .. d.color .. '/' .. d.var .. '/' .. d.clip) end

-- shown, with a title and a close button
assert(vd.Begin('vanagear', { true }) == true)
balanced_inside = d.color > 0 and d.var > 0
assert(balanced_inside, 'Begin pushed nothing')
vd.End(); balanced('shown window')

-- collapsed: Begin false, End still pops
stub.shown = false
assert(vd.Begin('x', { true }) == false); vd.End(); balanced('collapsed window')
stub.shown = true

-- no title bar, flags, see-through
vd.Begin('##button', { true }, ImGuiWindowFlags_NoTitleBar, { alpha = 0.55 }); vd.End(); balanced('no title bar')

-- nested windows (vanagear draws its commands window after its main one)
vd.Begin('a', { true }); vd.Begin('b', { true }); vd.End(); vd.End(); balanced('nested')

-- a scroll bar showing, and a broken draw list: the window still opens and pops
stub.scrollMax = 40; vd.Begin('s', { true }); vd.End(); balanced('scrolling')
stub.failDraw = true; assert(vd.Begin('f', { true })); vd.End(); balanced('draw list error')
stub.failDraw = false

-- buttons and rows
assert(vd.Button('click') == true); assert(vd.Button('no', { 80, 26 }, true) == false); balanced('buttons')
vd.SmallButton('s'); balanced('small button')
vd.Selectable('Idle', true); vd.Selectable('Call', false, 0); vd.Selectable('x', false, 0, { 0, 0 }); balanced('rows')
vd.Eyebrow('IDLE'); vd.Status('+3.1', vd.Ok); vd.Muted('m')

-- the see-through switch: solid by default, a click flips it, the choice is saved per window id
vd.Begin('vanatunes 0.3.4###vanatunes', { true }); vd.End(); balanced('switch shown')
assert(stub.lastBg > 0.9, 'starts solid')
stub.clickSee = true; vd.Begin('vanatunes 0.3.4###vanatunes', { true }); vd.End(); stub.clickSee = false; balanced('switch clicked')
vd.Begin('vanatunes 0.3.5###vanatunes', { true }); vd.End()
assert(stub.lastBg < 0.6, 'see-through after the click, kept across a version in the title')
vd.Begin('Vanadreams crafting', { true }, nil, { seeThrough = true }); vd.End()
assert(stub.lastBg < 0.6, 'a window can start see-through')
vd.Begin('##button', { true }, ImGuiWindowFlags_NoTitleBar); vd.End()
assert(stub.lastBg > 0.9, 'no title bar: no switch, solid')

-- the box was drawn
local seen = {}
for _, c in ipairs(stub.calls) do seen[c] = true end
for _, m in ipairs({ 'AddRectFilledMultiColor', 'AddRect', 'AddLine', 'AddCircleFilled', 'AddTriangleFilled', 'PopClipRect' }) do
    assert(seen[m], m .. ' never called')
end
return 'ok'
"""


def main(extra):
    import tempfile
    tmp = tempfile.mkdtemp()
    lua = lupa.LuaRuntime()
    stub = lua.execute(STUB, tmp + '/')
    lua.globals().package.path = str(HERE / '?.lua') + ';' + lua.globals().package.path
    vd = lua.eval("require('vdstyle')")
    result = lua.eval('function(...) return (function(...) ' + TEST + ' end)(...) end')(stub, vd)
    saved = pathlib.Path(tmp, 'config\\addons\\testaddon\\vdstyle.txt')
    assert saved.read_text().strip() == '###vanatunes=see', saved.read_text()
    print('vdstyle', vd.VERSION, result)
    bad = 0
    files = [ROOT / 'addons' / f for f in USERS] + [pathlib.Path(p) for p in extra]
    for f in files:
        ok, err = lua.eval('function(p) local f, e = loadfile(p); return f ~= nil, e end')(str(f))
        if not ok:
            print('does not compile:', err); bad += 1
    print(len(files), 'addon files compile' if not bad else 'files had errors')
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
