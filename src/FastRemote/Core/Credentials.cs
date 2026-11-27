using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace FastRemote
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
        [DllImport("advapi32",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CredEnumerateW(string filter,int flags,out int count,out IntPtr entries);
        // One stored credential; only the generic ones written by this app or cmdkey can be copied (Windows hides the blob of mstsc's own).
        public sealed class Stored
        {
            public string Host,User;
            public bool Copyable;
            public int Group;
        }
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
        // Lists TERMSRV/* entries. Group tells apart entries with the same user and the same password
        // without keeping the password: the bytes are compared in memory and cleared at once.
        public static List<Stored> List()
        {
            var result=new List<Stored>();
            if (!CredEnumerateW("TERMSRV/*",0,out int count,out var list)) return result;
            var secrets=new List<byte[]>();
            try
            {
                for (int i=0;i<count;i++)
                {
                    var e=Marshal.PtrToStructure<Entry>(Marshal.ReadIntPtr(list,i*IntPtr.Size));
                    var host=(e.Target ?? "").Substring("TERMSRV/".Length);
                    if (!Rdp.ValidHost(host) || result.Any(r=>string.Equals(r.Host,host,StringComparison.OrdinalIgnoreCase))) continue;
                    var item=new Stored{Host=host,User=e.User ?? "",Copyable=e.Type==1 && e.Size>0 && e.Blob!=IntPtr.Zero,Group=-1};
                    if (item.Copyable)
                    {
                        var bytes=new byte[e.Size]; Marshal.Copy(e.Blob,bytes,0,e.Size);
                        int same=-1;
                        for (int j=0;j<result.Count && same<0;j++)
                            if (result[j].Copyable && string.Equals(result[j].User,item.User,StringComparison.OrdinalIgnoreCase) && Same(secrets[j],bytes)) same=result[j].Group;
                        item.Group=same>=0 ? same : result.Count;
                        secrets.Add(bytes);
                    }
                    else secrets.Add(null);
                    result.Add(item);
                }
            }
            finally
            {
                CredFree(list);
                foreach (var b in secrets) if (b!=null) Array.Clear(b,0,b.Length);
            }
            return result;
        }
        static bool Same(byte[] a,byte[] b)
        {
            if (a==null || b==null || a.Length!=b.Length) return false;
            int diff=0; for (int i=0;i<a.Length;i++) diff|=a[i]^b[i];
            return diff==0;
        }
        // Writes the password of one host to another (and optionally another user) straight from the stored blob.
        public static void Copy(string from,string to,string user=null)
        {
            string source=Target(from),target=Target(to);
            if (!CredReadW(source,1,0,out var ptr)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var e=Marshal.PtrToStructure<Entry>(ptr);
                if (e.Size<=0 || e.Blob==IntPtr.Zero) throw new InvalidOperationException();
                var entry=new Entry{Type=1,Target=target,User=string.IsNullOrWhiteSpace(user) ? e.User : user,Persist=2,Size=e.Size,Blob=e.Blob};
                if (string.IsNullOrWhiteSpace(entry.User)) throw new ArgumentException();
                if (!string.Equals(source,target,StringComparison.OrdinalIgnoreCase)) Remove(to);
                if (!CredWriteW(ref entry,0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { CredFree(ptr); }
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
