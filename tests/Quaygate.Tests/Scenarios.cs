using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Quaygate.Tests
{
    // Each scenario has its own data directory; the launcher always uses a process recorder.
    public class Scenarios
    {
        readonly string directory;
        public Scenarios(string directory) { this.directory=directory; Directory.CreateDirectory(directory); }
        static void Check(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Equal<T>(T expected,T actual) => Check(EqualityComparer<T>.Default.Equals(expected,actual),"Expected "+expected+", got "+actual);
        public void Storage()
        {
            var store=new Store(directory);
            const string original="[{\"Id\":\"mrng-a\",\"Folder\":\"Lab\",\"Name\":\"Edited locally\",\"Host\":\"one.invalid\",\"Port\":3390,\"Domain\":\"LAB\",\"Username\":\"test\",\"Descr\":\"desk\",\"Admin\":true,\"FullScreen\":false,\"Source\":\"mremoteng\",\"Future\":{\"Level\":4}}]";
            var path=Path.Combine(directory,"connections.json");
            File.WriteAllText(path,original,new UTF8Encoding(true));
            var rows=store.Load<List<Connection>>("connections.json");
            Equal("Edited locally",rows.Single().Name); Equal(@"LAB\test",rows[0].Login);
            rows[0].Name="Local change"; rows[0].Options["Colors"]="16"; store.Save("connections.json",rows);
            Equal(original,File.ReadAllText(path+".bak"));
            string json=File.ReadAllText(path);
            foreach(var key in new[]{"Future","Level","Options","Colors"}) Check(json.Contains(key),key);
            foreach(var key in new[]{"CredentialUser","HasCred","Status","Password","DisplayPort"}) Check(!json.Contains(key),key);
            var again=store.Load<List<Connection>>("connections.json").Single(); Equal("16",again.Options["Colors"]); Equal(true,again.Admin);
            store.Save("connections.json",new List<Connection>()); Equal(0,store.Load<List<Connection>>("connections.json").Count);
        }
        public void ImportAndMerge()
        {
            const string xml=@"<m:Connections xmlns:m='urn:test'><Node Name='Root' Type='Container' Domain='LAB' Descr='inherited' ConnectToConsole='true' RedirectClipboard='true'>
              <Node Name='North' Type='Container' InheritDomain='true' InheritDescription='true' InheritUseConsoleSession='true' InheritRedirectClipboard='true'>
                <Node Name='App' Id='first' Type='Connection' Hostname=' app.invalid ' Protocol='RDP' InheritDomain='true' InheritDescription='true' InheritUseConsoleSession='true' InheritRedirectClipboard='true' Username='qa' Port='3391' Colors='Colors16Bit' Resolution='Fullscreen'/>
                <Node Name='Floor' Type='Container'><Node Name='Desk' Type='Connection' Hostname='desk.invalid' Protocol='RDP'/></Node>
              </Node><Node Name='West' Type='Container'><Node Name='Ignored' Type='Connection' Protocol='SSH2'/></Node></Node></m:Connections>";
            var path=Path.Combine(directory,"fake.xml"); File.WriteAllText(path,xml);
            var imported=Import.Read(path);
            Equal(2,imported.Count); var c=imported[0];
            Equal("North",c.Folder); Equal("North / Floor",imported[1].Folder); Equal("LAB",c.Domain); Equal("inherited",c.Descr);
            Equal(true,c.Admin && c.FullScreen); Equal("app.invalid",c.Host); Equal(3391,c.Port); Equal("16",c.Options["Colors"]); Equal("1",c.Options["RedirectClipboard"]);
            var current=new List<Connection>{Store.Clone(c),new Connection{Name="Manual"}};
            current[0].Name="Local"; current[0].Folder="My site"; current[0].Descr="My description"; current[0].Options["Colors"]="32"; current[0].Admin=false;
            c.Host="new.invalid"; c.Port=3392; c.Domain="ALT"; c.Username="other";
            Import.Merge(current,imported); Import.Merge(current,imported);
            Equal(3,current.Count); var saved=current[0]; Equal("Local",saved.Name); Equal("My site",saved.Folder); Equal("My description",saved.Descr);
            Equal("32",saved.Options["Colors"]); Equal(false,saved.Admin); Equal("new.invalid",saved.Host); Equal(3392,saved.Port); Equal(@"ALT\other",saved.Login);
            File.WriteAllText(path,"<Connections><Node Name='Keep'><Node Type='Connection' Protocol='RDP'/></Node></Connections>");
            Equal("Keep",Import.Read(path).Single().Folder);
        }
        public void SearchAndSites()
        {
            var c=new Connection{Name="Déploiement",Folder="Málaga / Norte",Host="desk.invalid",Domain="EXAMPLE",Username="user",Descr="Étage"};
            c.Index();
            Check(c.Matches(Connection.Fold("malaga DEPLOI ÉTAGE user desk").Split(' ')),"All fields");
            Check(!c.Matches(new[]{"MALAGA","ABSENT"}),"All words");
            var settings=new Settings{SiteOrder=new List<string>{"West","Gone","West"}};
            var rows=new[]{c,new Connection{Folder="West"},new Connection{Folder="East"}};
            Equal("West,East,Málaga",string.Join(",",settings.Sites(rows)));
            var store=new Store(directory); store.Save("settings.json",settings);
            Equal("West",store.Load<Settings>("settings.json").Sites(rows)[0]);
        }
        public void ImportSiteInheritance()
        {
            var xml=@"<Connections><Node Name='Root'><Node Name='North' Port='3390' Colors='Colors16Bit'>
              <Node Type='Connection' Name='Inherited' Id='one' Protocol='RDP' InheritColors='true' InheritPort='true'/>
              <Node Type='Connection' Name='Explicit' Id='two' Protocol='RDP' Colors='Colors32Bit' Port='3389'/>
              </Node></Node></Connections>";
            var path=Path.Combine(directory,"profiles.xml"); File.WriteAllText(path,xml);
            var settings=new Settings();
            var rows=Import.Read(path,settings,new Connection[0]);
            Equal("16",settings.SiteOptions["North"]["Colors"]); Equal("3390",settings.SiteOptions["North"]["Port"]);
            Equal(false,rows[0].Options.ContainsKey("Colors"));
            settings.SiteOptions["North"]["Colors"]="24"; settings.SiteOptions["North"]["Port"]="3397";
            Equal("24",Rdp.Effective(rows[0],settings)["Colors"]); Equal(3397,Rdp.Port(Rdp.Effective(rows[0],settings)));
            Equal("32",Rdp.Effective(rows[1],settings)["Colors"]); Equal(3389,Rdp.Port(Rdp.Effective(rows[1],settings)));
            var again=Import.Read(path,settings,rows); Import.Merge(rows,again);
            Equal("24",Rdp.Effective(rows[0],settings)["Colors"]); Equal(3397,Rdp.Port(Rdp.Effective(rows[0],settings)));
        }
        public void Launch()
        {
            var c=new Connection{Name="Desk",Host="desk.invalid",Domain="LAB",Username="qa",Folder="West"};
            var settings=new Settings();
            ProcessStartInfo started=null;
            Rdp.Launch(c,settings,directory,false,false,info=>started=info);
            Equal("/v:desk.invalid",started.Arguments); Equal("mstsc.exe",Path.GetFileName(started.FileName));
            c.Port=3392; c.Admin=true; c.FullScreen=true;
            Equal("/v:desk.invalid:3392 /admin /f",Rdp.Arguments(c,settings,directory));
            c.Host="::1"; Equal("/v:[::1]:3392 /admin /f",Rdp.Arguments(c,settings,directory,true,true));
            c.Host="desk.invalid"; c.Port=3389; c.Admin=false; c.FullScreen=false;
            settings.SiteOptions["West"]=new Dictionary<string,string>{{"Port","3395"},{"RedirectClipboard","0"},{"Colors","16"},{"Resolution","fixed"},{"Width","1440"},{"Height","900"},{"DisplayWallpaper","0"}};
            c.Options["Colors"]="32"; c.Options["RedirectDiskDrives"]="1"; c.Options["LoadBalanceInfo"]="pool";
            c.Options["RDGatewayHostname"]="gateway.invalid"; c.Options["UnknownKey"]="never emit"; c.Id="../folder/name";
            var args=Rdp.Arguments(c,settings,directory,true);
            Check(args.EndsWith("\" /admin"),"Admin file launch"); var path=args.Split('"')[1];
            Equal(Path.GetFullPath(directory),Path.GetDirectoryName(Path.GetFullPath(path)));
            var lines=File.ReadAllLines(path);
            foreach (var line in new[]{"full address:s:desk.invalid:3395",@"username:s:LAB\qa","session bpp:i:32","desktopwidth:i:1440","desktopheight:i:900","redirectclipboard:i:0","disable wallpaper:i:1","drivestoredirect:s:*","gatewayhostname:s:gateway.invalid","loadbalanceinfo:s:pool"})
                Check(lines.Contains(line),line);
            Check(!lines.Any(l=>l.Contains("never emit")||l.StartsWith("password",StringComparison.OrdinalIgnoreCase)),"File allowlist");
            Rdp.Arguments(c,settings,directory,false,true); Check(File.ReadAllLines(path).Contains("screen mode id:i:2"),"Full screen override");
            c.Username="user\r\nredirectprinters:i:1"; bool rejected=false;
            try { Rdp.Arguments(c,settings,directory); } catch(InvalidDataException) { rejected=true; }
            Check(rejected,"Reject newline in login"); Check(!Rdp.ValidHost("host /f"),"Reject command switch");
            File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-2)); Rdp.Clean(directory); Check(!File.Exists(path),"Clean generated file");
        }
        public void CredentialLifecycle()
        {
            const string host="quaygate-selftest.invalid";
            try
            {
                Credentials.Remove(host); Equal(null,Credentials.User(host));
                using var secret=new SecureString();
                foreach(char c in Guid.NewGuid().ToString("N")) secret.AppendChar(c);
                secret.MakeReadOnly();
                Credentials.Set(host,@"LAB\test",secret); Equal(@"LAB\test",Credentials.User(host));
                Credentials.Set(host,@"LAB\replacement",secret); Equal(@"LAB\replacement",Credentials.User(host));
            }
            finally { Credentials.Remove(host); }
            Equal(null,Credentials.User(host));
        }
        public void SelectedEndpointProbe()
        {
            var contacted=new List<string>();
            using var probe=new Probe((host,port,token)=>{
                contacted.Add(Rdp.Endpoint(host,port)); return Task.FromResult(true);
            });
            var skipped=probe.Selected("skipped.invalid",3389);
            var chosen=probe.Selected("selected.invalid",3395);
            Equal(null,skipped.GetAwaiter().GetResult());
            Equal(true,chosen.GetAwaiter().GetResult());
            Equal(true,probe.Selected("selected.invalid",3395).GetAwaiter().GetResult());
            Equal("selected.invalid:3395",string.Join(",",contacted));
        }
    }
}
