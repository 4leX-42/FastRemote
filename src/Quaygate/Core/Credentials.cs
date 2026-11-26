using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;

namespace Quaygate
{
    public static class Credentials
    {
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        struct Entry
        {
            public int Flags,Type;
            public string Target,Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME Written;
            public int Size;
            public IntPtr Blob;
            public int Persist,Count;
            public IntPtr Attributes;
            public string Alias,User;
        }
        [DllImport("advapi32",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CredReadW(string target,int type,int flags,out IntPtr entry);
        [DllImport("advapi32",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CredWriteW(ref Entry entry,int flags);
        [DllImport("advapi32",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CredDeleteW(string target,int type,int flags);
        [DllImport("advapi32")] static extern void CredFree(IntPtr entry);
        static string Target(string host)
        {
            if (!Rdp.ValidHost(host)) throw new ArgumentException(nameof(host));
            return "TERMSRV/"+host;
        }
        public static string User(string host)
        {
            string target=Target(host);
            foreach (int type in new[]{1,2})
            {
                if (!CredReadW(target,type,0,out var ptr)) continue;
                try { return Marshal.PtrToStructure<Entry>(ptr).User ?? ""; }
                finally { CredFree(ptr); }
            }
            return null;
        }
        public static void Remove(string host)
        {
            var target=Target(host);
            foreach (int type in new[]{1,2})
                if (!CredDeleteW(target,type,0))
                {
                    int error=Marshal.GetLastWin32Error();
                    if (error!=1168) throw new Win32Exception(error);
                }
        }
        public static void Set(string host,string user,SecureString password)
        {
            if (string.IsNullOrWhiteSpace(user) || password.Length==0 || password.Length>1280) throw new ArgumentException();
            var entry=new Entry{Type=1,Target=Target(host),User=user,Persist=2,Size=password.Length*2};
            entry.Blob=Marshal.SecureStringToGlobalAllocUnicode(password);
            try
            {
                Remove(host);
                if (!CredWriteW(ref entry,0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { Marshal.ZeroFreeGlobalAllocUnicode(entry.Blob); }
        }
    }
}
