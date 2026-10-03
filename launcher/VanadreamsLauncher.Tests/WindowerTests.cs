using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class WindowerInitTests
    {
        [TestMethod]
        public void Bind_through_input_becomes_an_ashita_bind_with_shift_as_plus()
        {
            var r = WindowerInit.Convert("bind ~f1 input /ma \"Cure IV\" <stpt>\nbind ^!numpad1 input /ja \"Provoke\" <t>");
            CollectionAssert.AreEqual(new[] { "/bind +f1 /ma \"Cure IV\" <stpt>", "/bind ^!numpad1 /ja \"Provoke\" <t>" }, r.Lines);
            Assert.AreEqual(2, r.Binds);
        }

        [TestMethod]
        public void A_bare_game_command_and_an_up_bind_carry_over()
        {
            var r = WindowerInit.Convert("bind f9 /heal\nbind f10 up input /echo released");
            CollectionAssert.AreEqual(new[] { "/bind f9 /heal", "/bind f10 up /echo released" }, r.Lines);
        }

        [TestMethod]
        public void Chat_closed_binds_turn_on_bind_block_once()
        {
            var r = WindowerInit.Convert("bind %w input /ws \"Savage Blade\" <t>\nbind %e input /ja \"Berserk\" <me>");
            StringAssert.StartsWith(r.Lines[0], "#");
            Assert.AreEqual("/bind block 1", r.Lines[1]);
            Assert.AreEqual("/bind w /ws \"Savage Blade\" <t>", r.Lines[2]);
            Assert.AreEqual(4, r.Lines.Count);
        }

        [TestMethod]
        public void Windower_commands_and_chains_are_kept_as_comments_saying_why()
        {
            var r = WindowerInit.Convert("bind f9 gs c cycle OffenseMode\nbind f11 input /ja \"Provoke\" <t>; wait 1; input /echo x\nconsole_displayactivity 1");
            Assert.AreEqual(0, r.Binds);
            Assert.AreEqual(3, r.Skipped.Count);
            StringAssert.StartsWith(r.Lines[0], "# not carried over (a Windower command, not a game command): bind f9 gs c");
            StringAssert.StartsWith(r.Lines[1], "# not carried over (more than one command in a row)");
            StringAssert.StartsWith(r.Lines[2], "# not carried over (a Windower setting with no Ashita line)");
        }

        [TestMethod]
        public void Aliases_become_slash_aliases_and_loaded_addons_are_listed()
        {
            var r = WindowerInit.Convert("alias rr input /ma \"Reraise\" <me>\nlua load gearswap\nlua l XIVParty\nload timestamp\nalias x gs c toggle");
            Assert.AreEqual("/alias add /rr /ma \"Reraise\" <me>", r.Lines[0]);
            Assert.AreEqual(1, r.Aliases);
            CollectionAssert.AreEqual(new[] { "gearswap", "XIVParty", "timestamp" }, r.WindowerAddons);
            StringAssert.StartsWith(r.Lines[1], "# not carried over");
        }

        [TestMethod]
        public void A_quoted_command_is_unquoted()
        {
            var r = WindowerInit.Convert("bind ^f1 \"input /ma \\\"Cure\\\" <t>\"");
            Assert.AreEqual("/bind ^f1 /ma \"Cure\" <t>", r.Lines[0]);
        }

        [TestMethod]
        public void Merge_puts_the_block_under_yours_and_replaces_an_earlier_one()
        {
            var script = ScriptWriter.Build(new List<ScriptEntry>(), "") ;
            script = script + "/bind f12 /fps" + Environment.NewLine;
            var once = WindowerInit.MergeIntoScript(script, new[] { "/bind f1 /heal" });
            var twice = WindowerInit.MergeIntoScript(once, new[] { "/bind f2 /sit" });
            Assert.IsFalse(twice.Contains("/bind f1 /heal"));
            Assert.IsTrue(twice.Contains("/bind f2 /sit"));
            Assert.IsTrue(twice.Contains("/bind f12 /fps"));
            Assert.AreEqual(1, twice.Split('\n').Count(l => l.Trim() == WindowerInit.BeginMarker));
            // the launcher's next save keeps it: it is in the player's part of the script
            var resaved = ScriptWriter.Build(new List<ScriptEntry>(), twice);
            Assert.IsTrue(resaved.Contains("/bind f2 /sit"));
            Assert.IsTrue(resaved.IndexOf(ScriptWriter.YoursMarker) < resaved.IndexOf("/bind f2 /sit"));
        }

        [TestMethod]
        public void Preset_offers_only_what_the_catalogue_has()
        {
            var cat = new Catalog();
            cat.Items.Add(new CatalogItem { Id = "luashitacast", Name = "LuAshitacast", Source = SourceType.GithubRelease });
            cat.Items.Add(new CatalogItem { Id = "xiui", Name = "XIUI", Source = SourceType.None });
            cat.Items.Add(new CatalogItem { Id = "timestamp", Name = "timestamp", Source = SourceType.Bundled });
            CollectionAssert.AreEqual(new[] { "luashitacast", "timestamp" }, WindowerPreset.CatalogItems(cat).Select(i => i.Id).ToList());
            Assert.AreEqual("xiui", WindowerPreset.For("xivparty").CatalogId);
        }
    }

    [TestClass]
    public class GearSwapImportTests
    {
        private static GearSwapJob Read(string lua, string file = "Ferrin_WHM.lua") =>
            GearSwapImport.Read("Ferrin", "WHM", new[] { new KeyValuePair<string, string>(file, lua) });

        private static Dictionary<string, string> Slots(GearSwapJob job, string set) =>
            job.Sets.Single(s => s.Name == set).Slots.ToDictionary(kv => kv.Key, kv => kv.Value.Name);

        [TestMethod]
        public void Job_and_character_come_from_the_file_name()
        {
            Assert.AreEqual("WHM", GearSwapImport.JobOf("Ferrin_WHM.lua"));
            Assert.AreEqual("WHM", GearSwapImport.JobOf("whm.lua"));
            Assert.AreEqual("BLU", GearSwapImport.JobOf("Ferrin_BLU_gear.lua"));
            Assert.IsNull(GearSwapImport.JobOf("Mote-Include.lua"));
            Assert.AreEqual("Ferrin", GearSwapImport.CharacterOf("Ferrin_WHM.lua"));
            Assert.IsNull(GearSwapImport.CharacterOf("WHM.lua"));
            Assert.IsNull(GearSwapImport.CharacterOf("WHM_gear.lua"));
            Assert.AreEqual("Ferrin", GearSwapImport.CharacterFor(System.IO.Path.Combine("Windower4", "addons", "GearSwap", "data", "Ferrin", "WHM.lua")));
            Assert.IsNull(GearSwapImport.CharacterFor(System.IO.Path.Combine("Windower4", "addons", "GearSwap", "data", "WHM.lua")));
        }

        [TestMethod]
        public void Plain_sets_inside_get_sets_are_read_with_slot_names_mapped()
        {
            var job = Read(@"
function get_sets()
    sets.idle = {main=""Bolelabunga"", left_ear='Ethereal Earring', rring=""Defending Ring"", ranged=""Fail-Not""}
    sets.precast.FC = {head=""Nahtirah Hat""} -- fast cast
end
function precast(spell)
    if spell.type == 'WeaponSkill' then equip(sets.precast.WS) end
end");
            var idle = Slots(job, "idle");
            Assert.AreEqual("Bolelabunga", idle["Main"]);
            Assert.AreEqual("Ethereal Earring", idle["Ear1"]);
            Assert.AreEqual("Defending Ring", idle["Ring2"]);
            Assert.AreEqual("Fail-Not", idle["Range"]);
            Assert.AreEqual("Nahtirah Hat", Slots(job, "precast.FC")["Head"]);
            CollectionAssert.AreEqual(new[] { "Main", "Range", "Ear1", "Ring2" }, job.Sets.Single(s => s.Name == "idle").Slots.Select(kv => kv.Key).ToList());
        }

        [TestMethod]
        public void Set_combine_copies_and_lets_the_later_slot_win_whatever_it_is_called()
        {
            var job = Read(@"
sets.precast.FC = {head='A', ear1='E1', waist='W'}
sets.precast.FC['Enhancing Magic'] = set_combine(sets.precast.FC, {waist='Siegel Sash', left_ear='E2'})
sets.precast.FC.Cure = sets.precast.FC['Enhancing Magic']");
            var enh = Slots(job, "precast.FC.Enhancing Magic");
            Assert.AreEqual("Siegel Sash", enh["Waist"]);
            Assert.AreEqual("E2", enh["Ear1"]);
            Assert.AreEqual("A", enh["Head"]);
            Assert.AreEqual("Siegel Sash", Slots(job, "precast.FC.Cure")["Waist"]);
            Assert.AreEqual("W", Slots(job, "precast.FC")["Waist"]);
        }

        [TestMethod]
        public void Locals_gear_names_items_with_augments_and_empty_are_followed()
        {
            var job = Read(@"
local cape = { name=""Alaunus's Cape"", augments={'MND+20','""Fast Cast""+10',}}
gear.default.weaponskill_neck = ""Fotia Gorget""
sets.precast.WS = {neck=gear.default.weaponskill_neck, back=cape, sub=empty, ammo=gear.ElementalAmmo}");
            var ws = job.Sets.Single(s => s.Name == "precast.WS");
            var d = ws.Slots.ToDictionary(kv => kv.Key, kv => kv.Value);
            Assert.AreEqual("Fotia Gorget", d["Neck"].Name);
            Assert.AreEqual("Alaunus's Cape", d["Back"].Name);
            CollectionAssert.AreEqual(new[] { "MND+20", "\"Fast Cast\"+10" }, d["Back"].Augments);
            Assert.AreEqual("remove", d["Sub"].Name);
            Assert.IsFalse(d.ContainsKey("Ammo"));
            Assert.IsTrue(job.Unresolved.Contains("precast.WS Ammo: gear.ElementalAmmo"));
        }

        [TestMethod]
        public void A_gear_file_is_read_after_the_job_file()
        {
            var job = GearSwapImport.Read("Ferrin", "WAR", new[]
            {
                new KeyValuePair<string, string>("Ferrin_WAR_gear.lua", "function init_gear_sets() sets.engaged = {head='Gear file'} end"),
                new KeyValuePair<string, string>("Ferrin_WAR.lua", "sets.engaged = {head='Job file'}"),
            });
            Assert.AreEqual("Gear file", Slots(job, "engaged")["Head"]);
        }

        [TestMethod]
        public void Code_the_reader_does_not_follow_is_stepped_over()
        {
            var job = Read(@"
--[[ a long
comment with sets.bogus = {head='No'} in it ]]
function job_setup()
    state.OffenseMode:options('Normal', 'Acc')
    if player.sub_job == 'NIN' then sets.engaged = set_combine(sets.engaged, {ear1='Suppanomimi'}) end
    for i, v in ipairs({1, 2, 3}) do local x = i * 2 end
    send_command('bind f9 gs c cycle OffenseMode')
end
sets.engaged.Acc = {head=""Ayanmo Zucchetto +2"", body=[[Ayanmo Corazza +2]]}
local function f() return sets.engaged end
");
            Assert.IsFalse(job.Sets.Any(s => s.Name == "bogus"));
            // unbalanced code ends the read of that statement, not the whole file
            var odd = Read("x = f(a, } )\nsets.idle = {head='Still read'}");
            Assert.AreEqual("Still read", Slots(odd, "idle")["Head"]);
            Assert.AreEqual("Suppanomimi", Slots(job, "engaged")["Ear1"]);
            Assert.AreEqual("Ayanmo Corazza +2", Slots(job, "engaged.Acc")["Body"]);
        }

        [TestMethod]
        public void The_profile_is_a_luashitacast_profile_with_every_set()
        {
            var job = Read("sets.idle = {head=\"O'Brien Hat\", body=\"Back\\\\slash\"}\nsets.precast.WS['Savage Blade'] = {neck='Fotia Gorget'}");
            var lua = GearSwapImport.WriteProfile(job, "2026-10-03");
            StringAssert.Contains(lua, "local sets = {");
            StringAssert.Contains(lua, "profile.Sets = sets;");
            StringAssert.Contains(lua, "    ['idle'] = {");
            StringAssert.Contains(lua, "        Head = 'O\\'Brien Hat',");
            StringAssert.Contains(lua, "        Body = 'Back\\\\slash',");
            StringAssert.Contains(lua, "    ['precast.WS.Savage Blade'] = {");
            StringAssert.Contains(lua, "profile.HandleWeaponskill = function()");
            Assert.IsTrue(lua.TrimEnd().EndsWith("return profile;"));
            Assert.AreEqual("Ferrin_WHM.lua", GearSwapImport.ProfileFileName(job));
        }
    }
}
