using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace Quaygate
{
    internal static class Program
    {
        public static bool Offscreen => Environment.GetEnvironmentVariable("QUAYGATE_OFFSCREEN")=="1";
        static string token;
        static uint message;
        delegate bool EnumProc(IntPtr hwnd,IntPtr data);
        [DllImport("user32")] static extern bool EnumWindows(EnumProc callback,IntPtr data);
        [DllImport("user32",CharSet=CharSet.Unicode)] static extern IntPtr GetProp(IntPtr hwnd,string name);
        [DllImport("user32",CharSet=CharSet.Unicode)] static extern bool SetProp(IntPtr hwnd,string name,IntPtr data);
        [DllImport("user32",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
        [DllImport("user32")] static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
        [DllImport("user32")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out int pid);
        [DllImport("user32")] static extern bool AllowSetForegroundWindow(int pid);
        [DllImport("user32")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("kernel32")] static extern IntPtr GetStdHandle(int id);
        [DllImport("kernel32")] static extern bool AttachConsole(uint process);

        static bool Signal()
        {
            for (int attempt=0;attempt<60;attempt++)
            {
                bool sent=false;
                EnumWindows((hwnd,unused)=>{
                    if (GetProp(hwnd,token)==IntPtr.Zero) return true;
                    GetWindowThreadProcessId(hwnd,out var pid);
                    AllowSetForegroundWindow(pid);
                    sent=PostMessage(hwnd,message,IntPtr.Zero,IntPtr.Zero);
                    return false;
                },IntPtr.Zero);
                if (sent) return true;
                Thread.Sleep(60);
            }
            return false;
        }
        static void Listen(Window window)
        {
            var hwnd=new WindowInteropHelper(window).Handle;
            SetProp(hwnd,token,new IntPtr(1));
            HwndSource.FromHwnd(hwnd).AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=>{
                if (msg==message && !Offscreen)
                {
                    if (window.WindowState==WindowState.Minimized) SystemCommands.RestoreWindow(window);
                    var target=window.OwnedWindows.Cast<Window>().LastOrDefault(x=>x.IsVisible) ?? window;
                    target.Activate();
                    SetForegroundWindow(new WindowInteropHelper(target).Handle);
                    handled=true;
                }
                return IntPtr.Zero;
            });
        }
        // First start after the rename: bring the connection list of the preview build (%APPDATA%\RdpDeck) once.
        static void MigratePreviewData()
        {
            if (Environment.GetEnvironmentVariable("QUAYGATE_HOME")!=null) return;
            string appData=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string oldList=Path.Combine(appData,"RdpDeck","connections.json");
            string home=Path.Combine(appData,"Quaygate");
            string newList=Path.Combine(home,"connections.json");
            if (File.Exists(newList) || !File.Exists(oldList)) return;
            try { Directory.CreateDirectory(home); File.Copy(oldList,newList); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        [STAThread]
        public static int Main(string[] args)
        {
            Store store=null;
            try
            {
                MigratePreviewData();
                store=new Store(Store.DefaultHome);
                using var hash=SHA256.Create();
                token="Quaygate."+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(store.Home.TrimEnd('\\').ToUpperInvariant()))).Replace("-","");
                bool smoke=args.Contains("--smoke");
                using var mutex=new Mutex(true,@"Local\"+token,out var owns);
                message=RegisterWindowMessage(token);
                if (!owns) return smoke ? 2 : Signal() ? 0 : 1;
                var app=new Application();
                // The window is built before app.Run: without this, awaits started in the constructor resume on the thread pool.
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(app.Dispatcher));
                app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("Controls.xaml",UriKind.Relative)});
                var settings=store.Load<Settings>("settings.json");
                Appearance.Apply(settings);
                var rows=store.Load<System.Collections.Generic.List<Connection>>("connections.json");
                var main=new MainWindow(store,settings,rows,smoke);
                if (smoke)
                {
                    var stdout=GetStdHandle(-11);
                    if ((stdout==IntPtr.Zero || stdout==new IntPtr(-1)) && AttachConsole(uint.MaxValue))
                        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush=true});
                    Console.WriteLine("Quaygate smoke OK | connections="+rows.Count);
                    return 0;
                }
                Rdp.Clean(store.RdpDirectory);
                main.SourceInitialized+=(s,e)=>Listen(main);
                app.DispatcherUnhandledException+=(s,e)=>{ store.Log(e.Exception); main.ReportError(); e.Handled=true; };
                return app.Run(main);
            }
            catch(Exception ex) { store?.Log(ex); Console.Error.WriteLine("Quaygate: "+ex.GetType().Name); return 1; }
        }
    }
}
