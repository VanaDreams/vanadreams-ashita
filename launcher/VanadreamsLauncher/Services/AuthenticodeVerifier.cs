using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Vanadreams.Services
{
    // WinVerifyTrust validates the signed file, its publisher chain, and its trusted
    // timestamp. Checking an X509Chain at today's date rejects a correctly timestamped
    // release as soon as Artifact Signing rotates its short-lived certificate.
    public static class AuthenticodeVerifier
    {
        private static readonly Guid GenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        private const string ExpectedPublisher = "CN=Lee Hattery, O=Lee Hattery, L=Fort Mcmurray, S=ab, C=CA";

        [StructLayout(LayoutKind.Sequential)]
        private struct WintrustFileInfo
        {
            public uint Size;
            public IntPtr FilePath;
            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WintrustData
        {
            public uint Size;
            public IntPtr PolicyCallbackData;
            public IntPtr SipClientData;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfo;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags;
            public uint UiContext;
            public IntPtr SignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, IntPtr data);

        public static bool IsSignedByUs(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            IntPtr name = IntPtr.Zero, file = IntPtr.Zero, data = IntPtr.Zero;
            try
            {
                name = Marshal.StringToCoTaskMemUni(path);
                var info = new WintrustFileInfo { Size = (uint)Marshal.SizeOf(typeof(WintrustFileInfo)), FilePath = name };
                file = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WintrustFileInfo)));
                Marshal.StructureToPtr(info, file, false);
                var request = new WintrustData
                {
                    Size = (uint)Marshal.SizeOf(typeof(WintrustData)),
                    UiChoice = 2,              // WTD_UI_NONE
                    RevocationChecks = 0,      // WTD_REVOKE_NONE
                    UnionChoice = 1,           // WTD_CHOICE_FILE
                    FileInfo = file,
                    ProviderFlags = 0x1000,   // WTD_CACHE_ONLY_URL_RETRIEVAL
                };
                data = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WintrustData)));
                Marshal.StructureToPtr(request, data, false);
                var action = GenericVerifyV2;
                if (WinVerifyTrust(IntPtr.Zero, ref action, data) != 0) return false;

                using (var signer = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)))
                    return string.Equals(signer.Subject, ExpectedPublisher, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
            finally
            {
                if (data != IntPtr.Zero) Marshal.FreeHGlobal(data);
                if (file != IntPtr.Zero) Marshal.FreeHGlobal(file);
                if (name != IntPtr.Zero) Marshal.FreeCoTaskMem(name);
            }
        }
    }
}
