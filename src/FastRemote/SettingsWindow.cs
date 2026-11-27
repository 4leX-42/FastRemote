using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using static FastRemote.Words;

namespace FastRemote
{
    // Settings centre: categories on the left, one page on the right, live preview, Save or Cancel.
    sealed class SettingsWindow : Window
    {
        public static readonly string[] Pages={"General","Appearance","ConnectionsPage","Monitoring","SessionsPage","Keyboard","Security","Data","About"};
        static readonly string[] PageIcons={"\uE80F","\uE790","\uE7F4","\uE9D9","\uEBC6","\uE765","\uE72E","\uE8B7","\uE946"};
        readonly MainWindow owner;
        readonly Settings next;
        readonly List<string> order;
        readonly Func<string,string> siteLabel;
        readonly ListBox menu=new ListBox();
        readonly ContentControl page=new ContentControl();
        readonly TextBlock error=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};
        readonly Border frame;
        public Action Import,Bulk,OpenFolder;
        public string Home,CredentialText;
        public bool Saved { get; private set; }

        public SettingsWindow(MainWindow owner,Settings current,List<string> sites,Func<string,string> label,string start)
        {
            this.owner=owner; next=Store.Clone(current); order=sites.ToList(); siteLabel=label;
            if (owner.IsVisible) Owner=owner;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanResize; ShowInTaskbar=false;
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            SetResourceReference(TitleProperty,"LSettings");
            WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=44*current.Scale,ResizeBorderThickness=new Thickness(5),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
            Width=Math.Max(600,Math.Min(owner.ActualWidth-40,860*current.Scale)); Height=Math.Max(420,Math.Min(owner.ActualHeight-40,600*current.Scale));
            MinWidth=600; MinHeight=420;
            if (Program.Offscreen) { WindowStartupLocation=WindowStartupLocation.Manual; Left=-24000; Top=-24000; ShowActivated=false; }

            var root=new Grid();
            root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(44)});
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});
            root.ColumnDefinitions.Add(new ColumnDefinition());

            var title=new TextBlock{FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(18,0,0,0)};
            title.SetResourceReference(TextBlock.TextProperty,"LSettings");
            var head=new Border{Child=title}; head.SetResourceReference(Border.BackgroundProperty,"Surface");
            Grid.SetColumnSpan(head,2); root.Children.Add(head);
            var close=new Button{Content="\uE8BB",Style=(Style)owner.FindResource("ChromeClose"),HorizontalAlignment=HorizontalAlignment.Right,IsCancel=false};
            close.Click+=(s,e)=>Close(); AutomationProperties.SetName(close,T("Close"));
            Grid.SetColumn(close,1); root.Children.Add(close);

            menu.ItemContainerStyle=(Style)owner.FindResource("NavItem");
            menu.ItemsSource=Pages.Select((p,i)=>new NavItem{Key=p,Label=T(p),Icon=PageIcons[i]}).ToList();
            menu.ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><DockPanel Margin='12,0,0,0'>"+
                "<TextBlock Text='{Binding Icon}' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='13' Width='18' Margin='0,0,12,0' VerticalAlignment='Center'/>"+
                "<TextBlock Text='{Binding Label}' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/></DockPanel></DataTemplate>");
            menu.Margin=new Thickness(0,10,0,0);
            AutomationProperties.SetAutomationId(menu,"SettingsPages");
            var side=new Border{Child=menu}; side.SetResourceReference(Border.BackgroundProperty,"Surface");
            Grid.SetRow(side,1); Grid.SetRowSpan(side,2); root.Children.Add(side);

            var scroll=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(32,22,32,22)};
            Grid.SetRow(scroll,1); Grid.SetColumn(scroll,1); root.Children.Add(scroll);

            var foot=new DockPanel{Margin=new Thickness(32,12,20,16),LastChildFill=true};
            var buttons=new StackPanel{Orientation=Orientation.Horizontal};
            DockPanel.SetDock(buttons,Dock.Right);
            var cancel=new Button{IsCancel=true}; cancel.SetResourceReference(ContentProperty,"LBCancel"); AutomationProperties.SetAutomationId(cancel,"DlgCancel");
            var ok=new Button{IsDefault=true,Style=(Style)owner.FindResource("BtnGold"),MinWidth=96,Height=32}; ok.SetResourceReference(ContentProperty,"LBSave"); AutomationProperties.SetAutomationId(ok,"DlgOk");
            ok.Click+=(s,e)=>Accept();
            buttons.Children.Add(cancel); buttons.Children.Add(ok);
            foot.Children.Add(buttons);
            error.SetResourceReference(TextBlock.ForegroundProperty,"Down");
            foot.Children.Add(error);
            var footBorder=new Border{Child=foot,BorderThickness=new Thickness(0,1,0,0)};
            footBorder.SetResourceReference(Border.BorderBrushProperty,"Line");
            Grid.SetRow(footBorder,2); Grid.SetColumn(footBorder,1); root.Children.Add(footBorder);

            frame=new Border{Child=root,BorderThickness=new Thickness(1)};
            frame.SetResourceReference(Border.BorderBrushProperty,"Line");
            frame.SetResourceReference(Border.BackgroundProperty,"Base");
            Content=frame;
            Appearance.Scale(this,frame,current.Scale);
            menu.SelectionChanged+=(s,e)=>ShowPage();
            menu.SelectedIndex=Math.Max(0,Array.IndexOf(Pages,start ?? "General"));
            PreviewKeyDown+=(s,e)=>{ if (e.Key==Key.Escape) { Close(); e.Handled=true; } };
            Closed+=(s,e)=>{ if (!Saved) Appearance.Apply(owner.Config); };
        }
        public Settings Result => next;
        public List<string> Order => order;

        void Accept()
        {
            if (!Appearance.IsAccent(next.Accent)) { error.Text=T("Invalid",T("Accent")); return; }
            next.SiteOrder=order.ToList();
            Saved=true; DialogResult=true;
        }
        void Preview()
        {
            if (!Appearance.IsAccent(next.Accent)) return;
            Appearance.Apply(next);
            Appearance.Scale(this,frame,next.Scale);
        }

        // ---------- building blocks ----------
        StackPanel body;
        TextBlock Text(string text,string brush="Text",double size=13,bool wrap=true)
        {
            var t=new TextBlock{Text=text,FontSize=size,TextWrapping=wrap ? TextWrapping.Wrap : TextWrapping.NoWrap};
            t.SetResourceReference(TextBlock.ForegroundProperty,brush);
            return t;
        }
        void Heading(string key)
        {
            var h=Text(T(key),"Text",20); h.FontWeight=FontWeights.SemiBold; h.Margin=new Thickness(0,0,0,4);
            body.Children.Add(h);
        }
        void Group(string key)
        {
            var g=Text(T(key).ToUpper(CultureInfo.CurrentCulture),"Sub",11); g.FontWeight=FontWeights.SemiBold; g.Margin=new Thickness(0,22,0,8);
            body.Children.Add(g);
        }
        void Note(string text) { var n=Text(text,"Sub",12); n.Margin=new Thickness(0,4,0,0); body.Children.Add(n); }
        CheckBox Switch(string key,bool value,Action<bool> set,string id)
        {
            var box=new CheckBox{Content=T(key),IsChecked=value,Style=(Style)owner.FindResource("Switch"),MaxWidth=560,HorizontalAlignment=HorizontalAlignment.Left};
            AutomationProperties.SetAutomationId(box,id);
            box.Click+=(s,e)=>set(box.IsChecked==true);
            body.Children.Add(box); return box;
        }
        void Segments(string group,string[] values,string current,Action<string> set,string id)
        {
            var row=new StackPanel{Orientation=Orientation.Horizontal};
            AutomationProperties.SetAutomationId(row,id);
            foreach (var v in values)
            {
                var r=new RadioButton{Content=T(v),GroupName=id,IsChecked=v==current,Style=(Style)owner.FindResource("Segment"),Tag=v};
                AutomationProperties.SetAutomationId(r,id+"_"+v);
                r.Checked+=(s,e)=>set((string)((RadioButton)s).Tag);
                row.Children.Add(r);
            }
            body.Children.Add(row);
        }
        TextBox Number(string key,int value,int min,int max,Action<int> set,string id)
        {
            var row=new DockPanel{Margin=new Thickness(0,10,0,0),MaxWidth=560,HorizontalAlignment=HorizontalAlignment.Left,LastChildFill=true};
            var box=new TextBox{Text=value.ToString(CultureInfo.InvariantCulture),Width=90,TextAlignment=TextAlignment.Right};
            AutomationProperties.SetAutomationId(box,id); AutomationProperties.SetName(box,T(key));
            DockPanel.SetDock(box,Dock.Right);
            var label=Text(T(key)); label.VerticalAlignment=VerticalAlignment.Center; label.Margin=new Thickness(0,0,16,0);
            row.Children.Add(box); row.Children.Add(label);
            box.TextChanged+=(s,e)=>{
                bool good=int.TryParse(box.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var n) && n>=min && n<=max;
                box.SetResourceReference(Control.BorderBrushProperty,good ? "Line" : "Down");
                error.Text=good ? "" : T("Invalid",T(key))+" ("+min+"–"+max+")";
                if (good) set(n);
            };
            body.Children.Add(row); return box;
        }
        Button Action(string key,Action run,string id)
        {
            var b=new Button{HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,8,0)};
            b.SetResourceReference(ContentProperty,"L"+key); AutomationProperties.SetAutomationId(b,id);
            b.Click+=(s,e)=>run(); body.Children.Add(b); return b;
        }
        void RestoreButton(Action reset)
        {
            var b=new Button{HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,28,0,0)};
            b.SetResourceReference(ContentProperty,"LRestore"); AutomationProperties.SetAutomationId(b,"Restore");
            b.Click+=(s,e)=>{ reset(); Preview(); ShowPage(); };
            body.Children.Add(b);
        }

        void ShowPage()
        {
            body=new StackPanel{MaxWidth=640,HorizontalAlignment=HorizontalAlignment.Left};
            var key=(menu.SelectedItem as NavItem)?.Key ?? "General";
            Heading(key);
            var d=new Settings();
            switch (key)
            {
                case "General":
                    Group("Language");
                    Segments("Language",new[]{"auto","es","en"},next.Language ?? "auto",v=>{ next.Language=v; Preview(); },"Language");
                    RestoreButton(()=>next.Language=d.Language);
                    break;
                case "Appearance":
                    Group("Theme"); Themes();
                    Group("Accent"); Accents();
                    Group("Scale"); ScaleSlider();
                    Group("Density");
                    Segments("Density",new[]{"compact","comfortable"},next.Density,v=>next.Density=v,"Density");
                    RestoreButton(()=>{ next.Theme=d.Theme; next.Accent=""; next.Scale=d.Scale; next.Density="compact"; });
                    break;
                case "ConnectionsPage":
                    Group("Order"); SiteOrder();
                    Group("Actions");
                    var row=new WrapPanel();
                    body.Children.Add(row);
                    foreach (var (k,run,id) in new (string,Action,string)[]{("Import",Import,"ImportBtn"),("Bulk",Bulk,"BulkBtn")})
                    {
                        var b=new Button{Margin=new Thickness(0,4,8,0)}; b.SetResourceReference(ContentProperty,"L"+k); AutomationProperties.SetAutomationId(b,id);
                        b.Click+=(s,e)=>run?.Invoke(); row.Children.Add(b);
                    }
                    break;
                case "Monitoring":
                    Switch("CheckAllOnStart",next.CheckAllOnStart,v=>next.CheckAllOnStart=v,"CheckAllOnStart");
                    Number("CheckAllMinutes",next.CheckAllMinutes,1,240,v=>next.CheckAllMinutes=v,"CheckAllMinutes");
                    Note(T("CheckAllNote"));
                    Switch("AutoCheck",next.AutoCheck,v=>next.AutoCheck=v,"AutoCheck");
                    Number("CheckEvery",next.CheckSeconds,15,3600,v=>next.CheckSeconds=v,"CheckSeconds");
                    Number("Timeout",next.TimeoutMs,500,10000,v=>next.TimeoutMs=v,"TimeoutMs");
                    Note(T("ReachNote"));
                    RestoreButton(()=>{ next.CheckAllOnStart=true; next.CheckAllMinutes=5; next.AutoCheck=true; next.CheckSeconds=60; next.TimeoutMs=2000; });
                    break;
                case "SessionsPage":
                    Switch("ReuseSession",next.ReuseSession,v=>next.ReuseSession=v,"ReuseSession");
                    Switch("AdoptSessions",next.AdoptSessions,v=>next.AdoptSessions=v,"AdoptSessions");
                    Switch("ConfirmDisconnectAll",next.ConfirmDisconnectAll,v=>next.ConfirmDisconnectAll=v,"ConfirmDisconnectAll");
                    Note(T("DisconnectNote"));
                    RestoreButton(()=>{ next.ReuseSession=true; next.AdoptSessions=true; next.ConfirmDisconnectAll=true; });
                    break;
                case "Keyboard": Shortcuts(); break;
                case "Security":
                    Note(T("SecurityNote"));
                    if (CredentialText!=null) { var c=Text(CredentialText); c.Margin=new Thickness(0,14,0,0); body.Children.Add(c); }
                    Action("Bulk",()=>Bulk?.Invoke(),"BulkBtn2");
                    break;
                case "Data":
                    Note(T("DataNote"));
                    var path=Text(Home ?? "","Text",12.5); path.FontFamily=(FontFamily)owner.FindResource("Mono"); path.Margin=new Thickness(0,12,0,0);
                    body.Children.Add(path);
                    Action("Folder",()=>OpenFolder?.Invoke(),"OpenFolder");
                    break;
                case "About":
                    var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
                    brand.Children.Add(new Image{Source=(ImageSource)owner.FindResource("LogoMark"),Width=40,Height=40});
                    var name=new StackPanel{Margin=new Thickness(14,0,0,0),VerticalAlignment=VerticalAlignment.Center};
                    var n=Text("FastRemote","Text",16); n.FontWeight=FontWeights.SemiBold; name.Children.Add(n);
                    name.Children.Add(Text(T("Version",Assembly.GetExecutingAssembly().GetName().Version.ToString(3)),"Sub",12));
                    brand.Children.Add(name); body.Children.Add(brand);
                    Note(T("AboutNote"));
                    break;
            }
            page.Content=body;
        }
        void Themes()
        {
            var row=new WrapPanel();
            foreach (var theme in Appearance.ThemeNames)
            {
                Brush B(string k){ var b=new SolidColorBrush(Appearance.Token(theme,k)); b.Freeze(); return b; }
                var tile=new Grid{Width=132,Height=84};
                tile.Children.Add(new Rectangle{Fill=B("Base"),RadiusX=3,RadiusY=3});
                var mini=new Canvas();
                mini.Children.Add(Bar(B("Surface"),0,0,132,14));
                mini.Children.Add(Bar(B("Surface"),0,14,34,54));
                mini.Children.Add(Bar(B("Accent"),0,22,2,8));
                for (int i=0;i<4;i++) { mini.Children.Add(Bar(B("Text"),42,22+i*10,46,3)); mini.Children.Add(Bar(B("Sub"),94,22+i*10,24,3)); }
                mini.Children.Add(Dot(B("Up"),124,23)); mini.Children.Add(Dot(B("Live"),124,33)); mini.Children.Add(Dot(B("Down"),124,43));
                tile.Children.Add(mini);
                var caption=new TextBlock{Text=T(theme),FontSize=11.5,Margin=new Thickness(8,0,0,4),VerticalAlignment=VerticalAlignment.Bottom,Foreground=B("Text")};
                tile.Children.Add(caption);
                var r=new RadioButton{Content=tile,GroupName="Theme",IsChecked=(next.Theme ?? "Carbon")==theme,Style=(Style)owner.FindResource("Tile"),Tag=theme};
                AutomationProperties.SetAutomationId(r,"Theme_"+theme); AutomationProperties.SetName(r,T(theme));
                r.Checked+=(s,e)=>{ next.Theme=(string)((RadioButton)s).Tag; Preview(); };
                row.Children.Add(r);
            }
            body.Children.Add(row);
        }
        static Rectangle Bar(Brush b,double x,double y,double w,double h){ var r=new Rectangle{Fill=b,Width=w,Height=h}; Canvas.SetLeft(r,x); Canvas.SetTop(r,y); return r; }
        static Ellipse Dot(Brush b,double x,double y){ var e=new Ellipse{Fill=b,Width=5,Height=5}; Canvas.SetLeft(e,x); Canvas.SetTop(e,y); return e; }
        void Accents()
        {
            var row=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};
            var field=new TextBox{Text=next.Accent ?? "",Width=110,Margin=new Thickness(4,0,0,10),VerticalAlignment=VerticalAlignment.Center};
            AutomationProperties.SetAutomationId(field,"Accent"); AutomationProperties.SetName(field,T("Accent"));
            foreach (var hex in new[]{"","#D3B47D","#8FB4E8","#6CC6A4","#B9A2F2","#E39A7B","#C9CDD4"})
            {
                var fill=hex=="" ? new SolidColorBrush(Appearance.Token(next.Theme,"Accent")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                var swatch=new Grid{Width=26,Height=26};
                swatch.Children.Add(new Ellipse{Fill=fill});
                if (hex=="") swatch.Children.Add(new TextBlock{Text="A",FontSize=11,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.Black});
                var r=new RadioButton{Content=swatch,GroupName="Accent",IsChecked=(next.Accent ?? "").Equals(hex,StringComparison.OrdinalIgnoreCase),Style=(Style)owner.FindResource("Tile"),Tag=hex,Margin=new Thickness(0,0,6,10)};
                AutomationProperties.SetName(r,hex=="" ? T("Default") : hex);
                r.Checked+=(s,e)=>{ next.Accent=(string)((RadioButton)s).Tag; field.Text=next.Accent; Preview(); };
                row.Children.Add(r);
            }
            field.TextChanged+=(s,e)=>{
                var v=field.Text.Trim(); bool good=Appearance.IsAccent(v);
                field.SetResourceReference(Control.BorderBrushProperty,good ? "Line" : "Down");
                error.Text=good ? "" : T("Invalid",T("Accent"));
                if (good && v!=next.Accent) { next.Accent=v; Preview(); }
            };
            row.Children.Add(field);
            body.Children.Add(row);
        }
        void ScaleSlider()
        {
            var row=new DockPanel{MaxWidth=460,HorizontalAlignment=HorizontalAlignment.Left,Width=460};
            var value=Text(next.Scale.ToString("P0",CultureInfo.CurrentCulture)); value.Width=56; value.VerticalAlignment=VerticalAlignment.Center; value.TextAlignment=TextAlignment.Right;
            DockPanel.SetDock(value,Dock.Right);
            var slider=new Slider{Minimum=.9,Maximum=2,TickFrequency=.1,SmallChange=.1,LargeChange=.1,IsSnapToTickEnabled=true,Value=next.Scale,VerticalAlignment=VerticalAlignment.Center};
            AutomationProperties.SetAutomationId(slider,"Scale"); AutomationProperties.SetName(slider,T("Scale"));
            slider.ValueChanged+=(s,e)=>{ next.Scale=Math.Round(slider.Value,1); value.Text=next.Scale.ToString("P0",CultureInfo.CurrentCulture); };
            // scaling the window while dragging moves the slider under the pointer: apply on release
            slider.PreviewMouseUp+=(s,e)=>Preview(); slider.KeyUp+=(s,e)=>Preview();
            row.Children.Add(value); row.Children.Add(slider);
            body.Children.Add(row);
        }
        void SiteOrder()
        {
            var labels=new ObservableCollection<string>(order.Select(siteLabel));
            var list=new ListBox{ItemsSource=labels,MaxHeight=260,MaxWidth=420,HorizontalAlignment=HorizontalAlignment.Left,MinWidth=300,BorderThickness=new Thickness(1)};
            list.SetResourceReference(Control.BorderBrushProperty,"Line");
            AutomationProperties.SetAutomationId(list,"SiteOrder");
            void Move(int from,int to)
            {
                if (from<0 || to<0 || from>=order.Count || to>=order.Count || from==to) return;
                var v=order[from]; order.RemoveAt(from); order.Insert(to,v); labels.Move(from,to);
            }
            Reorder.Attach(list,i=>true,Move,()=>{});
            list.PreviewKeyDown+=(s,e)=>{
                if ((Keyboard.Modifiers&ModifierKeys.Alt)==0) return;
                var k=e.Key==Key.System ? e.SystemKey : e.Key;
                int at=list.SelectedIndex, to=k==Key.Up ? at-1 : k==Key.Down ? at+1 : -1;
                if (at<0 || to<0 || to>=order.Count) return;
                Move(at,to); list.SelectedIndex=to; e.Handled=true;
            };
            body.Children.Add(list);
            Note(T("DragHint")+" · Alt+↑/↓");
        }
        void Shortcuts()
        {
            var grid=new Grid{Margin=new Thickness(0,12,0,0)};
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(170)});
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var keys=new (string,string)[]{("Ctrl+K","KeyFilter"),("Ctrl+Shift+P","KeyPalette"),("Enter","KeyConnect"),("Ctrl+Enter","KeyConsole"),("Shift+Enter","KeyFull"),
                ("Ctrl+Tab","KeySwitch"),("Ctrl+1 … 9","KeySwitch"),("F5","CheckAll"),("Ctrl+I","KeyInspector"),("Ctrl+B","KeySidebar"),("Ctrl+N","KeyNew"),
                ("F2","Edit"),("Ctrl+D","Duplicate"),("Ctrl+C","Copy"),("Ctrl+Shift+F","KeyFavorite"),("Supr","KeyDelete"),("Ctrl+,","Settings"),("Alt+↑/↓","Order")};
            for (int i=0;i<keys.Length;i++)
            {
                grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
                var key=new Border{Style=(Style)owner.FindResource("Key"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8),Child=Text(keys[i].Item1,"Text",12,false)};
                var what=Text(T(keys[i].Item2)); what.VerticalAlignment=VerticalAlignment.Center; what.Margin=new Thickness(0,0,0,8);
                Grid.SetRow(key,i); Grid.SetRow(what,i); Grid.SetColumn(what,1);
                grid.Children.Add(key); grid.Children.Add(what);
            }
            body.Children.Add(grid);
        }
    }

    public partial class MainWindow
    {
        void SettingsCenter(string start)
        {
            var w=new SettingsWindow(this,Config,Config.Sites(rows),s=>string.IsNullOrEmpty(s) ? T("NoSite") : s,start){
                Home=store.Home,
                CredentialText=credentialCount<0 ? null : T("CredentialSummary",credentialCount,rows.Select(c=>c.Host).Distinct(StringComparer.OrdinalIgnoreCase).Count()),
                OpenFolder=()=>{ if (!isolated) Process.Start(new ProcessStartInfo(store.Home){UseShellExecute=true}); }};
            w.Import=()=>{ w.Close(); ImportDialog(); };
            w.Bulk=()=>{ w.Close(); CredentialDialog(true); };
            if (w.ShowDialog()!=true) { Appearance.Apply(Config); return; }
            var next=w.Result;
            SaveLayout();
            next.SidebarWidth=Config.SidebarWidth; next.InspectorWidth=Config.InspectorWidth; next.ColumnWidths=Config.ColumnWidths;
            next.Collapsed=Config.Collapsed; next.Expanded=Config.Expanded; next.View=Config.View; next.HiddenColumns=Config.HiddenColumns;
            store.Save("settings.json",next); Config=next;
            reach.Timeout=TimeSpan.FromMilliseconds(Config.TimeoutMs);
            Appearance.Apply(Config); Layout();
            Rebuild(View,selected?.Id);
            Flash(T("Done"));
        }
    }
}
