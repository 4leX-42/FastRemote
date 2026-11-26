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

namespace Quaygate.Tests
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
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Quaygate;component/Controls.xaml",UriKind.Relative)});
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
            for(int i=0;i<200;i++) rows.Add(new Connection{Name="Desk "+i,Host="test.invalid",Folder="North"});
            var home=new Store(directory);
            var window=new MainWindow(home,settings,rows,true);
            var frame=(FrameworkElement)window.Content;
            foreach(var scale in new[]{1d,1.2})
            {
                Appearance.Scale(window,frame,scale);
                if (TextOptions.GetTextFormattingMode(window)!=(scale==1 ? TextFormattingMode.Display : TextFormattingMode.Ideal)) throw new InvalidOperationException("Formatting");
                frame.Measure(new Size(1280,800)); frame.Arrange(new Rect(0,0,1280,800)); frame.UpdateLayout();
                var bitmap=new RenderTargetBitmap(1280,800,96,96,PixelFormats.Pbgra32);
                bitmap.Render(frame);
                var pixels=new byte[1280*800*4]; bitmap.CopyPixels(pixels,1280*4,0);
                int bright=0;
                for (int i=0;i<pixels.Length;i+=4) if (pixels[i]>160 && pixels[i+1]>160 && pixels[i+2]>160) bright++;
                if (bright<300) throw new InvalidOperationException("Empty frame");
            }
            var box=(TextBox)window.FindName("Search");
            var count=(TextBlock)window.FindName("Count");
            var peer=new TextBoxAutomationPeer(box);
            var pattern=(IValueProvider)peer.GetPattern(PatternInterface.Value);
            pattern.SetValue("desk"); // warm-up: the first filter pays JIT, the measured one is a normal keystroke
            var timer=Stopwatch.StartNew();
            pattern.SetValue("desk 199"); string result=count.Text;
            timer.Stop();
            Console.WriteLine("In-process WPF SetValue to count (200 rows): {0:F2} ms",timer.Elapsed.TotalMilliseconds);
            if (result!="1 of 200 connections") throw new InvalidOperationException(result);
            // one keystroke must stay well under a frame budget on a loaded machine; typical is 2-10 ms
            if (timer.Elapsed.TotalMilliseconds>=150) throw new InvalidOperationException("Search took "+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms");
            window.Close();
            app.Shutdown();
        }
    }
}
