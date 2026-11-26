using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Quaygate
{
    public static class Import
    {
        static string Attr(XElement node,string name) => (string)node.Attribute(name) ?? "";
        static string Value(XElement node,string name,string flag=null)
        {
            while (node!=null && node.Name.LocalName=="Node")
            {
                if (!Attr(node,"Inherit"+(flag ?? name)).Equals("true",StringComparison.OrdinalIgnoreCase)) return Attr(node,name);
                node=node.Parent;
            }
            return "";
        }
        static string Map(string id,string raw)
        {
            if (raw=="") return null;
            if (bool.TryParse(raw,out var flag)) return flag ? "1" : "0";
            switch (id)
            {
                case "Resolution":
                    switch(raw.ToLowerInvariant()) { case "fullscreen": return "full"; case "fittowindow": return "fit"; case "smartsize": return "smart"; }
                    return null;
                case "Colors": return raw=="Colors256" ? "8" : Regex.Match(raw,@"\d+").Value;
                case "RedirectSound": return EnumValue(raw,"BringToThisComputer","LeaveAtRemoteComputer","DoNotPlay");
                case "RDPAuthenticationLevel": return EnumValue(raw,"NoAuth","AuthRequired","WarnOnFailedAuth");
                case "RDGatewayUsageMethod": return EnumValue(raw,"Never","Always","Detect");
                case "RDGatewayUseConnectionCredentials": return EnumValue(raw,"No","Yes");
                case "RedirectDiskDrives": return raw=="All" ? "1" : raw=="None" ? "0" : null;
                default: return raw;
            }
        }
        static string EnumValue(string raw,params string[] names)
        {
            var index=Array.FindIndex(names,n=>n.Equals(raw,StringComparison.OrdinalIgnoreCase));
            return index<0 ? raw : index.ToString();
        }
        public static List<Connection> Read(string path) => Read(path,null,null);

        public static List<Connection> Read(string path,Settings settings,IEnumerable<Connection> existing)
        {
            var xmlSettings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=64*1024*1024};
            using var reader=XmlReader.Create(path,xmlSettings);
            var document=XDocument.Load(reader);
            if (document.Root?.Name.LocalName!="Connections" || string.Equals(Attr(document.Root,"FullFileEncryption"),"true",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException();
            var result=new List<Connection>();
            var sources=new Dictionary<Connection,XElement>();
            foreach (var node in document.Descendants().Where(n=>n.Name.LocalName=="Node" && Attr(n,"Type")=="Connection"))
            {
                if (!Value(node,"Protocol").Equals("RDP",StringComparison.OrdinalIgnoreCase)) continue;
                var folders=node.Ancestors().Where(n=>n.Name.LocalName=="Node").Reverse().Select(n=>Attr(n,"Name")).ToArray();
                var name=Attr(node,"Name");
                var id=Attr(node,"Id");
                var conn=new Connection {Id="mrng-"+(id=="" ? string.Join("/",folders.Concat(new[]{name})) : id),
                    Source="mremoteng", Name=name, Folder=string.Join(" / ",folders), Host=Value(node,"Hostname").Trim(),
                    Domain=Value(node,"Domain"), Username=Value(node,"Username"), Descr=Value(node,"Descr","Description")};
                foreach (var opt in Option.All)
                {
                    var raw=Value(node,opt.Id,opt.Id=="ConnectToConsole" ? "UseConsoleSession" : null);
                    var mapped=Map(opt.Id,raw);
                    if (opt.Id=="Resolution")
                    {
                        var fixedSize=Regex.Match(raw,@"^_?(\d{3,4})x(\d{3,4})$",RegexOptions.IgnoreCase);
                        if (fixedSize.Success)
                        {
                            conn.Options["Resolution"]="fixed";
                            conn.Options["Width"]=fixedSize.Groups[1].Value;
                            conn.Options["Height"]=fixedSize.Groups[2].Value;
                            continue;
                        }
                    }
                    if (!opt.Valid(mapped)) continue;
                    if (opt.Id=="Port") conn.Port=int.Parse(mapped);
                    else if (opt.Id=="ConnectToConsole") conn.Admin=mapped=="1";
                    else if (opt.Id=="Resolution" && mapped=="full") conn.FullScreen=true;
                    else conn.Options[opt.Id]=mapped;
                }
                result.Add(conn);
                sources.Add(conn,node);
            }
            bool dropRoot=result.Count>0 && result.Select(c=>c.Site).Distinct().Count()==1 && result.All(c=>c.Folder.Contains(" / "));
            if (dropRoot)
                foreach (var c in result) c.Folder=c.Folder.Substring(c.Site.Length+3);
            if (settings!=null)
                ImportSiteDefaults(result,sources,dropRoot,settings,existing ?? Enumerable.Empty<Connection>());
            return result;
        }

        static bool Inherits(XElement connection,XElement site,string flag)
        {
            for (var n=connection;n!=site;n=n.Parent)
                if (n==null || !string.Equals(Attr(n,"Inherit"+flag),"true",StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        static void ImportSiteDefaults(List<Connection> rows,Dictionary<Connection,XElement> sources,bool dropRoot,
            Settings settings,IEnumerable<Connection> existing)
        {
            var occupied=new HashSet<string>(existing.Select(c=>c.Site));
            foreach (var group in rows.GroupBy(c=>c.Site))
            {
                var containers=group.Select(c=>sources[c].Ancestors().Where(n=>n.Name.LocalName=="Node").Reverse()
                    .Skip(dropRoot ? 1 : 0).FirstOrDefault()).Distinct().ToList();
                if (containers.Count!=1 || containers[0]==null) continue;
                var site=containers[0];
                bool known=settings.SiteOptions.TryGetValue(group.Key,out var defaults);
                if (!known && occupied.Contains(group.Key)) continue;
                defaults ??= new Dictionary<string,string>();
                foreach (var option in Option.All)
                {
                    var flag=option.Id=="ConnectToConsole" ? "UseConsoleSession" : option.Id;
                    var value=Map(option.Id,Value(site,option.Id,flag));
                    if (!option.Valid(value)) continue;
                    var inheriting=group.Where(c=>Inherits(sources[c],site,flag)).ToList();
                    if (inheriting.Count==0) continue;
                    // An unspecified value on a non-inheriting row must retain the client's default.
                    if (group.Except(inheriting).Any(c=>Value(sources[c],option.Id,flag)=="")) continue;
                    if (!known) defaults[option.Id]=value;
                    foreach (var c in inheriting)
                    {
                        c.Options.Remove(option.Id);
                        if (option.Id=="Port") c.InheritPort=true;
                        if (option.Id=="ConnectToConsole") c.Admin=false;
                        if (option.Id=="Resolution") c.FullScreen=false;
                    }
                    foreach (var c in group.Except(inheriting))
                    {
                        var own=Map(option.Id,Value(sources[c],option.Id,flag));
                        if (option.Valid(own)) c.Options[option.Id]=own;
                    }
                }
                if (defaults.Count>0) settings.SiteOptions[group.Key]=defaults;
            }
        }
        public static void Merge(List<Connection> current,IEnumerable<Connection> imported)
        {
            var byId=current.ToDictionary(c=>c.Id,StringComparer.OrdinalIgnoreCase);
            foreach (var row in imported)
            {
                if (!byId.TryGetValue(row.Id,out var old)) { current.Add(row); byId.Add(row.Id,row); continue; }
                if (!string.Equals(old.Source,"mremoteng",StringComparison.OrdinalIgnoreCase)) continue;
                old.Host=row.Host; old.Port=row.Port; old.Domain=row.Domain; old.Username=row.Username;
                old.InheritPort=row.InheritPort;
                if (old.Options.ContainsKey("Port")) old.Options["Port"]=row.Port.ToString();
            }
        }
    }
}
