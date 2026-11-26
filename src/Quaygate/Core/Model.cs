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

namespace Quaygate
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
        public ExtensionDataObject ExtensionData { get; set; }
        public string Site => (Folder ?? "").Split(new[]{" / "}, StringSplitOptions.None)[0];
        public string Login => string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Domain) ? Username ?? "" : Domain + "\\" + Username;
        public string HostLabel => Rdp.Endpoint(Host, DisplayPort);
        public int DisplayPort { get; set; } = 3389;
        public string CredentialUser { get; set; }
        public string KeyIcon => CredentialUser == null ? "" : "\uE8D7";
        public string Status { get; set; } = "Unknown";
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        private string searchIndex;
        public void Index() => searchIndex = Fold(string.Join(" ", Name, Host, Folder, Descr, Login));
        public bool Matches(string[] words) => words.All(w => (searchIndex ?? "").IndexOf(w, StringComparison.Ordinal) >= 0);
        public static string Fold(string text) => string.Concat((text ?? "").Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
        [OnDeserialized] void Loaded(StreamingContext context)
        {
            if (string.IsNullOrEmpty(Id)) Id = Guid.NewGuid().ToString();
            Options ??= new Dictionary<string,string>();
            if (Port <= 0) Port = 3389;
            DisplayPort = Port;
            Status = "Unknown";
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
        public ExtensionDataObject ExtensionData { get; set; }
        [OnDeserialized] void Loaded(StreamingContext context)
        {
            if (!(Scale >= .9 && Scale <= 1.6)) Scale = 1.2;
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
        public static string DefaultHome => Environment.GetEnvironmentVariable("QUAYGATE_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Quaygate");
        public string RdpDirectory => Environment.GetEnvironmentVariable("QUAYGATE_HOME") != null ? Path.Combine(Home,"rdp") :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Quaygate","rdp");
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
