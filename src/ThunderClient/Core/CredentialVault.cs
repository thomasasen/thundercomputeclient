using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ThunderClient.Core;

// Windows Credential Manager, Generic Credentials. Geheimnisse verlassen keine JSON-Datei.
public sealed class CredentialVault
{
    private const uint Generic = 1;
    private const uint PersistLocalMachine = 2;
    private static string Target(string name) => "ThunderClient/" + name;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr result);
    [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("Advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);

    public void Save(string service, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Leere Zugangsdaten werden nicht gespeichert.");
        var bytes = Encoding.Unicode.GetBytes(value);
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            var c = new Credential
            {
                Type = Generic, TargetName = Target(service), CredentialBlob = pointer,
                CredentialBlobSize = (uint)bytes.Length, Persist = PersistLocalMachine,
                UserName = Environment.UserName
            };
            if (!CredWrite(ref c, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Speichern im Credential Manager fehlgeschlagen");
        }
        finally
        {
            Array.Clear(bytes);
            for (var index = 0; index < value.Length * 2; index++) Marshal.WriteByte(pointer, index, 0);
            Marshal.FreeHGlobal(pointer);
        }
    }

    public string? Read(string service)
    {
        if (!CredRead(Target(service), Generic, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return null;
            throw new Win32Exception(error, "Credential Manager nicht lesbar");
        }
        try
        {
            var c = Marshal.PtrToStructure<Credential>(pointer);
            return c.CredentialBlobSize == 0 ? "" : Marshal.PtrToStringUni(c.CredentialBlob,
                checked((int)c.CredentialBlobSize / 2));
        }
        finally { CredFree(pointer); }
    }

    public bool Exists(string service) => Read(service) is not null;

    public void Delete(string service)
    {
        if (!CredDelete(Target(service), Generic, 0) && Marshal.GetLastWin32Error() != 1168)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Löschen im Credential Manager fehlgeschlagen");
    }
}

