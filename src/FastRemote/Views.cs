using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace FastRemote
{
    public class NavItem : INotifyPropertyChanged
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Icon { get; set; }
        public string Count { get; set; }
        public int Depth { get; set; }
        public bool IsHeader { get; set; }
        public bool HasChildren { get; set; }
        public bool Expanded { get; set; }
        public string Site { get; set; }
        public string ConnectionId { get; set; }
        public bool HeaderAction { get; set; }
        public Brush Tint { get; set; }
        public Visibility LiveVisibility { get; set; } = Visibility.Collapsed;
        public bool Movable => Key!=null && Key.StartsWith("site:",StringComparison.Ordinal);
        public Thickness Indent => new Thickness(6+Depth*22,0,0,0);
        public string Chevron => HasChildren ? (Expanded ? "\uE70D" : "\uE76C") : "";
        public double Height => IsHeader ? (HeaderAction ? 44 : 30) : 32;
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify() => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
        public override string ToString() => Label;
    }

    public class PaletteItem
    {
        public bool IsHeader { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; } = "";
        public string Icon { get; set; } = "";
        public Brush Tint { get; set; }
        public string Hint { get; set; } = "";
        public Action Run { get; set; }
        public Action Reveal { get; set; }
        public int Score { get; set; }
        public double Height => IsHeader ? 30 : 36;
        public override string ToString() => Title;
    }

    // One choice of the credential selector: title (user), detail (where it comes from) and an optional shared tag.
    public sealed class CredOption
    {
        public string Key { get; set; }
        public string Icon { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public bool Shared { get; set; }
        public string Tip { get; set; }
        public Visibility SharedVisibility => Shared ? Visibility.Visible : Visibility.Collapsed;
    }
    public class LiveItem : INotifyPropertyChanged
    {
        public Session Session { get; set; }
        public string Name { get; set; }
        public string Clock { get; set; }
        public SessionState State => Session.State;
        public bool Current { get; set; }
        public string Tip { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify() => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    }
}
