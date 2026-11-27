using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using static FastRemote.Words;

namespace FastRemote
{
    public partial class MainWindow : Window
    {
        readonly Store store;
        public Settings Config {get;private set;}
        List<Connection> rows;
        History history;
        readonly SessionHub hub=new SessionHub();
        readonly Availability reach;
        readonly ObservableCollection<LiveItem> live=new ObservableCollection<LiveItem>();
        readonly ObservableCollection<NavItem> nav=new ObservableCollection<NavItem>();
        readonly bool isolated;
        bool updating,ready,closed,checkingAll;
        readonly DispatcherTimer typing,clock,settle;
        CancellationTokenSource selectedCheck,batchCheck;
        DateTime nextCheck=DateTime.MaxValue,nextCheckAll=DateTime.MaxValue,lastCheckAll=DateTime.MinValue;
        Connection[] shown=new Connection[0];
        Connection selected;
        string selectedEndpoint;
        string view="all";
        int credentialCount=-1;
        string View => view;

        public MainWindow(Store data,Settings settings,List<Connection> list,bool smoke)
        {
            store=data; Config=settings; rows=list; isolated=smoke || Program.Offscreen;
            history=store.Load<History>("history.json");
            reach=new Availability{Timeout=TimeSpan.FromMilliseconds(Config.TimeoutMs),MaxConcurrent=2};
            reach.Changed+=ReachChanged;
            InitializeComponent();
            typing=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(90)};
            typing.Tick+=(s,e)=>{ typing.Stop(); Filter(selected?.Id); };
            // Rapid keyboard navigation never starts a sequence of checks: wait until the selection settles.
            settle=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(220)};
            settle.Tick+=(s,e)=>{ settle.Stop(); CheckSelected(false); };
            clock=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
            clock.Tick+=(s,e)=>Tick();
            Nav.ItemsSource=nav;
            SessionTabs.ItemsSource=live;
            Reorder.Attach(Nav,i=>i<nav.Count && nav[i].Movable,MoveSite,SaveSiteOrder);
            hub.Ended+=SessionEnded;
            Appearance.Scale(this,Frame,Config.Scale);
            Layout();
            Placement.Set(this,Config);
            List.AddHandler(ButtonBase.ClickEvent,new RoutedEventHandler(HeaderClick));
            List.SizeChanged+=(s,e)=>Fit();
            List.ContextMenu=new ContextMenu(); Nav.ContextMenu=new ContextMenu();
            List.ContextMenuOpening+=(s,e)=>{ if (!BuildRowMenu(List.ContextMenu)) e.Handled=true; };
            Nav.ContextMenuOpening+=(s,e)=>{ if (!BuildNavMenu()) e.Handled=true; };
            view=string.IsNullOrEmpty(Config.View) || Config.View.StartsWith("conn:",StringComparison.Ordinal) ? "all" : Config.View;
            ready=true;
            ApplyHistory();
            if (!isolated && Config.AdoptSessions) Adopt();
            Rebuild(view,null);
            SyncSessions();
            Loaded+=async(s,e)=>{
                if (isolated) return;
                List.Focus();
                NetworkChange.NetworkAddressChanged+=NetworkChanged;
                clock.Start();
                // the list opens with real states: every saved server is checked once at start, one at a time
                if (Config.CheckAllOnStart) CheckAll();
                await RefreshCredentials();
            };
            Closing+=(s,e)=>{
                if (!Program.Offscreen)
                {
                    var bounds=RestoreBounds;
                    if (!bounds.IsEmpty) { Config.Left=bounds.Left; Config.Top=bounds.Top; Config.Width=bounds.Width; Config.Height=bounds.Height; }
                    Config.Maximized=WindowState==WindowState.Maximized;
                }
                SaveNotes();
                SaveLayout(); Config.View=view;
                try { store.Save("settings.json",Config); }
                catch(Exception ex) { Record(ex); ReportError(); e.Cancel=true; return; }
                closed=true; clock.Stop(); settle.Stop();
                NetworkChange.NetworkAddressChanged-=NetworkChanged;
                selectedCheck?.Cancel(); batchCheck?.Cancel(); reach.Dispose();
            };
        }
        internal void Record(Exception ex)=>store.Log(ex);
        internal void ReportError()=>Summary.Text=T("Failure");
        void Flash(string text)=>Summary.Text=text;

        // ---------- layout ----------
        void Layout()
        {
            WindowChrome.GetWindowChrome(this).CaptionHeight=40*Config.Scale;
            Application.Current.Resources["RowHeight"]=Config.Density=="comfortable" ? 40d : 34d;
            bool side=!Config.SidebarCollapsed;
            SideColumn.Width=new GridLength(side ? Config.SidebarWidth : 0);
            SideSplitColumn.Width=new GridLength(side ? 5 : 0);
            Side.Visibility=SideSplit.Visibility=side ? Visibility.Visible : Visibility.Collapsed;
            bool inspector=!Config.InspectorHidden;
            InspectorColumn.Width=new GridLength(inspector ? Config.InspectorWidth : 0);
            SplitColumn.Width=new GridLength(inspector ? 5 : 0);
            Inspector.Visibility=InspectorSplit.Visibility=inspector ? Visibility.Visible : Visibility.Collapsed;
            BuildColumns();
        }
        void SaveLayout()
        {
            if (!Config.SidebarCollapsed && SideColumn.ActualWidth>=180) Config.SidebarWidth=Math.Round(SideColumn.ActualWidth);
            if (!Config.InspectorHidden && InspectorColumn.ActualWidth>=300) Config.InspectorWidth=Math.Round(InspectorColumn.ActualWidth);
            if (List.View is GridView grid)
                foreach (var column in grid.Columns)
                    if (column.Header is FrameworkElement h && h.Tag is string id && column.ActualWidth>20)
                    {
                        // widths set by Fit are not the user's choice
                        if (id!="Name" && fitted.TryGetValue(id,out var f) && Math.Abs(column.ActualWidth-f)<1) continue;
                        // the name column fills the free width until the user resizes it
                        if (id=="Name" && Math.Abs(column.ActualWidth-fittedName)<1) Config.ColumnWidths.Remove(id);
                        else Config.ColumnWidths[id]=Math.Round(column.ActualWidth);
                    }
        }
        void SplitDone(object sender,DragCompletedEventArgs e)
        {
            SideColumn.Width=new GridLength(Math.Max(180,Math.Min(480,SideColumn.ActualWidth)));
            if (!Config.InspectorHidden) InspectorColumn.Width=new GridLength(Math.Max(360,Math.Min(600,InspectorColumn.ActualWidth)));
            SaveLayout();
        }

        // ---------- columns ----------
        static readonly (string Id,string Label,string Template,double Width)[] ColumnSet={
            ("Name","Name","CellName",210),("Host","AddressCol","CellHost",140),("Type","TypeCol","CellType",70),("Reach","StateCol","CellReach",130),
            ("Site","Site","CellSite",120),("Session","Session","CellSession",110),("User","User","CellUser",200),("Last","LastUsed","CellLast",140),
            ("Latency","Latency","CellLatency",90),("Cred","Cred","CellCred",40),("Star","","CellStar",44)};
        IEnumerable<string> Hidden => Config.HiddenColumns ?? Settings.DefaultHidden.ToList();
        double fittedName=-1;
        void BuildColumns()
        {
            SaveLayout();
            var grid=new GridView{AllowsColumnReorder=false};
            foreach (var c in ColumnSet.Where(c=>c.Id=="Name" || c.Id=="Star" || !Hidden.Contains(c.Id)))
            {
                var label=new TextBlock{Tag=c.Id,Text=c.Label=="" ? "" : T(c.Label)};
                if (Config.SortBy==c.Id) label.Text+=Config.SortDescending ? "  ↓" : "  ↑";
                grid.Columns.Add(new GridViewColumn{Header=label,CellTemplate=(DataTemplate)FindResource(c.Template),
                    Width=Config.ColumnWidths.TryGetValue(c.Id,out var w) && w>=24 && w<=900 ? w : c.Width});
            }
            var header=new ContextMenu();
            foreach (var item in ColumnItems()) header.Items.Add(item);
            grid.ColumnHeaderContextMenu=header;
            List.View=grid;
            Dispatcher.BeginInvoke((Action)Fit,DispatcherPriority.Loaded);
        }
        IEnumerable<MenuItem> ColumnItems()
        {
            foreach (var c in ColumnSet.Where(c=>c.Id!="Name" && c.Id!="Star"))
            {
                var item=new MenuItem{Header=T(c.Label),IsCheckable=true,IsChecked=!Hidden.Contains(c.Id),Tag=c.Id,StaysOpenOnClick=true};
                item.Click+=(s,e)=>ToggleColumn((string)((MenuItem)s).Tag);
                yield return item;
            }
        }
        // Smallest useful width of each column; when the table is narrower, the last ones give way first.
        static readonly Dictionary<string,double> ColumnMin=new Dictionary<string,double>{
            ["Name"]=150,["Host"]=112,["Reach"]=118,["User"]=110,["Last"]=104,["Site"]=90,["Session"]=96,["Latency"]=64};
        static readonly string[] DropOrder={"Latency","Session","Site","Last","User"};
        readonly Dictionary<string,double> fitted=new Dictionary<string,double>();
        void Fit()
        {
            if (!(List.View is GridView grid) || List.ActualWidth<100) return;
            var cols=grid.Columns.Select(c=>(Column:c,Id:(c.Header as FrameworkElement)?.Tag as string)).Where(x=>x.Id!=null).ToList();
            // a width the user dragged since the last fit is kept as theirs
            foreach (var x in cols)
                if (fitted.TryGetValue(x.Id,out var f) && f>0 && x.Column.ActualWidth>20 && Math.Abs(x.Column.ActualWidth-f)>=1)
                    Config.ColumnWidths[x.Id]=Math.Round(x.Column.ActualWidth);
            double available=Math.Floor(List.ActualWidth-SystemParameters.VerticalScrollBarWidth-8);
            var widths=cols.ToDictionary(x=>x.Id,x=>Config.ColumnWidths.TryGetValue(x.Id,out var w) && w>=24 && w<=900 ? w : ColumnSet.First(s=>s.Id==x.Id).Width);
            double Min(string id)=>ColumnMin.TryGetValue(id,out var m) ? Math.Min(m,widths[id]) : widths[id];
            foreach (var id in DropOrder)
            {
                if (!widths.ContainsKey(id) || widths.Sum(p=>p.Value==0 ? 0 : Min(p.Key))<=available) continue;
                widths[id]=0;
            }
            bool freeName=!Config.ColumnWidths.ContainsKey("Name");
            if (freeName) widths["Name"]=Math.Max(ColumnMin["Name"],available-widths.Where(p=>p.Key!="Name").Sum(p=>p.Value));
            double over=widths.Values.Sum()-available;
            if (over>0)
            {
                double slack=widths.Where(p=>p.Value>0).Sum(p=>p.Value-Min(p.Key));
                if (slack>0)
                    foreach (var id in widths.Keys.ToList())
                        if (widths[id]>0) widths[id]=Math.Floor(widths[id]-(widths[id]-Min(id))*Math.Min(1,over/slack));
            }
            fitted.Clear();
            foreach (var x in cols) { x.Column.Width=widths[x.Id]; fitted[x.Id]=widths[x.Id]; }
            fittedName=freeName ? widths["Name"] : -1;
        }
        void ToggleColumn(string id)
        {
            var hidden=Hidden.ToList();
            if (!hidden.Remove(id)) hidden.Add(id);
            Config.HiddenColumns=hidden; BuildColumns();
        }
        void HeaderClick(object sender,RoutedEventArgs e)
        {
            if (!(e.OriginalSource is GridViewColumnHeader h) || !(h.Column?.Header is FrameworkElement f) || !(f.Tag is string id) || id=="Star" || id=="Cred") return;
            if (Config.SortBy==id && Config.SortDescending) Config.SortBy=null;
            else { Config.SortDescending=Config.SortBy==id; Config.SortBy=id; }
            BuildColumns(); shown=new Connection[0]; Filter(selected?.Id);
        }

        // ---------- data ----------
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
            credentialCount=lookup.Count(p=>p.Value!=null);
            CredCount.Text=credentialCount+"/"+hosts.Length;
            Detail();
        }
        void ApplyHistory()
        {
            foreach (var c in rows)
            {
                var entry=history.Of(c.Id);
                c.LastUsed=entry?.WhenUtc; c.LastProblem=entry?.Problem;
            }
        }
        void SaveHistory()
        {
            try { history.Forget(rows.Select(c=>c.Id)); store.Save("history.json",history); }
            catch(Exception ex) { Record(ex); }
        }
        void Rebuild(string keep,string prefer)
        {
            foreach (var c in rows)
            {
                c.DisplayPort=Rdp.Port(Rdp.Effective(c,Config));
                c.Index();
                c.Reach=reach.Get(c.Host,c.DisplayPort);
            }
            shown=new Connection[0];
            BuildNav(keep ?? view,prefer);
            Filter(prefer);
        }
        Connection Row(string id)=>rows.FirstOrDefault(c=>c.Id==id);

        // ---------- navigation: views and the site tree ----------
        void BuildNav(string keep,string selectId=null)
        {
            updating=true;
            try
            {
                nav.Clear();
                var sub=(Brush)FindResource("Sub"); var gold=(Brush)FindResource("Accent"); var text=(Brush)FindResource("Text");
                void Item(string key,string label,string icon,int count,Brush tint,int depth=0,bool children=false,bool expanded=false,string site=null,string id=null,bool on=false)
                    => nav.Add(new NavItem{Key=key,Label=label,Icon=icon,Count=count>0 ? count.ToString(CultureInfo.CurrentCulture) : "",Depth=depth,HasChildren=children,Expanded=expanded,
                        Site=site,ConnectionId=id,Tint=tint,LiveVisibility=on ? Visibility.Visible : Visibility.Collapsed});
                Item("fav",T("Favorites"),"\uE734",rows.Count(c=>c.Favorite),text);
                Item("all",T("AllConnections"),"\uE8FD",rows.Count,text);
                Item("live",T("LiveView"),"\uE7F4",rows.Count(c=>c.Live!=null),text);
                Item("recent",T("Recent"),"\uE81C",RecentRows().Count(),text);
                int down=rows.Count(c=>c.Reach.State==Reach.Down);
                if (down>0 || keep=="down") Item("down",T("DownView"),"\uE7BA",down,(Brush)FindResource("Down"));
                nav.Add(new NavItem{IsHeader=true,HeaderAction=true,Label=T("SitesHeader").ToUpper(CultureInfo.CurrentCulture)});
                foreach (var site in Config.Sites(rows))
                {
                    var members=rows.Where(c=>c.Site==site).ToList();
                    bool open=Config.Expanded.Contains("site:"+site) || members.Any(c=>c.Id==selectId);
                    Item("site:"+site,string.IsNullOrEmpty(site) ? T("NoSite") : site,"\uE8B7",members.Count,gold,0,members.Count>0,open,site);
                    if (!open) continue;
                    // sub folders (mRemoteNG imports keep their tree) and then the connections themselves
                    foreach (var group in members.GroupBy(c=>(c.Folder ?? "").Contains(" / ") ? c.Folder.Substring(c.Folder.IndexOf(" / ",StringComparison.Ordinal)+3) : ""))
                    {
                        int depth=1;
                        if (group.Key!="")
                        {
                            var key="folder:"+site+" / "+group.Key;
                            bool folderOpen=Config.Expanded.Contains(key) || group.Any(c=>c.Id==selectId);
                            Item(key,group.Key,"\uE8B7",group.Count(),gold,1,true,folderOpen,site);
                            if (!folderOpen) continue;
                            depth=2;
                        }
                        foreach (var c in group) Item("conn:"+c.Id,c.Name,c.KindIcon,0,sub,depth,false,false,site,c.Id,c.Live!=null);
                    }
                }
                var target=selectId!=null && keep!=null && keep.StartsWith("conn:",StringComparison.Ordinal) ? "conn:"+selectId : keep;
                var pick=nav.FirstOrDefault(n=>n.Key==target) ?? nav.FirstOrDefault(n=>n.Key==view) ?? nav.First(n=>n.Key=="all");
                Nav.SelectedItem=pick; lastNav=pick;
            }
            finally { updating=false; }
        }
        void RefreshNav()
        {
            var keep=(Nav.SelectedItem as NavItem)?.Key ?? view;
            var id=(Nav.SelectedItem as NavItem)?.ConnectionId;
            bool shape=rows.Any(c=>c.Reach.State==Reach.Down)!=nav.Any(n=>n.Key=="down");
            if (shape) { BuildNav(keep,id); return; }
            foreach (var n in nav)
            {
                int? count=n.Key switch { "fav"=>rows.Count(c=>c.Favorite), "live"=>rows.Count(c=>c.Live!=null), "recent"=>RecentRows().Count(),
                    "down"=>rows.Count(c=>c.Reach.State==Reach.Down), _=>(int?)null };
                bool changed=false;
                if (count!=null) { var t=count>0 ? count.Value.ToString(CultureInfo.CurrentCulture) : ""; if (t!=n.Count) { n.Count=t; changed=true; } }
                if (n.ConnectionId!=null)
                {
                    var v=Row(n.ConnectionId)?.Live!=null ? Visibility.Visible : Visibility.Collapsed;
                    if (v!=n.LiveVisibility) { n.LiveVisibility=v; changed=true; }
                }
                if (changed) n.Notify();
            }
        }
        IEnumerable<Connection> RecentRows()=>history.Recent.Select(e=>Row(e.Id)).Where(c=>c!=null);
        IEnumerable<Connection> InView(string v)
        {
            if (v=="fav") return rows.Where(c=>c.Favorite);
            if (v=="live") return rows.Where(c=>c.Live!=null);
            if (v=="recent") return RecentRows();
            if (v=="down") return rows.Where(c=>c.Reach.State==Reach.Down);
            if (v.StartsWith("site:",StringComparison.Ordinal)) { var site=v.Substring(5); return rows.Where(c=>c.Site==site); }
            if (v.StartsWith("folder:",StringComparison.Ordinal)) { var path=v.Substring(7); return rows.Where(c=>c.Folder==path || (c.Folder ?? "").StartsWith(path+" / ",StringComparison.Ordinal)); }
            return rows;
        }
        string ViewLabel(string v)
        {
            if (v=="all") return T("ConnectionsGroup");
            return nav.FirstOrDefault(n=>n.Key==v)?.Label ?? T("ConnectionsGroup");
        }
        NavItem lastNav;
        void NavChanged(object sender,SelectionChangedEventArgs e)
        {
            if (!ready || updating) return;
            if (!(Nav.SelectedItem is NavItem n)) return;
            if (n.IsHeader) { updating=true; Nav.SelectedItem=lastNav; updating=false; return; }
            lastNav=n;
            if (n.ConnectionId!=null)
            {
                // a server in the tree: show its site and select it
                var c=Row(n.ConnectionId);
                if (c==null) return;
                if (!InView(view).Contains(c)) { view="site:"+c.Site; shown=new Connection[0]; }
                if (FilterBox.Text.Length>0 && !c.Matches(Fold(FilterBox.Text))) { updating=true; FilterBox.Clear(); updating=false; }
                Filter(c.Id);
                return;
            }
            view=n.Key; shown=new Connection[0];
            Filter(selected?.Id);
        }
        static string[] Fold(string text)=>Connection.Fold(text).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
        void NavDoubleClick(object sender,MouseButtonEventArgs e)
        {
            if (!(ItemsControl.ContainerFromElement(Nav,e.OriginalSource as DependencyObject) is ListBoxItem item) || !(item.DataContext is NavItem n)) return;
            if (n.ConnectionId!=null) Execute("Connect");
            else if (n.HasChildren) Toggle(n);
        }
        void ToggleNode(object sender,MouseButtonEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is NavItem n) || !n.HasChildren) return;
            e.Handled=true; Toggle(n);
        }
        void Toggle(NavItem n)
        {
            if (!Config.Expanded.Remove(n.Key)) Config.Expanded.Add(n.Key);
            var keep=(Nav.SelectedItem as NavItem)?.Key;
            // collapsing a site that holds the selected server selects the site
            if (!Config.Expanded.Contains(n.Key) && keep!=null && keep.StartsWith("conn:",StringComparison.Ordinal) && Row(keep.Substring(5))?.Site==n.Site) keep=n.Key;
            BuildNav(keep);
        }
        void NavKeys(object sender,KeyEventArgs e)
        {
            if (!(Nav.SelectedItem is NavItem n)) return;
            if ((e.Key==Key.Left && n.HasChildren && n.Expanded) || (e.Key==Key.Right && n.HasChildren && !n.Expanded)) { Toggle(n); e.Handled=true; }
            else if (e.Key==Key.Enter && n.ConnectionId!=null) { Execute("Connect"); e.Handled=true; }
            else if ((Keyboard.Modifiers&ModifierKeys.Alt)!=0 && n.Movable)
            {
                var key=e.Key==Key.System ? e.SystemKey : e.Key;
                if (key!=Key.Up && key!=Key.Down) return;
                var sites=nav.Where(x=>x.Movable).ToList();
                int at=sites.IndexOf(n), to=at+(key==Key.Up ? -1 : 1);
                if (to<0 || to>=sites.Count) return;
                MoveSite(nav.IndexOf(n),nav.IndexOf(sites[to])); SaveSiteOrder(); e.Handled=true;
            }
        }
        void MoveSite(int from,int to)
        {
            if (from==to || from<0 || to<0 || !nav[from].Movable || !nav[to].Movable) return;
            // a site moves together with its expanded children
            int Span(int i) { int n=1; while (i+n<nav.Count && nav[i+n].Depth>0) n++; return n; }
            int span=Span(from);
            var block=nav.Skip(from).Take(span).ToList();
            updating=true;
            try
            {
                int target=to>from ? to+Span(to)-span : to;
                for (int i=0;i<span;i++) nav.RemoveAt(from);
                for (int i=0;i<span;i++) nav.Insert(target+i,block[i]);
                Nav.SelectedItem=block[0];
            }
            finally { updating=false; }
        }
        void SaveSiteOrder()
        {
            var order=nav.Where(n=>n.Movable).Select(n=>n.Site).ToList();
            var next=Store.Clone(Config); next.SiteOrder=order;
            try { store.Save("settings.json",next); Config.SiteOrder=order; shown=new Connection[0]; Filter(selected?.Id); }
            catch(Exception ex) { Record(ex); }
        }

        // ---------- list ----------
        void FilterChanged(object sender,TextChangedEventArgs e)
        {
            if (!ready || updating) return;
            typing.Stop();
            // small lists filter on the keystroke itself; big ones wait for a pause in typing
            if (rows.Count<1000) Filter(selected?.Id); else typing.Start();
        }
        void Filter(string prefer)
        {
            var words=Fold(FilterBox.Text);
            var visible=Sort(InView(view).Where(c=>c.Matches(words)),view=="recent").ToArray();
            ViewTitle.Text=ViewLabel(view)+" ("+visible.Length+")";
            updating=true;
            try
            {
                bool group=Config.GroupBySite && view!="recent" && !view.StartsWith("folder:",StringComparison.Ordinal);
                bool grouped=List.ItemsSource is ListCollectionView lv && lv.GroupDescriptions.Count>0;
                // Same rows in the same shape: keep the list (and its scroll and selection) untouched.
                if (!visible.SequenceEqual(shown) || group!=grouped)
                {
                    if (group)
                    {
                        var cv=new ListCollectionView(visible);
                        cv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Connection.SiteText)));
                        if (List.GroupStyle.Count==0) List.GroupStyle.Add(new GroupStyle{ContainerStyle=(Style)FindResource("FoldGroup")});
                        List.ItemsSource=cv;
                    }
                    else List.ItemsSource=visible;
                    shown=visible;
                }
                var want=visible.FirstOrDefault(c=>c.Id==prefer) ?? (List.SelectedItem is Connection cur && visible.Contains(cur) ? cur : visible.FirstOrDefault());
                if (!ReferenceEquals(List.SelectedItem,want)) { List.SelectedItems.Clear(); List.SelectedItem=want; }
            }
            finally { updating=false; }
            EmptyList.Text=rows.Count==0 ? T("First") : T("Empty");
            EmptyList.Visibility=visible.Length==0 ? Visibility.Visible : Visibility.Collapsed;
            Status();
            SelectRow();
        }
        IEnumerable<Connection> Sort(IEnumerable<Connection> found,bool recency)
        {
            var order=Config.Sites(rows);
            IOrderedEnumerable<Connection> sorted=Config.GroupBySite && !recency ? found.OrderBy(c=>order.IndexOf(c.Site)) : null;
            Func<Connection,object> key=Config.SortBy switch {
                "Name"=>c=>c.Name, "Host"=>c=>c.HostLabel, "Site"=>c=>order.IndexOf(c.Site), "Session"=>c=>c.Live==null ? 1 : 0,
                "User"=>c=>c.Login, "Last"=>c=>c.LastUsed ?? DateTime.MinValue, "Latency"=>c=>c.Reach.LatencyMs ?? int.MaxValue,
                "Reach"=>c=>c.Reach.State==Reach.Up ? 0 : c.Reach.State==Reach.Down ? 2 : 1, "Type"=>c=>c.Protocol, _=>null };
            if (key==null) return sorted ?? found;
            IComparer<object> comparer=Comparer<object>.Create((a,b)=>a is string x && b is string y ? StringComparer.CurrentCultureIgnoreCase.Compare(x,y) : Comparer<object>.Default.Compare(a,b));
            if (sorted==null) return Config.SortDescending ? found.OrderByDescending(key,comparer) : found.OrderBy(key,comparer);
            return Config.SortDescending ? sorted.ThenByDescending(key,comparer) : sorted.ThenBy(key,comparer);
        }
        void Status()
        {
            int up=rows.Count(c=>c.Reach.State==Reach.Up), down=rows.Count(c=>c.Reach.State==Reach.Down);
            StatusTotal.Text=T("Total",rows.Count);
            StatusUp.Text=T("Available",up);
            StatusDown.Text=T("Unreachable",down);
            StatusDown.SetResourceReference(TextBlock.ForegroundProperty,down>0 ? "Down" : "Text");
            StatusDot.SetResourceReference(Shape.FillProperty,down>0 ? "Down" : up>0 ? "Up" : "Sub");
            StatusLive.Text=live.Count==0 ? "" : live.Count==1 ? T("OneSession") : T("ActiveSessions",live.Count);
            StatusLive.Visibility=StatusLiveSep.Visibility=live.Count==0 ? Visibility.Collapsed : Visibility.Visible;
            int checking=rows.Count(c=>c.Reach.State==Reach.Checking);
            LastCheck.Text=checkingAll || checking>0 ? T("Checking") : lastCheckAll>DateTime.MinValue ? T("LastCheckAll",Since(lastCheckAll)) :
                T("LastCheckAll",T("Never").ToLower(CultureInfo.CurrentCulture));
            if (List.SelectedItems.Count>1) Summary.Text=T("Selection",List.SelectedItems.Count);
        }
        void RowChanged(object sender,SelectionChangedEventArgs e) { if (ready && !updating) SelectRow(); }
        void SelectRow()
        {
            var c=List.SelectedItem as Connection;
            bool changed=c!=selected || c?.Endpoint!=selectedEndpoint;
            if (changed) SaveNotes();
            selected=c; selectedEndpoint=c?.Endpoint;
            Detail(); Status(); MarkTabs();
            if (!changed) return;
            selectedCheck?.Cancel(); selectedCheck=null; settle.Stop(); nextCheck=DateTime.MaxValue;
            if (c==null) return;
            List.ScrollIntoView(c);
            if (!isolated && Config.AutoCheck && Rdp.ValidHost(c.Host)) settle.Start();
        }

        // ---------- availability ----------
        async void CheckSelected(bool force)
        {
            var c=selected;
            if (c==null || isolated || !Rdp.ValidHost(c.Host)) return;
            selectedCheck?.Cancel();
            var token=new CancellationTokenSource(); selectedCheck=token;
            try { await reach.Check(c.Host,c.DisplayPort,force,token.Token); }
            catch(OperationCanceledException) { return; }
            catch(ObjectDisposedException) { return; }
            if (closed || selected!=c) return;
            nextCheck=DateTime.UtcNow+reach.NextInterval(c.Host,c.DisplayPort,TimeSpan.FromSeconds(Config.CheckSeconds));
        }
        // Every saved server, one TCP connection to its RDP port at a time, spaced: never a burst.
        async void CheckAll()
        {
            if (isolated || checkingAll) return;
            batchCheck?.Cancel();
            var token=new CancellationTokenSource(); batchCheck=token;
            var targets=rows.Where(c=>Rdp.ValidHost(c.Host)).Select(c=>new KeyValuePair<string,int>(c.Host,c.DisplayPort)).ToList();
            checkingAll=true; Status();
            try
            {
                foreach (var chunk in targets.Select((t,i)=>new{t,i}).GroupBy(x=>x.i/50,x=>x.t))
                    await reach.CheckMany(chunk,250,token.Token);
                lastCheckAll=DateTime.UtcNow;
            }
            catch(OperationCanceledException) { }
            catch(ObjectDisposedException) { return; }
            finally { checkingAll=false; }
            if (closed) return;
            nextCheckAll=Config.CheckAllOnStart ? DateTime.UtcNow.AddMinutes(Config.CheckAllMinutes) : DateTime.MaxValue;
            Status();
        }
        void ReachChanged(string key,ReachResult result)
        {
            if (closed) return;
            foreach (var c in rows.Where(c=>string.Equals(c.Endpoint,key,StringComparison.OrdinalIgnoreCase)))
            {
                c.Reach=result; c.Notify();
                if (c==selected) Detail();
            }
            if (result.State!=Reach.Checking) RefreshNav();
            Status();
        }
        void NetworkChanged(object sender,EventArgs e)
        {
            // network or VPN change: cached answers are stale
            Dispatcher.BeginInvoke((Action)(()=>{ if (closed) return; reach.Invalidate(); if (Config.CheckAllOnStart) nextCheckAll=DateTime.UtcNow.AddSeconds(5); else if (Config.AutoCheck) CheckSelected(true); }));
        }

        // ---------- sessions ----------
        void Tick()
        {
            if (hub.Sessions.Count>0) { hub.Poll(); SyncSessions(); }
            if (Config.AutoCheck && selected!=null && DateTime.UtcNow>=nextCheck) { nextCheck=DateTime.MaxValue; CheckSelected(true); }
            if (Config.CheckAllOnStart && DateTime.UtcNow>=nextCheckAll) { nextCheckAll=DateTime.MaxValue; CheckAll(); }
            if (!checkingAll && lastCheckAll>DateTime.MinValue) LastCheck.Text=T("LastCheckAll",Since(lastCheckAll));
            if (selected!=null && selected.Reach.CheckedUtc>DateTime.MinValue) DReachMeta.Text=ReachMeta(selected);
        }
        void Adopt()
        {
            try
            {
                foreach (var c in rows) c.DisplayPort=Rdp.Port(Rdp.Effective(c,Config));
                hub.Adopt(rows.Where(c=>Rdp.ValidHost(c.Host)).Select(c=>new KeyValuePair<string,string>(c.Id,c.Endpoint)));
            }
            catch(Exception ex) { Record(ex); }
        }
        void SyncSessions()
        {
            var now=DateTime.UtcNow;
            bool membership=false;
            foreach (var c in rows)
            {
                var s=hub.Find(c.Id);
                if (s!=c.Live) { c.Live=s; membership=true; c.Notify(); }
            }
            for (int i=live.Count-1;i>=0;i--) if (!hub.Sessions.Contains(live[i].Session)) live.RemoveAt(i);
            foreach (var s in hub.Sessions)
                if (live.All(l=>l.Session!=s)) live.Add(new LiveItem{Session=s,Name=Row(s.ConnectionId)?.Name ?? s.Name});
            foreach (var l in live)
            {
                l.Clock=l.Session.EverConnected ? Clock(l.Session.Duration(now)) : T("S"+l.Session.State);
                l.Tip=l.Session.Endpoint+" · "+T("S"+l.Session.State);
                l.Notify();
            }
            MarkTabs();
            int count=live.Count;
            SessionsText.Text=count==0 ? T("NoActive") : count==1 ? T("OneSession") : T("ActiveSessions",count);
            SessionsIcon.SetResourceReference(TextBlock.ForegroundProperty,count>0 ? "Live" : "Sub");
            TabsBar.Visibility=count>0 ? Visibility.Visible : Visibility.Collapsed;
            if (membership) { RefreshNav(); if (view=="live") { shown=new Connection[0]; Filter(selected?.Id); } }
            Status();
            if (selected!=null) Detail();
        }
        void MarkTabs()
        {
            foreach (var l in live)
            {
                bool current=selected!=null && l.Session.ConnectionId==selected.Id;
                if (current!=l.Current) { l.Current=current; l.Notify(); }
            }
        }
        void SessionEnded(Session s)
        {
            var c=Row(s.ConnectionId);
            if (c==null) return;
            // only what was observed: the window closed before a session window ever appeared
            string problem=s.EverConnected ? null : "ClosedEarly";
            if (!s.Adopted) { history.Used(c.Id,s.StartedUtc,problem); c.LastUsed=s.StartedUtc; c.LastProblem=problem; SaveHistory(); }
            c.Notify();
        }
        void Launch(Connection c,bool admin,bool full,bool another)
        {
            var existing=hub.Find(c.Id);
            if (existing!=null && !another && Config.ReuseSession && hub.Focus(existing)) return;
            if (isolated) return;
            Rdp.Launch(c,Config,store.RdpDirectory,admin,full,info=>{
                using var p=Process.Start(info);
                if (p!=null) hub.Track(c.Id,c.Name,c.Endpoint,p.Id);
            });
            var now=DateTime.UtcNow;
            history.Used(c.Id,now,null); c.LastUsed=now; c.LastProblem=null; SaveHistory();
            SyncSessions();
            if (!clock.IsEnabled) clock.Start();
        }
        LiveItem ItemOf(object sender)=>(sender as FrameworkElement)?.DataContext as LiveItem;
        void TabClick(object sender,RoutedEventArgs e)
        {
            var l=ItemOf(sender); if (l==null) return;
            var c=Row(l.Session.ConnectionId);
            if (c!=null && selected!=c)
            {
                if (!shown.Contains(c)) { updating=true; FilterBox.Clear(); view="all"; Nav.SelectedItem=nav.FirstOrDefault(n=>n.Key=="all"); updating=false; shown=new Connection[0]; }
                Filter(c.Id);
            }
            hub.Focus(l.Session);
        }
        void TabClose(object sender,RoutedEventArgs e) { e.Handled=true; var l=ItemOf(sender); if (l!=null) hub.Disconnect(l.Session); }
        void SessionsMenu(object sender,RoutedEventArgs e)
        {
            var menu=new ContextMenu{PlacementTarget=SessionsButton,Placement=PlacementMode.Bottom};
            if (live.Count==0) menu.Items.Add(new MenuItem{Header=T("NoSessions"),IsEnabled=false});
            int n=1;
            foreach (var l in live)
            {
                var item=new MenuItem{Header=l.Name+"   "+l.Clock,InputGestureText=n<=9 ? "Ctrl+"+n : ""};
                var session=l.Session; item.Click+=(s,a)=>hub.Focus(session);
                menu.Items.Add(item); n++;
            }
            if (live.Count>0) { menu.Items.Add(new Separator()); menu.Items.Add(Entry("DisconnectAll")); }
            menu.IsOpen=true;
        }

        // ---------- inspector ----------
        void Detail()
        {
            var c=selected;
            Details.Visibility=c==null ? Visibility.Collapsed : Visibility.Visible;
            NoSel.Visibility=c==null ? Visibility.Visible : Visibility.Collapsed;
            if (c==null) return;
            DKind.Text=c.KindIcon; DKind.ToolTip=c.KindText;
            DName.Text=c.Name; DName.ToolTip=c.Name;
            BtnFav.Content=c.StarIcon;
            BtnFav.SetResourceReference(ForegroundProperty,c.Favorite ? "Accent" : "Sub");
            BtnFav.ToolTip=c.StarTip; AutomationSet(BtnFav,c.StarTip);
            Properties(c);
            // availability: what the TCP check of the RDP port showed, and when
            var r=c.Reach;
            DDot.DataContext=c;
            DReach.Text=c.ReachText;
            DReach.SetResourceReference(TextBlock.ForegroundProperty,r.State==Reach.Up ? "Up" : r.State==Reach.Down ? "Down" : r.State==Reach.Checking ? "Checking" : "Sub");
            DReachMeta.Text=ReachMeta(c);
            DReachMeta.Visibility=DReachMeta.Text=="" ? Visibility.Collapsed : Visibility.Visible;
            // session: from the mstsc windows actually observed
            var s=c.Live;
            DSession.Text=s==null ? T("SessionNone") : T("S"+s.State);
            DSession.SetResourceReference(TextBlock.ForegroundProperty,s==null ? "Sub" : s.State==SessionState.Interrupted ? "Down" : s.State==SessionState.Authenticating ? "Checking" : "Live");
            DSessionIcon.SetResourceReference(TextBlock.ForegroundProperty,s==null ? "Sub" : "Live");
            DSessionMeta.Text=s==null ? "" : s.EverConnected ? T("Duration")+": "+Clock(s.Duration(DateTime.UtcNow)) : "";
            DSessionMeta.Visibility=DSessionMeta.Text=="" ? Visibility.Collapsed : Visibility.Visible;
            // diagnostics: only causes with evidence
            string diag=null,note=null;
            if (s?.State==SessionState.Interrupted) diag=T("SInterruptedNote");
            else if (r.State==Reach.Down) { diag=FailureText(r.Failure,c.DisplayPort); note=T("ReachNote"); }
            else if (s==null && c.LastProblem=="ClosedEarly") diag=T("ClosedEarly");
            Diag.Visibility=diag==null ? Visibility.Collapsed : Visibility.Visible;
            DDiag.Text=diag ?? ""; DDiagNote.Text=note ?? ""; DDiagNote.Visibility=note==null ? Visibility.Collapsed : Visibility.Visible;
            // actions follow the session state
            bool open=s!=null;
            BtnOpen.Tag=open ? (s.State==SessionState.Interrupted ? "Retry" : "Focus") : "Connect";
            BtnOpenText.Text=T(open ? (s.State==SessionState.Interrupted ? "BRetry" : "BShow") : "BOpen");
            AutomationSet(BtnOpen,T(open ? (s.State==SessionState.Interrupted ? "Retry" : "GoSession") : "OpenSession"));
            BtnOpen.ToolTip=T(open ? (s.State==SessionState.Interrupted ? "Retry" : "GoSession") : "OpenSession");
            bool connecting=s?.State==SessionState.Connecting || s?.State==SessionState.Authenticating;
            BtnDisconnect.Tag=connecting ? "CancelConnect" : "Disconnect";
            BtnDisconnect.Content=T(connecting ? "BCancel" : "BDisconnect");
            BtnDisconnect.ToolTip=T(connecting ? "CancelConnect" : "Disconnect");
            BtnDisconnect.IsEnabled=open;
            ActionNote.Text=s?.State==SessionState.Connected ? T("DisconnectNote") : "";
            ActionNote.Visibility=ActionNote.Text=="" ? Visibility.Collapsed : Visibility.Visible;
            SessionTab(c); MonitorTab(c);
            if (!NotesBox.IsKeyboardFocused) { NotesBox.Text=c.Descr ?? ""; NotesBox.Tag=c.Id; NotesState.Text=T("NotesHint"); }
        }
        string ReachMeta(Connection c)
        {
            var r=c.Reach; var parts=new List<string>();
            if (r.CheckedUtc>DateTime.MinValue) parts.Add(T("Checked",Since(r.CheckedUtc)));
            if (r.State==Reach.Up && r.LatencyMs!=null) parts.Add(r.LatencyMs+" ms");
            return string.Join(" · ",parts);
        }
        static void AutomationSet(FrameworkElement e,string name)=>System.Windows.Automation.AutomationProperties.SetName(e,name);
        static string FailureText(ReachFailure f,int port)=>f switch {
            ReachFailure.Dns=>T("FDns"), ReachFailure.Timeout=>T("FTimeout",2), ReachFailure.Refused=>T("FRefused",port),
            ReachFailure.Unreachable=>T("FUnreachable"), _=>T("FOther") };
        void Rows(Panel target,IEnumerable<(string Label,string Value)> pairs)
        {
            var grid=target as Grid ?? new Grid();
            grid.Children.Clear(); grid.RowDefinitions.Clear();
            if (grid.ColumnDefinitions.Count==0) { grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,MinWidth=130}); grid.ColumnDefinitions.Add(new ColumnDefinition()); }
            foreach (var (label,value) in pairs)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                int row=grid.RowDefinitions.Count;
                grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
                var l=new TextBlock{Text=label,Style=(Style)FindResource("Prop"),FontSize=13,Margin=new Thickness(0,0,16,11)};
                var v=new TextBlock{Text=value,TextWrapping=TextWrapping.Wrap,FontSize=13,Margin=new Thickness(0,0,0,11)};
                v.SetResourceReference(TextBlock.ForegroundProperty,"Text");
                Grid.SetRow(l,row); Grid.SetRow(v,row); Grid.SetColumn(v,1);
                grid.Children.Add(l); grid.Children.Add(v);
            }
            if (!(target is Grid)) target.Children.Add(grid);
        }
        void Properties(Connection c)
        {
            Rows(Props,new[]{
                (T("Site"),c.SiteText),(T("Address"),c.Host),(T("Protocol"),T("ProtocolPort",c.DisplayPort)),
                (T("User"),string.IsNullOrEmpty(c.Login) ? "—" : c.Login),(T("Cred"),c.CredentialUser ?? T("NoCred")),
                (T("Kind"),string.IsNullOrEmpty(c.Kind) ? null : c.KindText),(T("Tags"),string.Join(", ",c.TagList)),(T("Description"),c.Descr)});
        }
        void SessionTab(Connection c)
        {
            SessionPage.Children.Clear();
            var s=c.Live;
            var pairs=new List<(string,string)>{(T("Session"),s==null ? T("SessionNone") : T("S"+s.State))};
            if (s!=null)
            {
                pairs.Add((T("Started"),When(s.StartedUtc)));
                if (s.EverConnected) pairs.Add((T("Duration"),Clock(s.Duration(DateTime.UtcNow))));
                pairs.Add((T("ProcessId"),s.ProcessId.ToString(CultureInfo.InvariantCulture)));
            }
            pairs.Add((T("LastUsed"),c.LastUsed==null ? T("Never") : When(c.LastUsed.Value)));
            Rows(SessionPage,pairs);
            void Note(string text) { if (text==null) return; var t=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,4,0,0)}; t.SetResourceReference(TextBlock.ForegroundProperty,"Sub"); SessionPage.Children.Add(t); }
            if (s?.Adopted==true) Note(T("AdoptedNote"));
            if (s?.State==SessionState.Interrupted) Note(T("SInterruptedNote"));
            if (s==null && c.LastProblem=="ClosedEarly") Note(T("ClosedEarly"));
            Note(T("DisconnectNote"));
        }
        void MonitorTab(Connection c)
        {
            MonitorPage.Children.Clear();
            var r=c.Reach;
            Rows(MonitorPage,new[]{
                (T("StateCol"),c.ReachText),(T("LastCheck"),r.CheckedUtc>DateTime.MinValue ? When(r.CheckedUtc)+" ("+Since(r.CheckedUtc)+")" : T("Never")),
                (T("Latency"),r.State==Reach.Up && r.LatencyMs!=null ? r.LatencyMs+" ms" : null),
                (T("Problem"),r.State==Reach.Down ? FailureText(r.Failure,c.DisplayPort) : null),
                (T("Failures"),r.Failures>0 ? r.Failures.ToString(CultureInfo.CurrentCulture) : null),(T("PortChecked"),"TCP "+c.DisplayPort)});
            var note=new TextBlock{Text=T("ReachNote"),TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,4,0,10)};
            note.SetResourceReference(TextBlock.ForegroundProperty,"Sub");
            MonitorPage.Children.Add(note);
            var b=new Button{Style=(Style)FindResource("Outline"),Tag="Check",Padding=new Thickness(14,0,14,0),HorizontalAlignment=HorizontalAlignment.Left,Content=T("BCheck"),ToolTip=T("Check")};
            b.Click+=ActionClick; MonitorPage.Children.Add(b);
        }
        void NotesSave(object sender,RoutedEventArgs e)=>SaveNotes();
        void SaveNotes()
        {
            if (!(NotesBox.Tag is string id)) return;
            var c=Row(id);
            if (c==null || (c.Descr ?? "")==NotesBox.Text) return;
            try
            {
                var updated=Store.Clone(rows);
                updated.First(x=>x.Id==id).Descr=NotesBox.Text;
                store.Save("connections.json",updated);
                c.Descr=NotesBox.Text; c.Index();
                NotesState.Text=T("NotesSaved");
            }
            catch(Exception ex) { Record(ex); ReportError(); }
        }

        // ---------- commands ----------
        void Commit(List<Connection> updated,string keep,string id)
        {
            store.Save("connections.json",updated);
            rows=updated; ApplyHistory(); SyncLive(); Rebuild(keep,id);
            Flash(T("Done")); _=RefreshCredentials();
        }
        void SyncLive() { foreach (var c in rows) c.Live=hub.Find(c.Id); }
        void SelectUnderMouse(object sender,MouseButtonEventArgs e)
        {
            var list=(ListBox)sender;
            if (ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) is ListBoxItem item && !item.IsSelected)
            {
                if (list==List) List.SelectedItems.Clear();
                item.IsSelected=true;
            }
        }
        void RowDoubleClick(object sender,MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(List,e.OriginalSource as DependencyObject) is ListViewItem) Execute("Connect");
        }
        void StarClick(object sender,RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is Connection c)) return;
            e.Handled=true;
            var updated=Store.Clone(rows);
            var target=updated.First(x=>x.Id==c.Id); target.Favorite=!target.Favorite;
            try { Commit(updated,view,c.Id); } catch(Exception ex) { Record(ex); ReportError(); }
        }
        void ActionClick(object sender,RoutedEventArgs e)=>Execute((string)((FrameworkElement)sender).Tag);
        List<Connection> Selection()=>List.SelectedItems.Cast<Connection>().ToList();
        MenuItem Entry(string key,string action=null,string gesture=null,bool enabled=true)
        {
            var item=new MenuItem{Header=T(key),Tag=action ?? key,InputGestureText=gesture ?? "",IsEnabled=enabled};
            item.Click+=ActionClick; return item;
        }
        bool BuildRowMenu(ContextMenu menu)
        {
            if (selected==null) return false;
            menu.Items.Clear();
            var s=selected.Live; int many=Math.Max(1,List.SelectedItems.Count);
            if (s!=null && many==1) { menu.Items.Add(Entry("GoSession","Focus","Enter")); menu.Items.Add(Entry("Disconnect")); menu.Items.Add(Entry("Another")); }
            else menu.Items.Add(Entry("OpenSession","Connect","Enter"));
            menu.Items.Add(Entry("Admin",null,"Ctrl+Enter"));
            menu.Items.Add(Entry("Full",null,"Shift+Enter"));
            menu.Items.Add(new Separator());
            var sel=Selection(); if (sel.Count==0) sel.Add(selected);
            menu.Items.Add(Entry(sel.All(c=>c.Favorite) ? "RemoveFavorite" : "AddFavorite","Favorite","Ctrl+Shift+F"));
            var kinds=new MenuItem{Header=T("SetKind")};
            foreach (var k in Connection.Kinds)
            {
                var item=new MenuItem{Header=T("K"+k),IsCheckable=true,IsChecked=sel.All(c=>(c.Kind ?? "")==k),Tag="Kind:"+k};
                item.Click+=ActionClick; kinds.Items.Add(item);
            }
            menu.Items.Add(kinds);
            menu.Items.Add(Entry("Check"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Entry("Cred",null,null,many==1));
            menu.Items.Add(Entry("RemoveCred",null,null,many==1 && selected.CredentialUser!=null));
            menu.Items.Add(Entry("Copy",null,"Ctrl+C"));
            menu.Items.Add(Entry("Edit",null,"F2",many==1));
            menu.Items.Add(Entry("Duplicate",null,"Ctrl+D",many==1));
            menu.Items.Add(Entry("Delete",null,"Supr"));
            return true;
        }
        void RowMenuButton(object sender,RoutedEventArgs e)
        {
            var menu=new ContextMenu{PlacementTarget=BtnRowMenu,Placement=PlacementMode.Bottom};
            if (BuildRowMenu(menu)) menu.IsOpen=true;
        }
        bool BuildNavMenu()
        {
            if (!(Nav.SelectedItem is NavItem n)) return false;
            var menu=Nav.ContextMenu; menu.Items.Clear();
            if (n.ConnectionId!=null) return BuildRowMenu(menu);
            if (!n.Movable) return false;
            menu.Items.Add(Entry("Rename")); menu.Items.Add(Entry("SiteOptions")); menu.Items.Add(Entry("Bulk"));
            return true;
        }
        void Popup(FrameworkElement target,params object[] items)
        {
            var menu=new ContextMenu{PlacementTarget=target,Placement=PlacementMode.Bottom};
            foreach (var i in items) menu.Items.Add(i);
            menu.IsOpen=true;
        }
        void NewMenu(object sender,RoutedEventArgs e)=>Popup(BtnNewMore,Entry("New",null,"Ctrl+N"),Entry("Duplicate",null,"Ctrl+D",selected!=null),Entry("Import"));
        void OpenMenu(object sender,RoutedEventArgs e)=>Popup(BtnOpenMore,Entry("Admin",null,"Ctrl+Enter"),Entry("Full",null,"Shift+Enter"),Entry("Another"),new Separator(),Entry("Cred"),Entry("Copy",null,"Ctrl+C"));
        void MoreMenu(object sender,RoutedEventArgs e)=>Popup(BtnMore,Entry("CheckAll",null,"F5"),Entry("Bulk"),Entry("Import"),new Separator(),Entry("PaletteMenu","Palette","Ctrl+Shift+P"),Entry("Settings",null,"Ctrl+,"));
        void ViewButton(object sender,RoutedEventArgs e)
        {
            var columns=new MenuItem{Header=T("Columns")};
            foreach (var item in ColumnItems()) columns.Items.Add(item);
            var group=new MenuItem{Header=T("GroupBySite"),IsCheckable=true,IsChecked=Config.GroupBySite,Tag="Group"}; group.Click+=ActionClick;
            var density=new MenuItem{Header=T("Density")};
            foreach (var d in new[]{"compact","comfortable"}) { var i=new MenuItem{Header=T(d),IsCheckable=true,IsChecked=Config.Density==d,Tag="Density:"+d}; i.Click+=ActionClick; density.Items.Add(i); }
            Popup(BtnView,columns,group,density);
        }
        void ViewMenuOpened(object sender,RoutedEventArgs e)
        {
            MSidebar.IsChecked=!Config.SidebarCollapsed; MInspector.IsChecked=!Config.InspectorHidden; MGroup.IsChecked=Config.GroupBySite;
            MCompact.IsChecked=Config.Density=="compact"; MComfortable.IsChecked=Config.Density=="comfortable";
            MColumns.Items.Clear(); foreach (var item in ColumnItems()) MColumns.Items.Add(item);
        }
        void ConnectionsMenuOpened(object sender,RoutedEventArgs e)
        {
            bool one=selected!=null, open=selected?.Live!=null;
            foreach (var m in new[]{MConnect,MAdmin,MFull,MEdit,MDuplicate,MFavorite,MDelete,MCred}) m.IsEnabled=one;
            MConnect.Header=T(open ? "GoSession" : "OpenSession"); MConnect.Tag=open ? "Focus" : "Connect";
            MAnother.IsEnabled=open; MDisconnect.IsEnabled=open; MDisconnectAll.IsEnabled=live.Count>0;
        }
        void Keys(object sender,KeyEventArgs e)
        {
            if (Palette.Visibility==Visibility.Visible) return;
            bool ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0;
            bool shift=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
            bool inText=FilterBox.IsKeyboardFocused;
            if (NotesBox.IsKeyboardFocused && e.Key!=Key.Escape && !(ctrl && (e.Key==Key.K || e.Key==Key.F))) return;
            string action=null;
            if (ctrl && shift && e.Key==Key.P) action="Palette";
            else if (ctrl && e.Key==Key.Tab) action="Switch";
            else if (ctrl && shift && e.Key==Key.F) action="Favorite";
            else if (ctrl && (e.Key==Key.K || e.Key==Key.F)) { FilterBox.Focus(); FilterBox.SelectAll(); }
            else if (e.Key==Key.Escape)
            {
                if (FilterBox.Text.Length>0) FilterBox.Clear();
                List.Focus();
            }
            else if (inText && (e.Key==Key.Up || e.Key==Key.Down))
                List.SelectedIndex=Math.Min(List.Items.Count-1,Math.Max(0,List.SelectedIndex+(e.Key==Key.Up ? -1 : 1)));
            else if (e.Key==Key.Enter && (inText || List.IsKeyboardFocusWithin || e.OriginalSource==this)) action=ctrl ? "Admin" : shift ? "Full" : "Connect";
            else if (e.Key==Key.F5) action="CheckAll";
            else if (e.Key==Key.F2) action="Edit";
            else if (ctrl && e.Key==Key.I) action="Inspector";
            else if (ctrl && e.Key==Key.B) action="Sidebar";
            else if (ctrl && e.Key==Key.OemComma) action="Settings";
            else if (ctrl && e.Key==Key.N) action="New";
            else if (ctrl && e.Key==Key.D) action="Duplicate";
            else if (ctrl && e.Key==Key.C && (!inText || FilterBox.SelectionLength==0)) action="Copy";
            else if (ctrl && e.Key>=Key.D1 && e.Key<=Key.D9) { int i=e.Key-Key.D1; if (i<live.Count) hub.Focus(live[i].Session); }
            else if (e.Key==Key.Delete && !inText) action="Delete";
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
                    case "Settings": SettingsCenter(null); return;
                    case "Shortcuts": SettingsCenter("Keyboard"); return;
                    case "About": SettingsCenter("About"); return;
                    case "OpenFolder": if (!isolated) Process.Start(new ProcessStartInfo(store.Home){UseShellExecute=true}); return;
                    case "New": Editor(null,false); return;
                    case "Import": ImportDialog(); return;
                    case "Bulk": CredentialDialog(true); return;
                    case "Rename": if (CurrentSite!=null) RenameSite(); return;
                    case "SiteOptions": if (CurrentSite!=null) SiteOptions(); return;
                    case "Palette": OpenPalette(false); return;
                    case "Switch": OpenPalette(true); return;
                    case "LiveView": Nav.SelectedItem=nav.FirstOrDefault(n=>n.Key=="live"); return;
                    case "CheckAll": case "CheckVisible": batchCheck?.Cancel(); checkingAll=false; CheckAll(); return;
                    case "Inspector": Config.InspectorHidden=!Config.InspectorHidden; Layout(); return;
                    case "Sidebar": SaveLayout(); Config.SidebarCollapsed=!Config.SidebarCollapsed; Layout(); return;
                    case "Group": Config.GroupBySite=!Config.GroupBySite; shown=new Connection[0]; Filter(selected?.Id); return;
                    case "Density:compact": case "Density:comfortable": Config.Density=action.Substring(8); Layout(); return;
                    case "DisconnectAll":
                        if (live.Count>0 && (!Config.ConfirmDisconnectAll || Confirm("DisconnectAll","DisconnectAllConfirm",live.Count.ToString(CultureInfo.CurrentCulture)))) { hub.DisconnectAll(); hub.Poll(); SyncSessions(); }
                        return;
                }
                if (selected==null) return;
                var many=Selection();
                if (many.Count==0) many.Add(selected);
                if (action.StartsWith("Kind:",StringComparison.Ordinal))
                {
                    var kind=action.Substring(5);
                    var ids=new HashSet<string>(many.Select(c=>c.Id));
                    var updated=Store.Clone(rows);
                    foreach (var c in updated.Where(c=>ids.Contains(c.Id))) c.Kind=kind=="" ? null : kind;
                    Commit(updated,view,selected.Id); return;
                }
                switch(action)
                {
                    case "Connect": case "Admin": case "Full": case "Retry": case "Another":
                        bool another=action=="Another" || action=="Retry";
                        if (action=="Retry" && selected.Live!=null) hub.Disconnect(selected.Live);
                        var targets=many.Count>1 && action!="Retry" && action!="Another" ? many : new List<Connection>{selected};
                        if (targets.Count>3 && !Confirm("Connect","Selection",targets.Count.ToString(CultureInfo.CurrentCulture))) return;
                        foreach (var c in targets.Take(10)) Launch(c,action=="Admin",action=="Full",another);
                        break;
                    case "Focus": hub.Focus(selected.Live); break;
                    case "Disconnect": if (selected.Live!=null) hub.Disconnect(selected.Live); break;
                    case "CancelConnect":
                        if (selected.Live!=null && !hub.Disconnect(selected.Live)) { using var p=Process.GetProcessById(selected.Live.ProcessId); p.Kill(); }
                        break;
                    case "Check": foreach (var c in many) _=reach.Check(c.Host,c.DisplayPort,true,CancellationToken.None).ContinueWith(t=>{ var ignored=t.Exception; },TaskScheduler.Default); break;
                    case "Favorite":
                    {
                        bool on=!many.All(c=>c.Favorite);
                        var ids=new HashSet<string>(many.Select(c=>c.Id));
                        var updated=Store.Clone(rows);
                        foreach (var c in updated.Where(c=>ids.Contains(c.Id))) c.Favorite=on;
                        Commit(updated,view,selected.Id); break;
                    }
                    case "Copy": Clipboard.SetText(string.Join(Environment.NewLine,many.Select(c=>c.Host))); break;
                    case "Edit": Editor(selected,false); break;
                    case "Duplicate": Editor(selected,true); break;
                    case "Delete":
                        if (Confirm("Delete","DeleteConfirm",many.Count==1 ? selected.Name : T("Selection",many.Count)))
                        { var gone=new HashSet<Connection>(many); Commit(rows.Where(c=>!gone.Contains(c)).ToList(),view,null); }
                        break;
                    case "Cred": CredentialDialog(false); break;
                    case "RemoveCred":
                        if (!isolated && Confirm("RemoveCred","RemoveConfirm",selected.Host)) { Credentials.Remove(selected.Host); _=RefreshCredentials(); }
                        break;
                }
            }
            catch(Exception ex) { Record(ex); ReportError(); }
        }
        string SiteLabel(string site)=>string.IsNullOrEmpty(site) ? T("NoSite") : site;
        string CurrentSite
        {
            get
            {
                var n=Nav.SelectedItem as NavItem;
                if (n?.Movable==true) return n.Site;
                return view.StartsWith("site:",StringComparison.Ordinal) ? view.Substring(5) : n?.Site;
            }
        }
        bool Confirm(string title,string message,string value)
        {
            var dialog=new Dialog(this,title);
            dialog.Note(T(message,value)); return dialog.Run();
        }
    }
}
