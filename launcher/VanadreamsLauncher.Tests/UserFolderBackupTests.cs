using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vanadreams.Services;

namespace Vanadreams.Tests
{
    [TestClass]
    public class UserFolderBackupTests
    {
        private string _dir, _game, _backups;

        [TestInitialize]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vdl-userbak-" + Guid.NewGuid().ToString("N"));
            _game = Path.Combine(_dir, "FINAL FANTASY XI");
            _backups = Path.Combine(_dir, "backups");
            Directory.CreateDirectory(Path.Combine(_game, "USER", "1a2b"));
            File.WriteAllText(Path.Combine(_game, "USER", "1a2b", "mcr.dat"), "macros");
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        [TestMethod]
        public void Take_ZipsEveryCharacterFolder()
        {
            var zip = UserFolderBackup.Take(_game, _backups, new DateTime(2026, 10, 15, 12, 0, 0));
            Assert.IsNotNull(zip);
            using (var a = ZipFile.OpenRead(zip))
                CollectionAssert.AreEqual(new[] { "1a2b/mcr.dat" }, a.Entries.Select(e => e.FullName).ToArray());
        }

        [TestMethod]
        public void Take_SkipsWhenNothingChanged()
        {
            Assert.IsNotNull(UserFolderBackup.Take(_game, _backups, new DateTime(2026, 10, 15, 12, 0, 0)));
            Assert.IsNull(UserFolderBackup.Take(_game, _backups, new DateTime(2026, 10, 15, 13, 0, 0)));
            File.WriteAllText(Path.Combine(_game, "USER", "1a2b", "mcr1.dat"), "more");
            Assert.IsNotNull(UserFolderBackup.Take(_game, _backups, new DateTime(2026, 10, 15, 14, 0, 0)));
            Assert.AreEqual(2, UserFolderBackup.Copies(_backups).Count);
        }

        [TestMethod]
        public void Take_KeepsOnlyTheNewestCopies()
        {
            for (var i = 0; i < UserFolderBackup.Keep + 3; i++)
            {
                File.WriteAllText(Path.Combine(_game, "USER", "1a2b", "mcr.dat"), "macros " + i);
                File.SetLastWriteTimeUtc(Path.Combine(_game, "USER", "1a2b", "mcr.dat"), new DateTime(2026, 10, 1).AddMinutes(i));
                UserFolderBackup.Take(_game, _backups, new DateTime(2026, 10, 15, 12, 0, 0).AddMinutes(i));
            }
            var copies = UserFolderBackup.Copies(_backups);
            Assert.AreEqual(UserFolderBackup.Keep, copies.Count);
            StringAssert.EndsWith(copies[0], "USER-20261015-121200.zip");
        }

        [TestMethod]
        public void CarryOver_TakesNewerAndMissingFilesOnly()
        {
            var other = Path.Combine(_dir, "other", "FINAL FANTASY XI");
            Directory.CreateDirectory(Path.Combine(other, "USER", "1a2b"));
            var mine = Path.Combine(_game, "USER", "1a2b", "mcr.dat");
            File.SetLastWriteTimeUtc(mine, new DateTime(2026, 10, 13));
            var older = Path.Combine(other, "USER", "1a2b", "mcr.dat");
            File.WriteAllText(older, "old macros");
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 10, 12));
            var extra = Path.Combine(other, "USER", "1a2b", "mcr10.dat");
            File.WriteAllText(extra, "book 10");
            File.SetLastWriteTimeUtc(extra, new DateTime(2026, 10, 15));

            CollectionAssert.AreEqual(new[] { "1a2b" }, UserFolderBackup.CarryOver(other, _game).ToArray());
            Assert.AreEqual("macros", File.ReadAllText(mine));   // the newer copy here is kept
            Assert.AreEqual("book 10", File.ReadAllText(Path.Combine(_game, "USER", "1a2b", "mcr10.dat")));
            Assert.AreEqual(0, UserFolderBackup.CarryOver(other, _game).Count);   // nothing left to bring
        }

        [TestMethod]
        public void CarryOver_SameGameOrNoUser()
        {
            Assert.AreEqual(0, UserFolderBackup.CarryOver(_game, _game).Count);
            Assert.AreEqual(0, UserFolderBackup.CarryOver(Path.Combine(_dir, "nowhere"), _game).Count);
        }

        [TestMethod]
        public void Take_NothingToCopy()
        {
            Assert.IsNull(UserFolderBackup.Take(Path.Combine(_dir, "nowhere"), _backups, DateTime.Now));
            Assert.IsNull(UserFolderBackup.Take(null, _backups, DateTime.Now));
        }

        [TestMethod]
        public void CarrySources_LastCopyCountsEvenWhenWindowsAlreadyMoved()
        {
            // 18 Oct 2026: the patch pointed Windows at the new copy first, so Play saw no switch and carried nothing
            var old = Path.Combine(_dir, "old", "FINAL FANTASY XI");
            Directory.CreateDirectory(Path.Combine(old, "USER", "2"));
            CollectionAssert.AreEqual(new[] { old }, UserFolderBackup.CarrySources(_game, old, _game, null).ToArray());
            Assert.AreEqual(0, UserFolderBackup.CarrySources(_game, _game, _game, new[] { old }).Count, "a record of the last copy means no hunting");
        }

        [TestMethod]
        public void CarrySources_FirstPlayLooksAtEveryKnownCopyOnce()
        {
            var old = Path.Combine(_dir, "old", "FINAL FANTASY XI");
            Directory.CreateDirectory(Path.Combine(old, "USER", "2"));
            var empty = Path.Combine(_dir, "empty", "FINAL FANTASY XI");
            Directory.CreateDirectory(empty);
            CollectionAssert.AreEqual(new[] { old }, UserFolderBackup.CarrySources(_game, "", _game, new[] { _game, empty, old, old + "\\" }).ToArray());
        }

        [TestMethod]
        public void Expired_KeepsTheNewestAndOnePerDay()
        {
            var now = new DateTime(2026, 10, 18, 12, 0, 0);
            var names = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 12; i++) names.Add("USER-20261018-" + (110000 - i * 100).ToString("000000") + ".zip");   // today, newest first
            names.Add("USER-20261016-101100.zip");
            names.Add("USER-20261016-050200.zip");
            names.Add("USER-20260901-120000.zip");   // older than KeepDays
            var gone = UserFolderBackup.Expired(names, now);
            CollectionAssert.AreEquivalent(new[] { names[10], names[11], "USER-20261016-050200.zip", "USER-20260901-120000.zip" }, gone);
        }
    }
}
