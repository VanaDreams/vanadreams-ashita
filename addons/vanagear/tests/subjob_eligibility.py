"""Off-game VanaGear main-or-subjob selection regressions."""
from pathlib import Path
import unittest
from lupa import luajit21

ADDON = Path(__file__).resolve().parent.parent


class SubjobEligibility(unittest.TestCase):
    def test_can_wear_and_auto_builder_follow_job_changes_and_level(self):
        lua = luajit21.LuaRuntime(unpack_returned_tuples=True)
        result = lua.execute(r'''
            local root = ...
            package.path = root .. '/?.lua;' .. package.path
            bit = require('bit')
            function T(t) return t end
            package.preload['encoding'] = function() return {} end
            local slotBit = {}
            for i = 1, 16 do slotBit[i] = bit.lshift(1, i - 1) end
            package.loaded['vg.data'] = {
                SlotBit = slotBit, ModId = function(n) return ({ HASTE_GEAR = 384 })[n] end,
                Items = function() return {} end, Augments = function() return {} end,
                GearSets = function() return {} end, Latents = function() return {} end,
            }
            local bags = require('vg.bags')
            local score = require('vg.score')
            local who = { Job = 1, Sub = 2, Level = 99, Race = 0, Zone = 0 }
            local function piece(id, jobs, level, haste)
                return { Id = id, Equippable = true, Server = {
                    slot = slotBit[5], jobs = jobs, level = level,
                    mods = { [384] = haste },
                } }
            end
            local main = piece(1, 1, 1, 100)
            local sub = piece(2, 2, 1, 300)
            local neither = piece(3, 4, 1, 900)
            local tooHigh = piece(4, 2, 100, 900)
            assert(bags.CanWear(main, 5, who, true))
            assert(bags.CanWear(sub, 5, who, true))
            assert(not bags.CanWear(neither, 5, who, true))
            assert(not bags.CanWear(tooHigh, 5, who, true))
            local mode = score.Compile({ Name = 'Tp_Default', Stats = { HASTE_GEAR = 1 } })
            local function selected()
                local built = score.Build(mode, { main, sub, neither, tooHigh }, who, { 5 }, { 0 }, true)
                return built[5] and built[5].Id or 0
            end
            -- Without the flag, the main job's gear only.
            local mainOnly = score.Build(mode, { main, sub }, who, { 5 }, { 0 })
            assert(mainOnly[5] and mainOnly[5].Id == 1)
            assert(selected() == 2)
            who.Sub = 3
            assert(selected() == 3)
            who.Sub = 0
            assert(selected() == 1)
            who.Job, who.Sub = 2, 1
            assert(selected() == 2)
            who.Level = 100
            assert(selected() == 4)
            return true
        ''', str(ADDON))
        self.assertTrue(result)

    def test_upgrade_suggestions_pass_subjob_eligibility(self):
        lua = luajit21.LuaRuntime(unpack_returned_tuples=True)
        result = lua.execute(r'''
            local root = ...
            package.path = root .. '/?.lua;' .. package.path
            chat = { header = function() return '' end, message = function(s) return s end }
            local withSub = nil
            package.loaded['vg.core'] = {}
            package.loaded['vg.gsnames'] = {}
            package.loaded['vg.use'] = {}
            package.loaded['vg.mine'] = {}
            package.loaded['vg.bags'] = {
                ReadStorage = function() return { { Name = 'sub piece', Bag = 1 } } end,
                CanWear = function(_, _, _, flag) withSub = flag; return flag == true end,
            }
            package.loaded['vg.score'] = { Vector = function() return {} end, Total = function() return 2 end }
            package.loaded['vg.data'] = { PetTypes = {} }
            gData = { Constants = { EquipSlotNames = { [5] = 'Head' } } }
            gVana = { Who = { JobName = 'WAR', Job = 1, Sub = 2, Level = 99 },
                Toggles = {}, Prefs = {}, Explain = { test = { mode = {}, ranks = { [5] = {} } } } }
            local commands = require('vg.commands')
            commands.Vg({ 'upgrades' })
            return withSub
        ''', str(ADDON))
        self.assertTrue(result)


if __name__ == '__main__':
    unittest.main()
