using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Vanadreams.Services
{
    /// <summary>The xiloader command line, taken apart and put back together.</summary>
    public sealed class LoaderCommand
    {
        private static readonly Regex Token = new Regex("\"[^\"]*\"|\\S+");

        /// <summary>The server's public name, which every player uses unless their network blocks the game's ports.</summary>
        public const string VanadreamsServer = "vanadreams.fairywitch.ca";

        /// <summary>
        /// The server's own address on Tailscale. It only answers a player the server has been shared with.
        /// Tailscale makes ordinary outgoing connections, so it gets through routers and providers that
        /// refuse the game's ports.
        /// </summary>
        public const string TailscaleServer = "100.114.52.41";

        public string Server { get; set; } = "";
        public bool Hairpin { get; set; }

        public bool IsTailscale => string.Equals(Server, TailscaleServer, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>The command logs in to Vanadreams: its public name, any other fairywitch name with vanadreams in it, or its Tailscale address.</summary>
        public bool IsVanadreams => IsTailscale || (Server ?? "").IndexOf("vanadreams", System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Point this command at the server over Tailscale, or back at its public name. Over Tailscale
        /// --hairpin goes on with it: the server hands every client its public address for the zones, and
        /// --hairpin keeps the client on the address it logged in to instead. Everything else is kept.
        /// </summary>
        public void UseTailscale(bool on)
        {
            Server = on ? TailscaleServer : VanadreamsServer;
            Hairpin = on;
        }
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string Extra { get; set; } = "";

        /// <summary>
        /// The PlayOnline language xiloader is told with --lang: 0 Japanese, 1 English (also when absent or not
        /// understood, as xiloader keeps its default), 2 European. It decides which PlayOnline registry key the
        /// loader reads, so the registration has to be written under the same one.
        /// </summary>
        public int Lang => LangOf(Extra);

        public static int LangOf(string extra)
        {
            var tokens = new List<string>();
            foreach (Match m in Token.Matches(extra ?? "")) tokens.Add(m.Value.Trim('"'));
            var lang = 1;
            for (var i = 0; i + 1 < tokens.Count; i++)
            {
                if (!string.Equals(tokens[i], "--lang", System.StringComparison.OrdinalIgnoreCase)) continue;
                var v = tokens[i + 1];
                if (v.StartsWith("JP", System.StringComparison.OrdinalIgnoreCase) || v.StartsWith("0")) lang = 0;
                else if (v.StartsWith("US", System.StringComparison.OrdinalIgnoreCase) || v.StartsWith("1")) lang = 1;
                else if (v.StartsWith("EU", System.StringComparison.OrdinalIgnoreCase) || v.StartsWith("2")) lang = 2;
            }
            return lang;
        }

        public static LoaderCommand Parse(string command)
        {
            var c = new LoaderCommand();
            if (string.IsNullOrWhiteSpace(command)) return c;
            var tokens = new List<string>();
            foreach (Match m in Token.Matches(command)) tokens.Add(m.Value.Trim('"'));
            var extra = new List<string>();
            for (var i = 0; i < tokens.Count; i++)
            {
                switch (tokens[i].ToLowerInvariant())
                {
                    case "--server": if (++i < tokens.Count) c.Server = tokens[i]; break;
                    case "--user": case "--username": if (++i < tokens.Count) c.User = tokens[i]; break;
                    case "--pass": case "--password": if (++i < tokens.Count) c.Password = tokens[i]; break;
                    case "--hairpin": c.Hairpin = true; break;
                    default: extra.Add(tokens[i]); break;
                }
            }
            c.Extra = string.Join(" ", extra);
            return c;
        }

        /// <summary>The command line as it goes into a boot ini: never carries credentials.</summary>
        public string ToIniCommand()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Server)) { parts.Add("--server"); parts.Add(Quote(Server)); }
            if (Hairpin) parts.Add("--hairpin");
            if (!string.IsNullOrWhiteSpace(Extra)) parts.Add(Extra.Trim());
            return string.Join(" ", parts);
        }

        /// <summary>The command line handed to xiloader at launch, credentials included.</summary>
        public string ToLaunchCommand()
        {
            var parts = new List<string> { ToIniCommand() };
            if (!string.IsNullOrWhiteSpace(User)) { parts.Add("--user"); parts.Add(Quote(User)); }
            if (!string.IsNullOrWhiteSpace(Password)) { parts.Add("--pass"); parts.Add(Quote(Password)); }
            return string.Join(" ", parts).Trim();
        }

        private static string Quote(string s) => s.IndexOf(' ') >= 0 ? "\"" + s + "\"" : s;
    }
}
