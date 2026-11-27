using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace FastRemote
{
    // Usage kept apart from connections.json, so a connect never rewrites the connection list.
    [DataContract]
    public class History : IExtensibleDataObject
    {
        [DataContract]
        public class Entry
        {
            [DataMember] public string Id { get; set; }
            [DataMember] public string When { get; set; }
            [DataMember(EmitDefaultValue=false)] public string Problem { get; set; }
            public DateTime? WhenUtc => DateTime.TryParse(When,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var d) ? d.ToUniversalTime() : (DateTime?)null;
        }
        public const int Keep=20;
        [DataMember] public List<Entry> Recent { get; set; } = new List<Entry>();
        public ExtensionDataObject ExtensionData { get; set; }
        [OnDeserialized] void Loaded(StreamingContext context) { Recent=(Recent ?? new List<Entry>()).Where(e=>e?.Id!=null).ToList(); }

        public void Used(string id,DateTime utc,string problem)
        {
            Recent.RemoveAll(e=>e.Id==id);
            Recent.Insert(0,new Entry{Id=id,When=utc.ToString("o",CultureInfo.InvariantCulture),Problem=problem});
            if (Recent.Count>Keep) Recent.RemoveRange(Keep,Recent.Count-Keep);
        }
        public void Forget(IEnumerable<string> liveIds)
        {
            var keep=new HashSet<string>(liveIds);
            Recent.RemoveAll(e=>!keep.Contains(e.Id));
        }
        public Entry Of(string id) => Recent.FirstOrDefault(e=>e.Id==id);
    }

    // Ranks text against a query: every word must match a word start, a substring, the letters in order,
    // or (words of 4+ letters) a word with one typo. Lower tiers score less.
    public static class Finder
    {
        public static int Score(string folded,string[] words)
        {
            if (words.Length==0) return 1;
            int total=0;
            foreach (var w in words)
            {
                int s=Word(folded,w);
                if (s==0) return 0;
                total+=s;
            }
            return total;
        }
        static int Word(string text,string w)
        {
            int at=text.IndexOf(w,StringComparison.Ordinal);
            if (at==0) return 100;
            if (at>0) return char.IsLetterOrDigit(text[at-1]) ? 60 : 90;
            if (w.Length>=3 && InOrder(text,w)) return 25;
            if (w.Length>=4 && text.Split(Separators,StringSplitOptions.RemoveEmptyEntries).Any(t=>Near(t,w))) return 15;
            return 0;
        }
        static readonly char[] Separators={' ','.','-','_','\\','/',':','(',')',','};
        static bool InOrder(string text,string w)
        {
            int i=0;
            foreach (char c in text) if (c==w[i] && ++i==w.Length) return true;
            return false;
        }
        // Optimal string alignment distance <= 1, also against the same-length prefix of a longer token.
        static bool Near(string token,string w)
        {
            if (token.Length>w.Length) token=token.Substring(0,w.Length);
            if (Math.Abs(token.Length-w.Length)>1) return false;
            int n=token.Length,m=w.Length;
            var d=new int[n+1,m+1];
            for (int i=0;i<=n;i++) d[i,0]=i;
            for (int j=0;j<=m;j++) d[0,j]=j;
            for (int i=1;i<=n;i++)
            for (int j=1;j<=m;j++)
            {
                int cost=token[i-1]==w[j-1] ? 0 : 1;
                d[i,j]=Math.Min(Math.Min(d[i-1,j]+1,d[i,j-1]+1),d[i-1,j-1]+cost);
                if (i>1 && j>1 && token[i-1]==w[j-2] && token[i-2]==w[j-1]) d[i,j]=Math.Min(d[i,j],d[i-2,j-2]+1);
            }
            return d[n,m]<=1;
        }
    }
}
