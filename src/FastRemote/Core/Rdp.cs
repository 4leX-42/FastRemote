using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FastRemote
{
    // The same option catalogue drives the editor, XML mapping and file allowlist.
    public sealed class Option
    {
        public string Id, Key, Choices;
        public Option(string id, string key, string choices = "0|1") { Id=id; Key=key; Choices=choices; }
        public static readonly Option[] All = {
            new Option("Resolution", "", "fit|full|smart|fixed"),
            new Option("Width","desktopwidth","number"), new Option("Height","desktopheight","number"),
            new Option("Colors","session bpp","8|15|16|24|32"), new Option("MultiMonitor","use multimon"),
            new Option("RedirectDiskDrives","drivestoredirect"), new Option("RedirectPrinters","redirectprinters"),
            new Option("RedirectPorts","redirectcomports"), new Option("RedirectSmartCards","redirectsmartcards"),
            new Option("RedirectClipboard","redirectclipboard"), new Option("RedirectSound","audiomode","0|1|2"),
            new Option("RedirectAudioCapture","audiocapturemode"), new Option("RedirectKeys","keyboardhook","0|1|2"),
            new Option("DisplayWallpaper","disable wallpaper"), new Option("DisplayThemes","disable themes"),
            new Option("EnableFontSmoothing","allow font smoothing"), new Option("EnableDesktopComposition","allow desktop composition"),
            new Option("CacheBitmaps","bitmapcachepersistenable"), new Option("ConnectToConsole","administrative session"),
            new Option("UseCredSsp","enablecredsspsupport"), new Option("RDPAuthenticationLevel","authentication level","0|1|2"),
            new Option("RDGatewayUsageMethod","gatewayusagemethod","0|1|2"),
            new Option("RDGatewayHostname","gatewayhostname","text"),
            new Option("RDGatewayUseConnectionCredentials","promptcredentialonce"),
            new Option("LoadBalanceInfo","loadbalanceinfo","text"), new Option("Port","","number")
        };
        public bool Valid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Any(char.IsControl)) return false;
            if (Choices == "text") return Id != "RDGatewayHostname" || Rdp.ValidHost(value);
            if (Choices != "number") return Choices.Split('|').Contains(value);
            return int.TryParse(value,out int n) && (Id=="Port" ? n>=1 && n<=65535 : n>=200 && n<=8192);
        }
    }

    public static class Rdp
    {
        public static bool ValidHost(string host) => !string.IsNullOrWhiteSpace(host) && host.Length<=253 &&
            !host.Any(c=>char.IsControl(c) || char.IsWhiteSpace(c)) &&
            (IPAddress.TryParse(host,out _) || Regex.IsMatch(host,@"\A[\p{L}\p{Nd}_][\p{L}\p{Nd}._-]*\z"));
        public static string Endpoint(string host,int port)
        {
            if (host?.Contains(":")==true && !host.StartsWith("[")) host="["+host+"]";
            return port==3389 || port<=0 ? host : host+":"+port;
        }
        public static Dictionary<string,string> Effective(Connection c,Settings settings)
        {
            var result = new Dictionary<string,string>();
            if (settings.SiteOptions.TryGetValue(c.Site,out var site))
                foreach (var pair in site) result[pair.Key]=pair.Value;
            if (!c.InheritPort && c.Port!=3389 && c.Port>0) result["Port"]=c.Port.ToString();
            if (c.FullScreen) result["Resolution"]="full";
            if (c.Admin) result["ConnectToConsole"]="1";
            foreach (var pair in c.Options) result[pair.Key]=pair.Value;
            return result;
        }
        public static int Port(Dictionary<string,string> values) => values.TryGetValue("Port",out var v) && int.TryParse(v,out var n) && n>0 && n<=65535 ? n : 3389;
        public static string Arguments(Connection c, Settings settings, string directory, bool admin=false, bool full=false)
        {
            if (!ValidHost(c.Host)) throw new InvalidDataException();
            var values=Effective(c,settings);
            foreach (var o in Option.All)
                if (values.TryGetValue(o.Id,out var v) && !o.Valid(v)) throw new InvalidDataException();
            string value(string id) => values.TryGetValue(id,out var val) ? val : null;
            var endpoint=Endpoint(c.Host, Port(values));
            admin |= value("ConnectToConsole")=="1";
            full |= value("Resolution")=="full";
            var suffix=admin ? " /admin" : "";
            var advanced=Option.All.Any(o => values.ContainsKey(o.Id) && o.Id!="Port" && o.Id!="ConnectToConsole" &&
                !(o.Id=="Resolution" && value(o.Id)=="full"));
            if (!advanced) return "/v:"+endpoint+suffix+(full ? " /f" : "");
            var lines=new List<string>{"full address:s:"+endpoint};
            if (!string.IsNullOrEmpty(c.Login))
            {
                if (c.Login.Any(char.IsControl)) throw new InvalidDataException();
                lines.Add("username:s:"+c.Login);
            }
            var resolution=value("Resolution");
            if (full || resolution!=null) lines.Add("screen mode id:i:"+(full ? "2" : "1"));
            if (!full && resolution=="fit") lines.Add("dynamic resolution:i:1");
            if (!full && resolution=="smart") lines.Add("smart sizing:i:1");
            if (resolution=="fixed")
            {
                lines.Add("desktopwidth:i:"+(value("Width") ?? "1280"));
                lines.Add("desktopheight:i:"+(value("Height") ?? "720"));
            }
            foreach (var o in Option.All)
            {
                var v=value(o.Id);
                if (v==null || o.Key=="" || o.Id=="Width" || o.Id=="Height") continue;
                if (o.Id=="RedirectDiskDrives") { lines.Add(o.Key+":s:"+(v=="1" ? "*" : "")); continue; }
                if (o.Id=="DisplayWallpaper" || o.Id=="DisplayThemes") v=v=="1" ? "0" : "1";
                lines.Add(o.Key+(o.Choices=="text" ? ":s:" : ":i:")+v);
            }
            Directory.CreateDirectory(directory);
            // Imported identifiers may contain folder separators or reserved device names.
            string stem;
            if (Guid.TryParse(c.Id,out var id)) stem=id.ToString();
            else { using var hash=SHA256.Create(); stem=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(c.Id))).Replace("-",""); }
            var path=Path.Combine(directory,stem+".rdp");
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllLines(temp,lines,Encoding.Unicode);
                if (File.Exists(path)) File.Replace(temp,path,null);
                else File.Move(temp,path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return "\""+path+"\""+suffix;
        }
        public static void Launch(Connection c,Settings settings,string directory,bool admin,bool full,Action<ProcessStartInfo> start)
        {
            start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"mstsc.exe"),Arguments(c,settings,directory,admin,full)){UseShellExecute=false});
        }
        public static void Clean(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.EnumerateFiles(directory,"*.rdp"))
                if ((Guid.TryParse(Path.GetFileNameWithoutExtension(path),out _) || Regex.IsMatch(Path.GetFileNameWithoutExtension(path),"^[A-F0-9]{64}$")) &&
                    File.GetLastWriteTimeUtc(path)<DateTime.UtcNow.AddDays(-1)) File.Delete(path);
        }
    }
}
