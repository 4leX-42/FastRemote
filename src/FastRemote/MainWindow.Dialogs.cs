using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using static FastRemote.Words;

namespace FastRemote
{
    public partial class MainWindow
    {
        void Editor(Connection original,bool duplicate)
        {
            var c=original==null ? new Connection{Folder=CurrentSite ?? ""} : Store.Clone(original);
            if (duplicate) { c.Id=Guid.NewGuid().ToString(); c.Source="manual"; c.Name+=" (2)"; }
            var dlg=new Dialog(this,original==null ? "New" : duplicate ? "Duplicate" : "Edit"); dlg.AcceptText("Save");
            var name=dlg.Field("Name",c.Name,"FName");
            var host=dlg.Field("Host",c.Host,"FHost");
            var folder=dlg.Field("Site",c.Folder,"FFolder");
            var port=dlg.Field("Port",c.Options.TryGetValue("Port",out var own) ? own : !c.InheritPort && c.Port!=3389 ? c.Port.ToString() : "","FPort");
            port.ToolTip=T("Inherit");
            var domain=dlg.Field("Domain",c.Domain,"FDomain");
            var user=dlg.Field("User",c.Username,"FUser");
            var credential=CredentialSection(dlg,c,host,domain,user);
            var kind=dlg.Choices("Kind",Connection.Kinds,c.Kind ?? "",k=>T("K"+k),"FKind");
            var tags=dlg.Field("Tags",c.Tags,"FTags"); tags.ToolTip=T("TagsHint");
            var favorite=dlg.Add(new CheckBox{Content=T("Favorite"),IsChecked=c.Favorite},"FFavorite");
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
                // the credential goes first: if it fails the dialog stays open and nothing is saved twice
                error=credential();
                if (error!=null) return error;
                c.Host=host.Text.Trim(); c.Name=string.IsNullOrWhiteSpace(name.Text) ? c.Host : name.Text.Trim();
                c.Folder=folder.Text.Trim(); c.Domain=domain.Text.Trim(); c.Username=user.Text.Trim(); c.Descr=description.Text;
                c.Kind=(string)kind.SelectedValue=="" ? null : (string)kind.SelectedValue;
                c.Tags=string.Join(", ",tags.Text.Split(',').Select(t=>t.Trim()).Where(t=>t.Length>0)); if (c.Tags=="") c.Tags=null;
                c.Favorite=favorite.IsChecked==true;
                c.Port=p=="" ? 3389 : int.Parse(p);
                c.InheritPort=p=="";
                if (p=="") c.Options.Remove("Port"); else c.Options["Port"]=p;
                c.Admin=c.Options.TryGetValue("ConnectToConsole",out var admin) && admin=="1";
                c.FullScreen=c.Options.TryGetValue("Resolution",out var resolution) && resolution=="full";
                var updated=rows.ToList();
                if (original==null || duplicate) updated.Add(c); else updated[updated.IndexOf(original)]=c;
                store.Save("connections.json",updated);
                updating=true; FilterBox.Clear(); updating=false;
                rows=updated; ApplyHistory(); SyncLive(); Rebuild(View,c.Id); _=RefreshCredentials(); Flash(T("Done"));
                return null;
            });
        }
        // Visual tests list made-up credentials; the isolated window never reads or writes the real ones.
        internal static Func<List<Credentials.Stored>> CredentialPreview;
        static bool SameText(string a,string b)=>string.Equals(a ?? "",b ?? "",StringComparison.OrdinalIgnoreCase);
        // Credential of a connection in the editor. Windows keeps one per host (TERMSRV/<host>), shared by every
        // connection to that host; copying another one writes a new entry and leaves the original as it was.
        Func<string> CredentialSection(Dialog dlg,Connection c,TextBox host,TextBox domain,TextBox user)
        {
            if (isolated && CredentialPreview==null) return ()=>null;
            List<Credentials.Stored> stored;
            try { stored=isolated ? CredentialPreview() : Credentials.List(); } catch(Exception ex) { Record(ex); return ()=>null; }
            var own=stored.FirstOrDefault(x=>SameText(x.Host,c.Host));
            string originalLogin=c.Login;
            // servers are shown by the connection name the user knows, not by address
            List<string> Names(IEnumerable<string> hosts)=>hosts.SelectMany(h=>{
                var names=rows.Where(r=>SameText(r.Host,h)).Select(r=>r.Name).Distinct().ToList();
                return names.Count==0 ? new List<string>{h} : names; }).Distinct().ToList();
            var ownOthers=rows.Where(r=>r.Id!=c.Id && SameText(r.Host,c.Host)).Select(r=>r.Name).Distinct().ToList();
            var items=new List<CredOption>{own!=null
                ? new CredOption{Key="own",Icon="\uE8D7",Title=own.User,Detail=T("CredOwnDetail"),Shared=ownOthers.Count>0,Tip=ownOthers.Count>0 ? string.Join("\n",ownOthers) : null}
                : new CredOption{Key="own",Icon="\uE710",Title=T("CredNew"),Detail=T("CredNewDetail")}};
            foreach (var g in stored.Where(x=>x.Copyable && x!=own).GroupBy(x=>x.Group).OrderBy(g=>g.First().User,StringComparer.OrdinalIgnoreCase))
            {
                var names=Names(g.Select(x=>x.Host));
                items.Add(new CredOption{Key="copy:"+g.First().Host,Icon="\uE71B",Title=g.First().User,
                    Detail=names.Count==1 ? T("CredStoredOn",names[0]) : T("CredUsedOn",names.Count),
                    Shared=names.Count>1,Tip=string.Join("\n",names)});
            }
            if (own!=null) items.Add(new CredOption{Key="none",Icon="\uE711",Title=T("CredNoneTitle"),Detail=T("CredNoneDetail")});
            dlg.Label("Cred");
            var choice=dlg.Add(new ComboBox{ItemsSource=items,SelectedValuePath="Key",SelectedValue="own",
                ItemTemplate=(DataTemplate)FindResource("CredItem"),MaxDropDownHeight=320,Name="FCredCombo"},"FCred");
            AutomationProperties.SetName(choice,T("Cred"));
            var ownPanel=dlg.Add(new StackPanel(),"FCredOwn");
            dlg.Label("Password",ownPanel);
            var pwd=dlg.Add(new PasswordBox(),"FPwd",ownPanel);
            AutomationProperties.SetName(pwd,T("Password"));
            dlg.Add(new TextBlock{Text=T(own!=null ? "CredKeep" : "CredOptional"),TextWrapping=TextWrapping.Wrap,Style=(Style)FindResource("Caption")},null,ownPanel);
            var note=dlg.Note("","FCredNote");
            note.SetResourceReference(TextBlock.ForegroundProperty,"Sub");
            dlg.Closed+=(s,e)=>pwd.Clear();
            void Update()
            {
                var key=(string)choice.SelectedValue ?? "own";
                ownPanel.Visibility=key=="own" ? Visibility.Visible : Visibility.Collapsed;
                var h=host.Text.Trim();
                var others=rows.Where(r=>r.Id!=c.Id && SameText(r.Host,h)).Select(r=>r.Name).Distinct().ToList();
                note.Text=key.StartsWith("copy:") ? T("CredCopy") :
                    key=="none" ? T("CredRemoveNote",own.Host)+(others.Count>0 ? ". "+T("CredShared",string.Join(", ",others.Take(4))) : "") :
                    others.Count>0 ? T("CredShared",string.Join(", ",others.Take(4))) : "";
                note.Visibility=note.Text=="" ? Visibility.Collapsed : Visibility.Visible;
            }
            choice.SelectionChanged+=(s,e)=>Update();
            host.TextChanged+=(s,e)=>Update();
            Update();
            return ()=>{
                if (isolated) return T("Failure");
                var key=(string)choice.SelectedValue ?? "own";
                var target=host.Text.Trim();
                var d=domain.Text.Trim(); var u=user.Text.Trim();
                var login=u=="" || d=="" ? u : d+"\\"+u;
                try
                {
                    if (key=="none") { Credentials.Remove(own.Host); return null; }
                    if (key.StartsWith("copy:"))
                    {
                        var from=key.Substring(5);
                        Credentials.Copy(from,target);
                        if (u=="")
                        {
                            // the connection shows the user of the credential it now uses
                            var source=stored.First(x=>SameText(x.Host,from)).User;
                            var cut=source.IndexOf('\\');
                            domain.Text=cut>0 ? source.Substring(0,cut) : d; user.Text=cut>0 ? source.Substring(cut+1) : source;
                        }
                        return null;
                    }
                    using var secret=pwd.SecurePassword;
                    if (secret.Length>0)
                    {
                        var name=login!="" ? login : own?.User;
                        if (string.IsNullOrWhiteSpace(name)) return T("NeedUser");
                        Credentials.Set(target,name,secret);
                        return null;
                    }
                    if (own==null) return null;
                    bool moved=!SameText(own.Host,target), renamed=login!="" && !SameText(login,originalLogin) && !SameText(login,own.User);
                    if (!moved && !renamed) return null;
                    if (!own.Copyable) return T("CredRetype");
                    Credentials.Copy(own.Host,target,renamed ? login : null);
                    return null;
                }
                catch(Exception ex) { Record(ex); return T("Failure"); }
                finally { _=RefreshCredentials(); }
            };
        }
        void ImportDialog()
        {
            var dlg=new Dialog(this,"Import"); dlg.AcceptText("Import");
            dlg.Note(T("ImportHelp"),"IText");
            string defaultFile=isolated ? Path.Combine(store.Home,"confCons.xml") :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"mRemoteNG","confCons.xml");
            var path=dlg.Field("ImportPath",defaultFile,"IPath");
            dlg.Run(()=>{
                var next=Store.Clone(Config);
                var parsed=Import.Read(path.Text.Trim(),next,rows);
                var updated=Store.Clone(rows); Import.Merge(updated,parsed);
                SaveProfiles(updated,next);
                ApplyHistory(); SyncLive(); Rebuild(View,selected?.Id); _=RefreshCredentials(); Flash(T("Imported",parsed.Count)); return null;
            });
        }
        void CredentialDialog(bool bulk)
        {
            if (!bulk && selected==null) return;
            var dlg=new Dialog(this,bulk ? "Bulk" : "Cred"); dlg.AcceptText("Save");
            var c=selected;
            var site=CurrentSite ?? c?.Site;
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
                Flash(T("CredentialCount",count)); return null;
            });
        }
        void RenameSite()
        {
            var old=CurrentSite;
            var dlg=new Dialog(this,"Rename"); dlg.AcceptText("Save");
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
                SyncLive(); Rebuild("site:"+value,selected?.Id); _=RefreshCredentials(); return null;
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
            var site=CurrentSite;
            var next=Store.Clone(Config);
            if (!next.SiteOptions.TryGetValue(site,out var overrides)) next.SiteOptions[site]=overrides=new Dictionary<string,string>();
            var dlg=new Dialog(this,"SiteOptions"); dlg.AcceptText("Save"); dlg.Note(SiteLabel(site));
            var options=dlg.OptionFields(overrides,true);
            dlg.Run(()=>{
                var error=options(); if (error!=null) return error;
                store.Save("settings.json",next); Config=next; Rebuild(View,selected?.Id); return null;
            });
        }
    }
}
