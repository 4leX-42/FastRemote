using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using static Quaygate.Words;

namespace Quaygate
{
    public class SiteItem
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }
        public override string ToString()=>Label;
    }
    public partial class MainWindow : Window
    {
        readonly Store store;
        public Settings Config {get;private set;}
        List<Connection> rows;
        readonly Probe probe=new Probe();
        readonly bool isolated;
        bool updating,ready,closed;
        int searchVersion;
        CancellationTokenSource filter;
        readonly System.Windows.Threading.DispatcherTimer typing;
        Connection[] shown=new Connection[0];
        Connection selected;
        string selectedEndpoint;
        string SiteKey => (Sites.SelectedItem as SiteItem)?.Key;
        string SiteLabel(string site)=>string.IsNullOrEmpty(site) ? T("NoSite") : site;

        public MainWindow(Store data,Settings settings,List<Connection> list,bool smoke)
        {
            store=data; Config=settings; rows=list; isolated=smoke || Program.Offscreen;
            InitializeComponent();
            // With thousands of rows, search waits for a short pause in typing.
            typing=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(90)};
            typing.Tick+=(s,e)=>{ typing.Stop(); Filter(selected?.Id); };
            Reorder.Attach(Sites,i=>Sites.Items[i] is SiteItem item && item.Key!=null,
                (a,b)=>((ObservableCollection<SiteItem>)Sites.ItemsSource).Move(a,b),SaveSiteOrder);
            Appearance.Scale(this,Frame,Config.Scale);
            WindowChrome.GetWindowChrome(this).CaptionHeight=40*Config.Scale;
            Placement.Set(this,Config);
            ready=true;
            Rebuild(null,null);
            Loaded+=async(s,e)=>{
                if (!isolated) { Search.Focus(); await RefreshCredentials(); }
            };
            Closing+=(s,e)=>{
                if (!Program.Offscreen)
                {
                    var bounds=RestoreBounds;
                    if (!bounds.IsEmpty) { Config.Left=bounds.Left; Config.Top=bounds.Top; Config.Width=bounds.Width; Config.Height=bounds.Height; }
                    Config.Maximized=WindowState==WindowState.Maximized;
                }
                try { store.Save("settings.json",Config); }
                catch(Exception ex) { Record(ex); ReportError(); e.Cancel=true; return; }
                closed=true; probe.Dispose(); filter?.Cancel();
            };
        }
        internal void Record(Exception ex)=>store.Log(ex);
        internal void ReportError()=>Info.Text=T("Failure");
        async Task RefreshCredentials()
        {
            if (isolated) return;
            var snapshot=rows;
            var hosts=rows.Select(c=>c.Host).Where(Rdp.ValidHost).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var lookup=await Task.Run(()=>hosts.ToDictionary(h=>h,Credentials.User,StringComparer.OrdinalIgnoreCase));
            if (closed || snapshot!=rows) return;
            foreach (var c in rows)
            {
                if (c.Host!=null && lookup.TryGetValue(c.Host,out var user)) c.CredentialUser=user;
                c.Notify();
            }
            Detail();
        }
        void Rebuild(string site,string prefer)
        {
            foreach (var c in rows)
            {
                c.DisplayPort=Rdp.Port(Rdp.Effective(c,Config));
                c.Index();
            }
            updating=true;
            try
            {
                var counts=rows.GroupBy(c=>c.Site).ToDictionary(g=>g.Key,g=>g.Count());
                var entries=Config.Sites(rows).Select(k=>new SiteItem{Key=k,Label=SiteLabel(k),Count=counts[k]}).ToList();
                entries.Add(new SiteItem{Label=T("All"),Count=rows.Count});
                Sites.ItemsSource=new ObservableCollection<SiteItem>(entries);
                Sites.SelectedItem=((ObservableCollection<SiteItem>)Sites.ItemsSource).FirstOrDefault(s=>s.Key==site) ?? ((ObservableCollection<SiteItem>)Sites.ItemsSource).Last();
                List.ContextMenu=Menu("Connect","Console","Full","Cred","RemoveCred","Copy","Edit","Duplicate","Delete");
                Sites.ContextMenu=Menu("Rename","SiteOptions");
            }
            finally { updating=false; }
            Filter(prefer);
        }
        ContextMenu Menu(params string[] keys)
        {
            var menu=new ContextMenu();
            foreach (var key in keys)
            {
                var item=new MenuItem{Header=T(key),Tag=key};
                item.Click+=ActionClick; menu.Items.Add(item);
            }
            menu.Opened+=(s,e)=>{if (menu.PlacementTarget==Sites && SiteKey==null || menu.PlacementTarget==List && selected==null) menu.IsOpen=false;};
            return menu;
        }
        void SearchChanged(object sender,TextChangedEventArgs e)
        {
            if (!ready || updating) return;
            SearchHint.Visibility=Search.Text.Length==0 ? Visibility.Visible : Visibility.Collapsed;
            typing.Stop();
            // small lists filter on the keystroke itself; big ones wait for a pause in typing
            if (rows.Count<1000) Filter(selected?.Id); else typing.Start();
        }
        void SiteChanged(object sender,SelectionChangedEventArgs e) { if (ready && !updating) Filter(selected?.Id); }
        async void Filter(string prefer)
        {
            int version=++searchVersion;
            filter?.Cancel();
            var words=Connection.Fold(Search.Text).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            var site=SiteKey;
            var source=rows;
            SearchHint.Visibility=Search.Text.Length==0 ? Visibility.Visible : Visibility.Collapsed;
            ListTitle.Text=site==null ? T("ListTitle") : SiteLabel(site);
            Connection[] Find(CancellationToken token) => source.Where(c=>{token.ThrowIfCancellationRequested(); return (site==null || c.Site==site) && c.Matches(words);}).ToArray();
            Connection[] visible;
            if (source.Count>=4000)
            {
                using var request=new CancellationTokenSource();
                filter=request;
                try { visible=await Task.Run(()=>Find(request.Token)); }
                catch(OperationCanceledException) { return; }
                finally { if (filter==request) filter=null; }
                if (closed || version!=searchVersion) return;
            }
            else visible=Find(CancellationToken.None);
            updating=true;
            try
            {
                // Same rows as before: keep the list (and its scroll and selection) untouched.
                if (!visible.SequenceEqual(shown)) { List.ItemsSource=visible; shown=visible; }
                var want=visible.FirstOrDefault(c=>c.Id==prefer) ?? visible.FirstOrDefault();
                if (!ReferenceEquals(List.SelectedItem,want)) List.SelectedItem=want;
            }
            finally { updating=false; }
            Count.Text=visible.Length==rows.Count ? T("Count",visible.Length) : T("Filtered",visible.Length,rows.Count);
            EmptyList.Text=rows.Count==0 ? T("First") : T("Empty");
            EmptyList.Visibility=visible.Length==0 ? Visibility.Visible : Visibility.Collapsed;
            SelectRow();
        }
        void RowChanged(object sender,SelectionChangedEventArgs e) { if (ready && !updating) SelectRow(); }
        async void SelectRow()
        {
            var c=List.SelectedItem as Connection;
            bool changed=c!=selected || c?.HostLabel!=selectedEndpoint;
            selected=c; selectedEndpoint=c?.HostLabel;
            Detail();
            if (!changed) return;
            probe.Cancel();
            if (c==null) return;
            List.ScrollIntoView(c);
            if (isolated || !Rdp.ValidHost(c.Host)) return;
            c.Status="Checking"; c.Notify(); Detail();
            var result=await probe.Selected(c.Host,c.DisplayPort);
            void Show()
            {
                if (result==null || selected!=c || closed) return;
                c.Status=result.Value ? "Online" : "Offline"; c.Notify(); Detail();
            }
            if (Dispatcher.CheckAccess()) Show(); else _=Dispatcher.BeginInvoke((Action)Show);
        }
        void Detail()
        {
            var c=selected;
            Details.Visibility=c==null ? Visibility.Collapsed : Visibility.Visible;
            NoSel.Visibility=c==null ? Visibility.Visible : Visibility.Collapsed;
            if (c==null) return;
            DSite.Text=SiteLabel(c.Site); DName.Text=c.Name; DHost.Text=c.HostLabel;
            DUser.Text=string.IsNullOrEmpty(c.Login) ? "—" : c.Login;
            DCred.Text=c.CredentialUser ?? T("NoCred"); DDescr.Text=string.IsNullOrEmpty(c.Descr) ? "—" : c.Descr;
            DState.Text=T(c.Status ?? "Unknown");
        }
        void Commit(List<Connection> updated,string site,string id)
        {
            store.Save("connections.json",updated);
            rows=updated; Rebuild(site,id);
            Info.Text=T("Done"); _=RefreshCredentials();
        }
        void SelectUnderMouse(object sender,MouseButtonEventArgs e)
        {
            var list=(ListBox)sender;
            if (ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) is ListBoxItem item) item.IsSelected=true;
        }
        void SaveSiteOrder()
        {
            var order=((ObservableCollection<SiteItem>)Sites.ItemsSource).Where(x=>x.Key!=null).Select(x=>x.Key).ToList();
            var next=Store.Clone(Config); next.SiteOrder=order;
            try { store.Save("settings.json",next); Config=next; }
            catch(Exception ex) { Record(ex); }
        }
        void RowDoubleClick(object sender,MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(List,e.OriginalSource as DependencyObject) is ListBoxItem) Execute("Connect");
        }
        void ActionClick(object sender,RoutedEventArgs e)=>Execute((string)((FrameworkElement)sender).Tag);
        void Keys(object sender,KeyEventArgs e)
        {
            bool ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0;
            bool shift=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
            string action=null;
            if (ctrl && e.Key==Key.F) { Search.Focus(); Search.SelectAll(); }
            else if (e.Key==Key.Escape) { Search.Clear(); Search.Focus(); }
            else if (Search.IsKeyboardFocused && (e.Key==Key.Up || e.Key==Key.Down))
                List.SelectedIndex=Math.Min(List.Items.Count-1,Math.Max(0,List.SelectedIndex+(e.Key==Key.Up ? -1 : 1)));
            else if (e.Key==Key.Enter && (Search.IsKeyboardFocused || List.IsKeyboardFocusWithin || e.OriginalSource==this)) action=ctrl ? "Console" : shift ? "Full" : "Connect";
            else if (e.Key==Key.F2) action="Edit";
            else if (ctrl && e.Key==Key.N) action="New";
            else if (ctrl && e.Key==Key.D) action="Duplicate";
            else if (ctrl && e.Key==Key.C && (!Search.IsKeyboardFocused || Search.SelectionLength==0)) action="Copy";
            else if (e.Key==Key.Delete && !Search.IsKeyboardFocused) action="Delete";
            else return;
            e.Handled=true;
            if (action!=null) Execute(action);
        }
        void Execute(string action)
        {
            try
            {
                switch(action)
                {
                    case "Close": Close(); return;
                    case "Minimize": WindowState=WindowState.Minimized; return;
                    case "Maximize": WindowState=WindowState==WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; return;
                    case "Settings": SettingsDialog(); return;
                    case "New": Editor(null,false); return;
                    case "Import": ImportDialog(); return;
                    case "Bulk": CredentialDialog(true); return;
                    case "Rename": if (SiteKey!=null) RenameSite(); return;
                    case "SiteOptions": if (SiteKey!=null) SiteOptions(); return;
                }
                if (selected==null) return;
                switch(action)
                {
                    case "Connect": case "Console": case "Full":
                        if (!isolated) Rdp.Launch(selected,Config,store.RdpDirectory,action=="Console",action=="Full",info=>Process.Start(info));
                        break;
                    case "Copy": Clipboard.SetText(selected.Host); break;
                    case "Edit": Editor(selected,false); break;
                    case "Duplicate": Editor(selected,true); break;
                    case "Delete":
                        if (Confirm("Delete","DeleteConfirm",selected.Name)) Commit(rows.Where(c=>c!=selected).ToList(),SiteKey,null);
                        break;
                    case "Cred": CredentialDialog(false); break;
                    case "RemoveCred":
                        if (!isolated && Confirm("RemoveCred","RemoveConfirm",selected.Host)) { Credentials.Remove(selected.Host); _=RefreshCredentials(); }
                        break;
                }
            }
            catch(Exception ex) { Record(ex); ReportError(); }
        }
        bool Confirm(string title,string message,string value)
        {
            var dialog=new Dialog(this,title);
            dialog.Note(T(message,value)); return dialog.Run();
        }
    }
}
