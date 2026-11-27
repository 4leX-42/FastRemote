using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FastRemote.Tests
{
    public static class VisualScenario
    {
        public static void Run(string directory)
        {
            Exception failure=null;
            var thread=new Thread(()=>{
                try { Render(directory); } catch(Exception ex) { failure=ex; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure!=null) throw new InvalidOperationException("WPF rendering or filtering failed.",failure);
        }
        static void Render(string directory)
        {
            var app=new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/FastRemote;component/Controls.xaml",UriKind.Relative)});
            var settings=new Settings{Language="en"};
            foreach(var preset in new[]{"Carbon","Graphite","Light","Contrast"})
            foreach(var accent in new[]{"","#010204","#FEF8FE","#4F7D31"})
            {
                settings.Theme=preset; settings.Accent=accent;
                Appearance.Apply(settings);
                Color BrushColor(string key)=>((SolidColorBrush)app.Resources[key]).Color;
                void Contrast(string text,string background)
                {
                    if (Appearance.Ratio(BrushColor(text),BrushColor(background))<4.5) throw new InvalidOperationException(preset+" "+text+"/"+background);
                }
                foreach(var surface in new[]{"Base","Surface","Hover"})
                foreach(var text in new[]{"Text","Sub","Accent"}) Contrast(text,surface);
                Contrast("Ink","Accent");
            }
            settings.Theme="Carbon"; settings.Accent=""; Appearance.Apply(settings);
            var rows=new List<Connection>();
            for(int i=0;i<200;i++) rows.Add(new Connection{Name="Desk "+i,Host="test.invalid",Folder=i%3==0 ? "North / Rack "+(i%2) : i%3==1 ? "South" : "East",Username="qa"+i,Domain="LAB",Favorite=i%7==0,Kind=i%4==0 ? "dc" : "server",Tags=i%5==0 ? "core" : null});
            var home=new Store(directory);
            var window=new MainWindow(home,settings,rows,true);
            var frame=(FrameworkElement)window.Content;
            var shots=Environment.GetEnvironmentVariable("FASTREMOTE_SHOTS");
            foreach(var scale in new[]{1d,1.25,1.3,1.5,2})
            {
                Appearance.Scale(window,frame,scale);
                if (TextOptions.GetTextFormattingMode(window)!=(scale==1 ? TextFormattingMode.Display : TextFormattingMode.Ideal)) throw new InvalidOperationException("Formatting");
                int W=(int)(1100*scale),H=(int)(660*scale);
                frame.Measure(new Size(W,H)); frame.Arrange(new Rect(0,0,W,H)); frame.UpdateLayout();
                var bitmap=new RenderTargetBitmap(W,H,96,96,PixelFormats.Pbgra32);
                bitmap.Render(frame);
                var pixels=new byte[W*H*4]; bitmap.CopyPixels(pixels,W*4,0);
                int bright=0;
                for (int i=0;i<pixels.Length;i+=4) if (pixels[i]>160 && pixels[i+1]>160 && pixels[i+2]>160) bright++;
                if (bright<300) throw new InvalidOperationException("Empty frame");
                if (shots!=null)
                {
                    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file=File.Create(Path.Combine(shots,"scale-"+(int)(scale*100)+".png")); encoder.Save(file);
                }
            }
            if (shots!=null)
            {
                void Shot(FrameworkElement e,int w,int h,string name)
                {
                    e.Measure(new Size(w,h)); e.Arrange(new Rect(0,0,w,h)); e.UpdateLayout();
                    var b=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32); b.Render(e);
                    var enc=new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(b));
                    using var f=File.Create(Path.Combine(shots,name)); enc.Save(f);
                }
                Appearance.Scale(window,frame,1);
                typeof(MainWindow).GetMethod("OpenPalette",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(window,new object[]{false});
                ((TextBox)window.FindName("PaletteText")).Text="desk 1";
                Shot(frame,1100,660,"palette.png");
                typeof(MainWindow).GetMethod("ClosePalette",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(window,null);
                rows.Add(new Connection{Name="Backup East",Host="backup.invalid",Folder="East"});
                rows.Add(new Connection{Name="Print North",Host="print.invalid",Folder="North"});
                MainWindow.CredentialPreview=()=>new List<Credentials.Stored>{
                    new Credentials.Stored{Host="test.invalid",User=@"LAB\qa2",Copyable=true,Group=0},
                    new Credentials.Stored{Host="other.invalid",User=@"LAB\admin",Copyable=true,Group=1},
                    new Credentials.Stored{Host="backup.invalid",User=@"LAB\backup",Copyable=true,Group=2},
                    new Credentials.Stored{Host="print.invalid",User=@"LAB\backup",Copyable=true,Group=2}};
                Dialog.Preview=d=>{
                    Shot((FrameworkElement)d.Content,440,900,"editor.png");
                    // the open list: each option as the drop-down draws it
                    var combo=(ComboBox)LogicalTreeHelper.FindLogicalNode((DependencyObject)d.Content,"FCredCombo");
                    var list=new StackPanel{Background=(Brush)app.Resources["Surface"],Width=400};
                    foreach (var item in combo.Items) list.Children.Add(new ComboBoxItem{Content=item,ContentTemplate=combo.ItemTemplate});
                    Shot(list,400,300,"credentials.png");
                };
                try
                {
                    var editor=typeof(MainWindow).GetMethod("Editor",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                    editor.Invoke(window,new object[]{rows[2],false});
                }
                finally { Dialog.Preview=null; MainWindow.CredentialPreview=null; }
                foreach (var page in new[]{"Appearance","Monitoring","Keyboard"})
                {
                    var w=new SettingsWindow(window,settings,settings.Sites(rows),x=>x,page);
                    Shot((FrameworkElement)w.Content,860,600,"settings-"+page+".png");
                    w.Close();
                }
            }
            var box=(TextBox)window.FindName("FilterBox");
            var count=(TextBlock)window.FindName("ViewTitle");
            var peer=new TextBoxAutomationPeer(box);
            var pattern=(IValueProvider)peer.GetPattern(PatternInterface.Value);
            pattern.SetValue("desk"); // warm-up: the first filter pays JIT, the measured one is a normal keystroke
            var timer=Stopwatch.StartNew();
            pattern.SetValue("desk 199"); string result=count.Text;
            timer.Stop();
            Console.WriteLine("In-process WPF SetValue to count (200 rows): {0:F2} ms",timer.Elapsed.TotalMilliseconds);
            if (result!="Connections (1)") throw new InvalidOperationException(result);
            // one keystroke must stay well under a frame budget on a loaded machine; typical is 2-10 ms
            if (timer.Elapsed.TotalMilliseconds>=150) throw new InvalidOperationException("Search took "+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms");
            window.Close();
            app.Shutdown();
        }
    }
}
