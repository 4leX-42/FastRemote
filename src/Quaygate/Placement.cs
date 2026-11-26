using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Quaygate
{
    static class Placement
    {
        [StructLayout(LayoutKind.Sequential)] struct Pair { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] struct Limits { public Pair Reserved,Size,Position,Minimum,Maximum; }

        public static void Set(Window window,Settings settings)
        {
            if (Program.Offscreen)
            {
                window.Width=1280; window.Height=820; window.Left=-24000; window.Top=-24000;
                window.ShowActivated=false; window.ShowInTaskbar=false;
                return;
            }
            window.SourceInitialized+=(sender,e)=>{
                var handle=new WindowInteropHelper(window).Handle;
                var source=HwndSource.FromHwnd(handle);
                var matrix=source.CompositionTarget.TransformFromDevice;
                var screens=Screen.AllScreens;
                var preferred=screens.FirstOrDefault(s=>!s.Primary && s.DeviceName.EndsWith("DISPLAY1",StringComparison.OrdinalIgnoreCase)) ?? Screen.PrimaryScreen;
                Rect Work(Screen screen)
                {
                    var b=screen.WorkingArea;
                    return new Rect(matrix.Transform(new Point(b.Left,b.Top)),matrix.Transform(new Point(b.Right,b.Bottom)));
                }
                var area=Work(preferred);
                if (settings.Width>=window.MinWidth && settings.Height>=window.MinHeight)
                {
                    var saved=new Rect(settings.Left,settings.Top,settings.Width,settings.Height);
                    var visible=screens.Select(Work).OrderByDescending(r=>{var i=Rect.Intersect(r,saved);return i.IsEmpty ? 0 : i.Width*i.Height;}).First();
                    if (visible.IntersectsWith(saved)) area=visible;
                    window.Width=Math.Min(area.Width,settings.Width); window.Height=Math.Min(area.Height,settings.Height);
                    window.Left=Math.Max(area.Left,Math.Min(settings.Left,area.Right-window.Width));
                    window.Top=Math.Max(area.Top,Math.Min(settings.Top,area.Bottom-window.Height));
                }
                else
                {
                    window.Width=Math.Min(1550,area.Width*.82); window.Height=Math.Min(1050,area.Height*.85);
                    window.Left=area.Left+(area.Width-window.Width)/2; window.Top=area.Top+(area.Height-window.Height)/2;
                }
                source.AddHook((IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)=>{
                    if (msg==0x24)
                    {
                        var limits=Marshal.PtrToStructure<Limits>(l);
                        var monitor=Screen.FromHandle(hwnd);
                        limits.Position.X=monitor.WorkingArea.Left-monitor.Bounds.Left;
                        limits.Position.Y=monitor.WorkingArea.Top-monitor.Bounds.Top;
                        limits.Size.X=monitor.WorkingArea.Width; limits.Size.Y=monitor.WorkingArea.Height;
                        Marshal.StructureToPtr(limits,l,false); handled=true;
                    }
                    return IntPtr.Zero;
                });
                if (settings.Maximized) window.WindowState=WindowState.Maximized;
            };
        }
    }
}
