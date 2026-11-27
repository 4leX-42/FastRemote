using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FastRemote
{
    public enum SessionState { Connecting, Authenticating, Connected, Interrupted, Ended }
    public struct WindowInfo { public IntPtr Handle; public int ProcessId; public string Class; public string Title; public bool Visible; }
    public interface IWindowSource
    {
        IReadOnlyList<WindowInfo> TopLevel();
        bool IsAlive(int processId);
        IReadOnlyList<int> Processes(string name);
        bool Focus(IntPtr window);
        bool Close(IntPtr window);
        bool Kill(int processId);
    }
    public sealed class Session : INotifyPropertyChanged
    {
        public string Id { get; }=Guid.NewGuid().ToString("N");
        public string ConnectionId { get; }
        public string Name { get; }
        public string Endpoint { get; }
        public int ProcessId { get; }
        public DateTime StartedUtc { get; }
        public DateTime? ConnectedUtc { get; private set; }
        public SessionState State { get; private set; }
        public bool EverConnected => ConnectedUtc.HasValue;
        public bool Adopted { get; }
        public IntPtr Window { get; private set; }
        public TimeSpan Duration(DateTime utcNow) => ConnectedUtc.HasValue ? utcNow-ConnectedUtc.Value : TimeSpan.Zero;
        public event PropertyChangedEventHandler PropertyChanged;
        internal Session(string id,string name,string endpoint,int pid,DateTime started,bool adopted)
        { ConnectionId=id; Name=name; Endpoint=endpoint; ProcessId=pid; StartedUtc=started; Adopted=adopted; }
        internal void Update(SessionState state,IntPtr window,DateTime now)
        {
            if (state==State && window==Window) return;
            if (state==SessionState.Connected) ConnectedUtc ??= now;
            State=state;
            Window=window;
            PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
        }
    }
    public sealed class SessionHub
    {
        const string Container="TscShellContainerClass", Credential="Credential Dialog Xaml Host";
        readonly IWindowSource source;
        readonly Func<DateTime> utcNow;
        public SessionHub() : this(new Win32Windows(),()=>DateTime.UtcNow) {}
        internal SessionHub(IWindowSource source,Func<DateTime> utcNow) { this.source=source; this.utcNow=utcNow; }
        public ObservableCollection<Session> Sessions { get; }=new ObservableCollection<Session>();
        public event Action<Session> Ended;
        public Session Track(string connectionId,string name,string endpoint,int processId)
        {
            var existing=Sessions.FirstOrDefault(s=>s.ProcessId==processId);
            if (existing!=null) return existing;
            var session=new Session(connectionId,name,endpoint,processId,utcNow(),false);
            Sessions.Add(session);
            return session;
        }
        public void Poll()
        {
            var windows=source.TopLevel().Where(w=>w.Visible).ToLookup(w=>w.ProcessId);
            var now=utcNow();
            foreach (var s in Sessions.ToArray())
            {
                if (!source.IsAlive(s.ProcessId))
                {
                    s.Update(SessionState.Ended,IntPtr.Zero,now);
                    if (Sessions.Remove(s)) Ended?.Invoke(s);
                    continue;
                }
                var visible=windows[s.ProcessId];
                var window=visible.FirstOrDefault(w=>w.Class==Container);
                if (window.Handle!=IntPtr.Zero) s.Update(SessionState.Connected,window.Handle,now);
                else
                {
                    window=visible.FirstOrDefault(w=>w.Class==Credential);
                    if (window.Handle!=IntPtr.Zero) s.Update(SessionState.Authenticating,window.Handle,now);
                    else s.Update(s.EverConnected ? SessionState.Interrupted : SessionState.Connecting,visible.FirstOrDefault().Handle,now);
                }
            }
        }
        public Session Find(string connectionId) => Sessions.LastOrDefault(s=>s.ConnectionId==connectionId);
        public bool Focus(Session s) => s!=null && Sessions.Contains(s) && s.Window!=IntPtr.Zero && source.Focus(s.Window);
        public bool Disconnect(Session s)
        {
            if (s==null || !Sessions.Contains(s)) return false;
            var window=source.TopLevel().Where(w=>w.ProcessId==s.ProcessId && w.Class==Container)
                .OrderByDescending(w=>w.Visible).FirstOrDefault();
            return window.Handle!=IntPtr.Zero && source.Close(window.Handle);
        }
        public int DisconnectAll() => Sessions.Select(s=>s.ProcessId).Distinct().ToArray().Count(source.Kill);
        public int Adopt(IEnumerable<KeyValuePair<string,string>> endpointsById)
        {
            var endpoints=endpointsById.Where(p=>!string.IsNullOrEmpty(p.Value)).ToArray();
            var candidates=new HashSet<int>(source.Processes("mstsc"));
            candidates.ExceptWith(Sessions.Select(s=>s.ProcessId));
            int count=0;
            foreach (var w in source.TopLevel())
            {
                if (!w.Visible || w.Class!=Container || w.Title==null || !candidates.Contains(w.ProcessId)) continue;
                var match=endpoints.FirstOrDefault(p=>w.Title.StartsWith(p.Value+" - ",StringComparison.OrdinalIgnoreCase));
                if (match.Value==null) continue;
                candidates.Remove(w.ProcessId);
                var now=utcNow();
                var session=new Session(match.Key,match.Value,match.Value,w.ProcessId,now,true);
                session.Update(SessionState.Connected,w.Handle,now);
                Sessions.Add(session);
                count++;
            }
            return count;
        }
    }
    internal sealed class Win32Windows : IWindowSource
    {
        delegate bool EnumProc(IntPtr window,IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out int pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int capacity);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder title,int capacity);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);

        public IReadOnlyList<WindowInfo> TopLevel()
        {
            var windows=new List<WindowInfo>();
            var name=new StringBuilder(256);
            EnumWindows((window,_)=>{
                if (GetWindowThreadProcessId(window,out int pid)==0) return true;
                name.Clear();
                GetClassName(window,name,name.Capacity);
                var title=new StringBuilder(GetWindowTextLength(window)+1);
                GetWindowText(window,title,title.Capacity);
                windows.Add(new WindowInfo { Handle=window, ProcessId=pid, Class=name.ToString(), Title=title.ToString(), Visible=IsWindowVisible(window) });
                return true;
            },IntPtr.Zero);
            return windows;
        }
        public IReadOnlyList<int> Processes(string name)
        {
            var ids=new List<int>();
            var processes=Process.GetProcessesByName(name);
            try { foreach (var p in processes) ids.Add(p.Id); }
            finally { foreach (var p in processes) p.Dispose(); }
            return ids;
        }
        public bool IsAlive(int processId)
        {
            try { using var p=Process.GetProcessById(processId); return !p.HasExited; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is Win32Exception) { return false; }
        }
        public bool Focus(IntPtr window)
        {
            if (IsIconic(window)) ShowWindow(window,9);
            return SetForegroundWindow(window);
        }
        public bool Close(IntPtr window) => PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);
        public bool Kill(int processId)
        {
            try { using var p=Process.GetProcessById(processId); p.Kill(); return true; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is Win32Exception) { return false; }
        }
    }
}
