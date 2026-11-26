using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using static Quaygate.Words;

namespace Quaygate
{
    public partial class MainWindow
    {
        void Editor(Connection original,bool duplicate)
        {
            var c=original==null ? new Connection{Folder=SiteKey ?? ""} : Store.Clone(original);
            if (duplicate) { c.Id=Guid.NewGuid().ToString(); c.Source="manual"; c.Name+=" (2)"; }
            var dlg=new Dialog(this,original==null ? "New" : duplicate ? "Duplicate" : "Edit");
            var name=dlg.Field("Name",c.Name,"FName");
            var host=dlg.Field("Host",c.Host,"FHost");
            var folder=dlg.Field("Site",c.Folder,"FFolder");
            var port=dlg.Field("Port",c.Options.TryGetValue("Port",out var own) ? own : !c.InheritPort && c.Port!=3389 ? c.Port.ToString() : "","FPort");
            port.ToolTip=T("Inherit");
            var domain=dlg.Field("Domain",c.Domain,"FDomain");
            var user=dlg.Field("User",c.Username,"FUser");
            var description=dlg.Field("Description",c.Descr,"FDescr");
            description.AcceptsReturn=true; description.TextWrapping=TextWrapping.Wrap;
            if (c.Admin && !c.Options.ContainsKey("ConnectToConsole")) c.Options["ConnectToConsole"]="1";
            if (c.FullScreen && !c.Options.ContainsKey("Resolution")) c.Options["Resolution"]="full";
            var options=dlg.OptionFields(c.Options,false);
            dlg.Run(()=>{
                if (!Rdp.ValidHost(host.Text.Trim())) return T("NeedHost");
                var p=port.Text.Trim();
                if (p!="" && !Option.All.First(o=>o.Id=="Port").Valid(p)) return T("Invalid",T("Port"));
                var error=options();
                if (error!=null) return error;
                c.Host=host.Text.Trim(); c.Name=string.IsNullOrWhiteSpace(name.Text) ? c.Host : name.Text.Trim();
                c.Folder=folder.Text.Trim(); c.Domain=domain.Text.Trim(); c.Username=user.Text.Trim(); c.Descr=description.Text;
                c.Port=p=="" ? 3389 : int.Parse(p);
                c.InheritPort=p=="";
                if (p=="") c.Options.Remove("Port"); else c.Options["Port"]=p;
                c.Admin=c.Options.TryGetValue("ConnectToConsole",out var admin) && admin=="1";
                c.FullScreen=c.Options.TryGetValue("Resolution",out var resolution) && resolution=="full";
                var updated=rows.ToList();
                if (original==null || duplicate) updated.Add(c); else updated[updated.IndexOf(original)]=c;
                store.Save("connections.json",updated);
                updating=true; Search.Clear(); updating=false;
                rows=updated; Rebuild(SiteKey==null ? null : c.Site,c.Id); _=RefreshCredentials(); Info.Text=T("Done");
                return null;
            });
        }
        void ImportDialog()
        {
            var dlg=new Dialog(this,"Import");
            dlg.Note(T("ImportHelp"),"IText");
            string defaultFile=isolated ? Path.Combine(store.Home,"confCons.xml") :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"mRemoteNG","confCons.xml");
            var path=dlg.Field("ImportPath",defaultFile,"IPath");
            dlg.Run(()=>{
                var next=Store.Clone(Config);
                var parsed=Import.Read(path.Text.Trim(),next,rows);
                var updated=Store.Clone(rows); Import.Merge(updated,parsed);
                SaveProfiles(updated,next);
                Rebuild(SiteKey,selected?.Id); _=RefreshCredentials(); Info.Text=T("Imported",parsed.Count); return null;
            });
        }
        void CredentialDialog(bool bulk)
        {
            if (!bulk && selected==null) return;
            var dlg=new Dialog(this,bulk ? "Bulk" : "Cred");
            var c=selected;
            var site=SiteKey ?? c?.Site;
            var login=c?.Login;
            RadioButton bySite=null,same=null;
            if (bulk)
            {
                dlg.Add(new RadioButton{Content=T("All")+" ("+rows.Count+")",IsChecked=true},"BAll");
                bySite=dlg.Add(new RadioButton{Content=T("Site")+" · "+SiteLabel(site),IsEnabled=site!=null},"BSite");
                same=dlg.Add(new RadioButton{Content=T("Same")+" · "+login,IsEnabled=!string.IsNullOrEmpty(login)},"BSame");
                dlg.Note(T("OwnUser"));
            }
            else dlg.Note(c.HostLabel);
            var user=dlg.Field("User",bulk ? "" : c.CredentialUser ?? c.Login,bulk ? "BUser" : "CUser");
            dlg.Label("Password");
            var pwd=dlg.Add(new PasswordBox(),bulk ? "BPwd" : "CPwd");
            dlg.Closed+=(s,e)=>pwd.Clear();
            dlg.Run(()=>{
                using var secret=pwd.SecurePassword;
                if (secret.Length==0) return T("NeedPassword");
                var targets=bulk ? rows.AsEnumerable() : new[]{c};
                if (bySite?.IsChecked==true) targets=targets.Where(x=>x.Site==site);
                if (same?.IsChecked==true) targets=targets.Where(x=>string.Equals(x.Login,login,StringComparison.OrdinalIgnoreCase));
                var operations=targets.Select(x=>new {x.Host,User=string.IsNullOrWhiteSpace(user.Text) ? x.Login : user.Text.Trim()})
                    .GroupBy(x=>x.Host,StringComparer.OrdinalIgnoreCase).ToList();
                if (operations.Count==0 || operations.Any(g=>g.Any(x=>string.IsNullOrWhiteSpace(x.User)) ||
                    g.Select(x=>x.User).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1)) return T("NeedUser");
                if (operations.Any(g=>!Rdp.ValidHost(g.Key))) return T("NeedHost");
                if (isolated) return T("Failure");
                int count=0;
                try { foreach (var group in operations) { Credentials.Set(group.Key,group.First().User,secret); count++; } }
                catch(Exception ex) { Record(ex); return T("Partial",count); }
                finally { _=RefreshCredentials(); }
                Info.Text=T("CredentialCount",count); return null;
            });
        }
        void RenameSite()
        {
            var old=SiteKey;
            var dlg=new Dialog(this,"Rename");
            var field=dlg.Field("Name",old,"SiteName");
            dlg.Run(()=>{
                var value=field.Text.Trim();
                if (value==old) return null;
                if (value=="" || value.Contains(" / ") || rows.Any(c=>c.Site==value)) return T("Invalid",T("Site"));
                var updated=Store.Clone(rows);
                foreach (var c in updated.Where(c=>c.Site==old)) c.Folder=value+(c.Folder ?? "").Substring(old.Length);
                var next=Store.Clone(Config);
                next.SiteOrder=Config.Sites(rows).Select(s=>s==old ? value : s).ToList();
                if (next.SiteOptions.TryGetValue(old,out var options)) { next.SiteOptions.Remove(old); next.SiteOptions[value]=options; }
                SaveProfiles(updated,next);
                Rebuild(value,selected?.Id); _=RefreshCredentials(); return null;
            });
        }
        void SaveProfiles(List<Connection> updated,Settings next)
        {
            store.Save("settings.json",next);
            try { store.Save("connections.json",updated); }
            catch { store.Save("settings.json",Config); throw; }
            Config=next; rows=updated;
        }
        void SiteOptions()
        {
            var site=SiteKey;
            var next=Store.Clone(Config);
            if (!next.SiteOptions.TryGetValue(site,out var overrides)) next.SiteOptions[site]=overrides=new Dictionary<string,string>();
            var dlg=new Dialog(this,"SiteOptions"); dlg.Note(SiteLabel(site));
            var options=dlg.OptionFields(overrides,true);
            dlg.Run(()=>{
                var error=options(); if (error!=null) return error;
                store.Save("settings.json",next); Config=next; Rebuild(site,selected?.Id); return null;
            });
        }
        void SettingsDialog()
        {
            var next=Store.Clone(Config);
            var dlg=new Dialog(this,"Settings");
            dlg.Label("Scale");
            var sizes=dlg.Add(new DockPanel());
            var scaleText=new TextBlock{Text=next.Scale.ToString("P0"),MinWidth=52,VerticalAlignment=VerticalAlignment.Center};
            DockPanel.SetDock(scaleText,Dock.Right);
            dlg.Add(scaleText,"ScaleValue",sizes);
            var scale=dlg.Add(new Slider{Minimum=.9,Maximum=1.6,TickFrequency=.1,SmallChange=.1,LargeChange=.1,
                IsSnapToTickEnabled=true,Value=next.Scale,VerticalAlignment=VerticalAlignment.Center},"Scale",sizes);
            AutomationProperties.SetName(scale,T("Scale"));
            dlg.Label("Theme");
            var theme=dlg.Add(new ListBox{ItemsSource=new[]{"Carbon","Graphite","Light","Contrast"}.Select(k=>new KeyValuePair<string,string>(k,T(k))).ToArray(),
                DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=next.Theme ?? "Carbon"},"Theme");
            var accent=dlg.Field("Accent",next.Accent,"Accent");
            var language=dlg.Choices("Language",new[]{"auto","es","en"},next.Language ?? "auto",v=>T(v),"Language");
            dlg.Label("Order");
            var order=Config.Sites(rows);
            var list=dlg.Add(new ListBox{Height=80},"SiteOrder");
            void ShowOrder(int at) { list.ItemsSource=order.Select(SiteLabel).ToArray(); list.SelectedIndex=at; }
            ShowOrder(order.Count>0 ? 0 : -1);
            void Move(int direction)
            {
                int index=list.SelectedIndex, dest=index+direction;
                if (index<0 || dest<0 || dest>=order.Count) return;
                var value=order[index]; order.RemoveAt(index); order.Insert(dest,value); ShowOrder(dest);
            }
            var buttons=dlg.Add(new WrapPanel());
            dlg.Button("Up","MoveUp",()=>Move(-1),buttons); dlg.Button("Down","MoveDown",()=>Move(1),buttons);
            dlg.Button("Folder","OpenFolder",()=>{ if (!isolated) Process.Start(new ProcessStartInfo(store.Home){UseShellExecute=true}); });
            void Preview()
            {
                next.Scale=Math.Round(scale.Value,1);
                scaleText.Text=next.Scale.ToString("P0");
                next.Theme=(string)theme.SelectedValue; next.Accent=accent.Text.Trim();
                next.Language=(string)language.SelectedValue;
                if (Appearance.IsAccent(next.Accent)) Appearance.Apply(next);
            }
            scale.ValueChanged+=(s,e)=>Preview(); theme.SelectionChanged+=(s,e)=>Preview();
            language.SelectionChanged+=(s,e)=>Preview(); accent.TextChanged+=(s,e)=>Preview();
            bool saved=dlg.Run(()=>{
                if (!Appearance.IsAccent(accent.Text.Trim())) return T("Invalid",T("Accent"));
                Preview(); next.SiteOrder=order;
                store.Save("settings.json",next); Config=next; return null;
            });
            Appearance.Apply(Config);
            WindowChromeUpdate();
            Rebuild(SiteKey,selected?.Id);
            if (saved) Info.Text=T("Done");
        }
        void WindowChromeUpdate() => System.Windows.Shell.WindowChrome.GetWindowChrome(this).CaptionHeight=48*Config.Scale;
    }
}
