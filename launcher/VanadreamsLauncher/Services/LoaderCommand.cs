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
        public const string TailscaleServer = "100.93.179.33";

        /// <summary>
        /// Where the server was on Tailscale before it moved to the new server PC (18 Oct 2026). Profiles that
        /// still carry it are Tailscale profiles too, and are written out with the current address.
        /// </summary>
        public const string PreviousTailscaleServer = "100.114.52.41";

        /// <summary>
        /// The server's Tailscale address now: the one built in, until the catalogue names another
        /// (UseServers). The Profiles page's Tailscale box writes this one.
        /// </summary>
        public static string Tailscale { get; private set; } = TailscaleServer;

        private static Dictionary<string, string> _moved = BuiltInMoves();

        private static Dictionary<string, string> BuiltInMoves() => new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            // only addresses no other server could be at: a home-network address like 192.168.0.19 is somebody
            // else's server on somebody else's network
            { PreviousTailscaleServer, TailscaleServer },
        };

        /// <summary>
        /// The server addresses from the catalogue's "servers" block, so a future move needs a catalogue edit and no
        /// new launcher: its Tailscale address, and old address -> new address for every place the server has been.
        /// The built-in moves stay; the catalogue's are added over them. Blank or missing keeps what is built in.
        /// </summary>
        public static void UseServers(string tailscale, IDictionary<string, string> moved)
        {
            var map = BuiltInMoves();
            if (moved != null)
                foreach (var kv in moved)
                    if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value) && !Same(kv.Key, kv.Value))
                        map[kv.Key.Trim()] = kv.Value.Trim();
            var ts = string.IsNullOrWhiteSpace(tailscale) ? TailscaleServer : tailscale.Trim();
            // the built-in address is an old one once the catalogue names another
            if (!Same(ts, TailscaleServer) && !map.ContainsKey(TailscaleServer)) map[TailscaleServer] = ts;
            map.Remove(ts);   // the current address has not moved, whatever an old list says
            _moved = map;
            Tailscale = ts;
        }

        /// <summary>Back to the addresses built in (tests).</summary>
        public static void ResetServers() { _moved = BuiltInMoves(); Tailscale = TailscaleServer; }

        /// <summary>
        /// Where the server is now, for an address it was once at, following a chain of moves (A -> B -> C gives C);
        /// null when the address never moved.
        /// </summary>
        public static string MovedTo(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return null;
            string to = null, at = server.Trim();
            string next;
            for (var hops = 0; hops < 8 && _moved.TryGetValue(at, out next); hops++) { to = next; at = next; }
            return to;
        }

        /// <summary>The address to connect to: the server's new one when it moved, else as written.</summary>
        public static string Current(string server) => MovedTo(server) ?? server;

        private static bool Same(string a, string b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase);

        private static readonly Regex ServerArg = new Regex("(?<flag>--server\\s+)(?<value>\"[^\"]*\"|\\S+)", RegexOptions.IgnoreCase);

        /// <summary>
        /// A boot command with its --server pointed at where the server is now, every other character left as it
        /// was (--hairpin, --lang, a login written into the file by hand). Null when it does not name an old address.
        /// </summary>
        public static string Repoint(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            var m = ServerArg.Match(command);
            if (!m.Success) return null;
            var to = MovedTo(m.Groups["value"].Value.Trim('"'));
            if (to == null) return null;
            return command.Substring(0, m.Groups["value"].Index) + Quote(to) + command.Substring(m.Groups["value"].Index + m.Groups["value"].Length);
        }

        public string Server { get; set; } = "";
        public bool Hairpin { get; set; }

        public bool IsTailscale => Same(Current(Server), Tailscale);

        /// <summary>The command logs in to Vanadreams: its public name, any other fairywitch name with vanadreams in it, its Tailscale address, or any address it has moved from.</summary>
        public bool IsVanadreams => IsTailscale || MovedTo(Server) != null
                                    || (Server ?? "").IndexOf("vanadreams", System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Point this command at the server over Tailscale, or back at its public name. Over Tailscale
        /// --hairpin goes on with it: the server hands every client its public address for the zones, and
        /// --hairpin keeps the client on the address it logged in to instead. Everything else is kept.
        /// </summary>
        public void UseTailscale(bool on)
        {
            Server = on ? Tailscale : VanadreamsServer;
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
            if (!string.IsNullOrWhiteSpace(Server))
            {
                parts.Add("--server"); parts.Add(Quote(Current(Server)));
            }
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
