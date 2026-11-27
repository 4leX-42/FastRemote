using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace FastRemote
{
    [DataContract]
    public class Connection : IExtensibleDataObject, INotifyPropertyChanged
    {
        [DataMember] public string Id { get; set; } = Guid.NewGuid().ToString();
        [DataMember] public string Name { get; set; } = "";
        [DataMember] public string Host { get; set; } = "";
        [DataMember] public string Folder { get; set; } = "";
        [DataMember] public int Port { get; set; } = 3389;
        [DataMember(EmitDefaultValue=false)] public bool InheritPort { get; set; }
        [DataMember] public string Domain { get; set; } = "";
        [DataMember] public string Username { get; set; } = "";
        [DataMember] public string Descr { get; set; } = "";
        [DataMember] public bool Admin { get; set; }
        [DataMember] public bool FullScreen { get; set; }
        [DataMember] public string Source { get; set; } = "manual";
        [DataMember] public Dictionary<string,string> Options { get; set; } = new Dictionary<string,string>();
        [DataMember(EmitDefaultValue=false)] public bool Favorite { get; set; }
        [DataMember(EmitDefaultValue=false)] public string Kind { get; set; }
        [DataMember(EmitDefaultValue=false)] public string Tags { get; set; }
        public ExtensionDataObject ExtensionData { get; set; }
        public static readonly string[] Kinds={"","server","dc","backup","terminal","desktop","network"};
        public string KindIcon => Kind switch { "server"=>"\uE8CE", "dc"=>"\uE83D", "backup"=>"\uE7B8", "terminal"=>"\uE977", "desktop"=>"\uE7F4", "network"=>"\uE839", _=>"\uE7F4" };
        public IEnumerable<string> TagList => (Tags ?? "").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(t=>t.Trim()).Where(t=>t.Length>0);
        public string Protocol => Options!=null && Options.TryGetValue("RDGatewayUsageMethod",out var gw) && gw=="1" ? "RDP · GW" : "RDP";
        // Runtime state, never saved.
        public ReachResult Reach { get; set; } = new ReachResult();
        public Session Live { get; set; }
        public DateTime? LastUsed { get; set; }
        public string LastProblem { get; set; }
        public string ReachState => Reach?.State.ToString() ?? "Unknown";
        public string SessionState => Live?.State.ToString() ?? "";
        public string LatencyText => Reach?.State==FastRemote.Reach.Up && Reach.LatencyMs!=null ? Reach.LatencyMs+" ms" : "";
        public string LastText => LastUsed==null ? "\u2014" : Words.When(LastUsed.Value);
        public string SessionText => Live==null ? "" : Words.T("S"+Live.State);
        public string FavoriteIcon => Favorite ? "\uE735" : "";
        public string ReachText => ReachState=="Up" ? Words.T("UpShort") : ReachState=="Down" ? Words.T("DownShort") : Words.T(ReachState);
        public string ReachTip => Reach==null || Reach.CheckedUtc==DateTime.MinValue ? Words.T(ReachState) : Words.T(ReachState)+" · "+Words.T("Checked",Words.Since(Reach.CheckedUtc));
        public System.Windows.Visibility LiveVisibility => Live==null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        public string StarIcon => Favorite ? "\uE735" : "\uE734";
        public object StarBrush => System.Windows.Application.Current?.TryFindResource(Favorite ? "Accent" : "Sub");
        public string StarTip => Words.T(Favorite ? "StarRemove" : "StarAdd");
        public string KindText => Words.T("K"+(Kind ?? ""));
        public string SiteText => string.IsNullOrEmpty(Site) ? Words.T("NoSite") : Site;
        public string Endpoint => Rdp.Endpoint(Host,DisplayPort);
        public string Site => (Folder ?? "").Split(new[]{" / "}, StringSplitOptions.None)[0];
        public string Login => string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Domain) ? Username ?? "" : Domain + "\\" + Username;
        public string HostLabel => Rdp.Endpoint(Host, DisplayPort);
        public int DisplayPort { get; set; } = 3389;
        public string CredentialUser { get; set; }
        public string KeyIcon => CredentialUser == null ? "" : "\uE8D7";
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        private string searchIndex;
        public void Index() => searchIndex = Fold(string.Join(" ", Name, Host, Folder, Descr, Login, Tags, Kind==null ? "" : Words.T("K"+Kind)));
        public string SearchText => searchIndex ?? "";
        public bool Matches(string[] words) => words.All(w => (searchIndex ?? "").IndexOf(w, StringComparison.Ordinal) >= 0);
        public static string Fold(string text) => string.Concat((text ?? "").Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
        [OnDeserialized] void Loaded(StreamingContext context)
        {
            if (string.IsNullOrEmpty(Id)) Id = Guid.NewGuid().ToString();
            Options ??= new Dictionary<string,string>();
            if (Port <= 0) Port = 3389;
            DisplayPort = Port;
            Reach = new ReachResult();
            Index();
        }
    }

    [DataContract]
    public class Settings : IExtensibleDataObject
    {
        [DataMember] public double Scale { get; set; } = 1.2;
        [DataMember] public string Theme { get; set; } = "Carbon";
        [DataMember] public string Accent { get; set; } = "";
        [DataMember] public string Language { get; set; } = "auto";
        [DataMember] public List<string> SiteOrder { get; set; } = new List<string>();
        [DataMember] public Dictionary<string, Dictionary<string,string>> SiteOptions { get; set; } = new Dictionary<string, Dictionary<string,string>>();
        [DataMember] public double Left, Top, Width, Height;
        [DataMember] public bool Maximized;
        [DataMember(EmitDefaultValue=false)] public string Density { get; set; } = "compact";
        [DataMember(EmitDefaultValue=false)] public double SidebarWidth { get; set; } = 240;
        [DataMember(EmitDefaultValue=false)] public double InspectorWidth { get; set; } = 360;
        [DataMember(EmitDefaultValue=false)] public bool InspectorHidden { get; set; }
        [DataMember(EmitDefaultValue=false)] public bool SidebarCollapsed { get; set; }
        [DataMember(EmitDefaultValue=false)] public List<string> Collapsed { get; set; } = new List<string>();
        [DataMember(EmitDefaultValue=false)] public List<string> Expanded { get; set; } = new List<string>();
        [DataMember(EmitDefaultValue=false)] public List<string> HiddenColumns { get; set; }
        [DataMember(EmitDefaultValue=false)] public Dictionary<string,double> ColumnWidths { get; set; } = new Dictionary<string,double>();
        [DataMember(EmitDefaultValue=false)] public string SortBy { get; set; }
        [DataMember(EmitDefaultValue=false)] public bool SortDescending { get; set; }
        [DataMember] public bool GroupBySite { get; set; } = true;
        [DataMember(EmitDefaultValue=false)] public string View { get; set; }
        // 2 = table and panels of the classic layout; older saved widths belong to other columns
        [DataMember(EmitDefaultValue=false)] public int LayoutVersion { get; set; }
        // Availability: only the selected connection is checked automatically (no sweeps of the network).
        [DataMember] public bool AutoCheck { get; set; } = true;
        [DataMember] public int CheckSeconds { get; set; } = 60;
        [DataMember] public int TimeoutMs { get; set; } = 2000;
        // Sessions: Connect on a connection with a live session brings that window forward.
        [DataMember] public bool CheckAllOnStart { get; set; } = true;
        [DataMember] public int CheckAllMinutes { get; set; } = 5;
        [DataMember] public bool ReuseSession { get; set; } = true;
        [DataMember] public bool AdoptSessions { get; set; } = true;
        [DataMember] public bool ConfirmDisconnectAll { get; set; } = true;
        public static readonly string[] DefaultHidden={"Site","Session","Latency","Cred"};
        // The serializer does not run constructors: members added after the first release get their defaults here.
        [OnDeserializing] void Defaults(StreamingContext context)
        {
            Density="compact"; SidebarWidth=240; InspectorWidth=360;
            Collapsed=new List<string>(); Expanded=new List<string>(); ColumnWidths=new Dictionary<string,double>();
            AutoCheck=true; CheckSeconds=60; TimeoutMs=2000; CheckAllOnStart=true; CheckAllMinutes=5; GroupBySite=true;
            ReuseSession=true; AdoptSessions=true; ConfirmDisconnectAll=true;
        }
        public ExtensionDataObject ExtensionData { get; set; }
        [OnDeserialized] void Loaded(StreamingContext context)
        {
            if (!(Scale >= .9 && Scale <= 2.0)) Scale = 1.2;
            Collapsed ??= new List<string>(); Expanded ??= new List<string>();
            ColumnWidths ??= new Dictionary<string,double>();
            if (Density!="comfortable") Density="compact";
            if (!(SidebarWidth>=160 && SidebarWidth<=480)) SidebarWidth=240;
            if (!(InspectorWidth>=260 && InspectorWidth<=560)) InspectorWidth=360;
            if (CheckSeconds<15 || CheckSeconds>3600) CheckSeconds=60;
            if (TimeoutMs<500 || TimeoutMs>10000) TimeoutMs=2000;
            if (CheckAllMinutes<1 || CheckAllMinutes>240) CheckAllMinutes=5;
            if (LayoutVersion<2)
            {
                ColumnWidths.Clear(); HiddenColumns=null; SortBy=null; GroupBySite=true;
                SidebarWidth=270; InspectorWidth=400; LayoutVersion=2;
            }
            // 4 = narrower sidebar by default (210 cut the labels)
            if (LayoutVersion<4) { SidebarWidth=240; LayoutVersion=4; }
            SiteOrder ??= new List<string>();
            SiteOptions ??= new Dictionary<string,Dictionary<string,string>>();
        }
        public List<string> Sites(IEnumerable<Connection> rows)
        {
            var sites = rows.Select(c => c.Site).Distinct().ToList();
            return SiteOrder.Where(sites.Contains).Concat(sites.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)).Distinct().ToList();
        }
    }

    public class Store
    {
        public string Home { get; }
        public Store(string home) { Home = Path.GetFullPath(home); Directory.CreateDirectory(Home); }
        public static string DefaultHome => Environment.GetEnvironmentVariable("FASTREMOTE_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastRemote");
        public string RdpDirectory => Environment.GetEnvironmentVariable("FASTREMOTE_HOME") != null ? Path.Combine(Home,"rdp") :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FastRemote","rdp");
        static DataContractJsonSerializer Serializer<T>() => new DataContractJsonSerializer(typeof(T),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        public T Load<T>(string name) where T : new()
        {
            var path = Path.Combine(Home,name);
            if (!File.Exists(path)) return new T();
            using var input = new StreamReader(path, Encoding.UTF8, true);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input.ReadToEnd()));
            return (T)Serializer<T>().ReadObject(stream) ?? throw new InvalidDataException();
        }
        public static T Clone<T>(T value)
        {
            using var stream = new MemoryStream();
            var serializer = Serializer<T>();
            serializer.WriteObject(stream,value);
            stream.Position = 0;
            return (T)serializer.ReadObject(stream);
        }
        public void Save<T>(string name, T value)
        {
            var path = Path.Combine(Home,name);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    using var writer = JsonReaderWriterFactory.CreateJsonWriter(output, Encoding.UTF8, false, true, "  ");
                    Serializer<T>().WriteObject(writer,value);
                    writer.Flush();
                    output.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp,path,path+".bak");
                else File.Move(temp,path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public void Log(Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Home,"error.log"), DateTime.UtcNow.ToString("o") + " " + ex.GetType().Name + " " + ex.HResult + Environment.NewLine); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
