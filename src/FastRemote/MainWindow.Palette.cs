using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static FastRemote.Words;

namespace FastRemote
{
    public partial class MainWindow
    {
        bool sessionsOnly;
        IInputElement before;

        void OpenPalette(bool sessions)
        {
            sessionsOnly=sessions && live.Count>0;
            before=Keyboard.FocusedElement;
            Palette.Visibility=Visibility.Visible;
            updating=true; PaletteText.Text=""; updating=false;
            PaletteFoot.Text="↑↓  "+T("GoTo")+"     Enter  "+T("Connect")+"     Alt+Enter  "+T("Show")+"     Esc  "+T("Close");
            Populate();
            PaletteText.Focus();
        }
        void ClosePalette()
        {
            if (Palette.Visibility!=Visibility.Visible) return;
            Palette.Visibility=Visibility.Collapsed;
            PaletteList.ItemsSource=null;
            if (before is UIElement e && e.IsVisible && !PaletteText.Equals(e)) e.Focus(); else List.Focus();
        }
        void PaletteOutside(object sender,MouseButtonEventArgs e) { e.Handled=true; ClosePalette(); }
        void PaletteChanged(object sender,TextChangedEventArgs e) { if (!updating && Palette.Visibility==Visibility.Visible) Populate(); }

        Brush Tint(string key)=>(Brush)FindResource(key);
        PaletteItem Of(Connection c)
        {
            var s=c.Live;
            return new PaletteItem{Title=c.Name,Detail=string.Join("  ·  ",new[]{c.Endpoint,c.SiteText,c.Login}.Where(x=>!string.IsNullOrEmpty(x))),
                Icon=c.KindIcon,Tint=Tint(s!=null ? "Live" : c.Reach.State==Reach.Down ? "Down" : "Sub"),Hint=s!=null ? T("S"+s.State) : "",
                Run=()=>{ Reveal(c); Execute(s!=null ? "Focus" : "Connect"); },Reveal=()=>Reveal(c)};
        }
        void Reveal(Connection c)
        {
            if (!InView(View).Contains(c)) Nav.SelectedItem=nav.First(n=>n.Key=="all");
            if (!shown.Contains(c)) { FilterBox.Clear(); }
            List.SelectedItem=c; List.ScrollIntoView(c);
        }
        IEnumerable<PaletteItem> Actions()
        {
            PaletteItem A(string key,string icon,string hint,Action run)=>new PaletteItem{Title=T(key),Icon=icon,Tint=Tint("Sub"),Hint=hint,Run=run};
            yield return A("New","\uE710","Ctrl+N",()=>Execute("New"));
            yield return A("CheckVisible","\uE72C","F5",()=>Execute("CheckVisible"));
            yield return A("Bulk","\uE8D7","",()=>Execute("Bulk"));
            yield return A("Import","\uE896","",()=>Execute("Import"));
            yield return A("Settings","\uE713","Ctrl+,",()=>Execute("Settings"));
            yield return A("Inspector","\uE8A0","Ctrl+I",()=>Execute("Inspector"));
            yield return A("Collapse","\uE76B","Ctrl+B",()=>Execute("Sidebar"));
            if (live.Count>0) yield return A("DisconnectAll","\uE711","",()=>Execute("DisconnectAll"));
            if (selected!=null)
            {
                var c=selected;
                yield return new PaletteItem{Title=T("Edit")+"  «"+c.Name+"»",Icon="\uE70F",Tint=Tint("Sub"),Hint="F2",Run=()=>Editor(c,false)};
                if (c.Live!=null) yield return new PaletteItem{Title=T("Disconnect")+"  «"+c.Name+"»",Icon="\uE711",Tint=Tint("Sub"),Run=()=>hub.Disconnect(c.Live)};
            }
            foreach (var n in nav.Where(n=>!n.IsHeader))
            {
                var item=n;
                yield return new PaletteItem{Title=T("GoTo")+"  "+n.Label,Icon=n.Icon,Tint=Tint("Sub"),Run=()=>Nav.SelectedItem=item};
            }
        }
        void Populate()
        {
            var words=Connection.Fold(PaletteText.Text).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            var items=new List<PaletteItem>();
            void Group(string key,IEnumerable<PaletteItem> found,int max)
            {
                var list=found.Take(max).ToList();
                if (list.Count==0) return;
                items.Add(new PaletteItem{IsHeader=true,Title=T(key).ToUpper(System.Globalization.CultureInfo.CurrentCulture)});
                items.AddRange(list);
            }
            var sessions=live.Select(l=>Row(l.Session.ConnectionId)).Where(c=>c!=null).Distinct();
            if (words.Length==0)
            {
                Group("LiveView",sessions.Select(Of),9);
                if (!sessionsOnly)
                {
                    Group("Recent",RecentRows().Where(c=>c.Live==null).Select(Of),6);
                    Group("Actions",Actions().Take(5),5);
                }
            }
            else
            {
                IEnumerable<PaletteItem> Rank(IEnumerable<Connection> source)=>source.Select(c=>{ var p=Of(c); p.Score=Finder.Score(c.SearchText,words); return p; })
                    .Where(p=>p.Score>0).OrderByDescending(p=>p.Score);
                Group("LiveView",Rank(sessions),9);
                if (!sessionsOnly)
                {
                    Group("ConnectionsGroup",Rank(rows.Where(c=>c.Live==null)),12);
                    Group("Actions",Actions().Select(a=>{ a.Score=Finder.Score(Connection.Fold(a.Title),words); return a; }).Where(a=>a.Score>0).OrderByDescending(a=>a.Score),6);
                }
            }
            PaletteList.ItemsSource=items;
            PaletteList.SelectedItem=items.FirstOrDefault(i=>!i.IsHeader);
            PaletteEmpty.Text=T("PaletteEmpty",PaletteText.Text);
            PaletteEmpty.Visibility=items.Count==0 ? Visibility.Visible : Visibility.Collapsed;
        }
        void MovePalette(int step)
        {
            var items=(PaletteList.ItemsSource as List<PaletteItem>) ?? new List<PaletteItem>();
            int i=PaletteList.SelectedIndex;
            for (int n=0;n<items.Count;n++)
            {
                i=(i+step+items.Count)%items.Count;
                if (!items[i].IsHeader) { PaletteList.SelectedIndex=i; PaletteList.ScrollIntoView(items[i]); return; }
            }
        }
        void PaletteKeys(object sender,KeyEventArgs e)
        {
            var key=e.Key==Key.System ? e.SystemKey : e.Key;
            bool alt=(Keyboard.Modifiers&ModifierKeys.Alt)!=0;
            switch (key)
            {
                case Key.Escape: ClosePalette(); break;
                case Key.Down: MovePalette(1); break;
                case Key.Up: MovePalette(-1); break;
                case Key.Tab: MovePalette((Keyboard.Modifiers&ModifierKeys.Shift)!=0 ? -1 : 1); break;
                case Key.PageDown: for (int i=0;i<8;i++) MovePalette(1); break;
                case Key.PageUp: for (int i=0;i<8;i++) MovePalette(-1); break;
                case Key.Enter: RunPalette(PaletteList.SelectedItem as PaletteItem,alt); break;
                default: return;
            }
            e.Handled=true;
        }
        void PaletteClick(object sender,MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(PaletteList,e.OriginalSource as DependencyObject) is ListBoxItem item) RunPalette(item.DataContext as PaletteItem,false);
        }
        void RunPalette(PaletteItem item,bool reveal)
        {
            if (item==null || item.IsHeader) return;
            ClosePalette();
            try { if (reveal && item.Reveal!=null) item.Reveal(); else item.Run?.Invoke(); }
            catch(Exception ex) { Record(ex); ReportError(); }
        }
    }
}
