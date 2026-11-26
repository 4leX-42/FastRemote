using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Quaygate
{
    public partial class Dialog : Window
    {
        Func<string> commit;
        public Dialog(MainWindow owner,string title)
        {
            InitializeComponent();
            Owner=owner;
            SetResourceReference(TitleProperty,"L"+title);
            Appearance.Scale(this,Frame,owner.Config.Scale);
            Width=Math.Min(owner.ActualWidth,440*owner.Config.Scale);
            MaxHeight=Math.Max(300,owner.ActualHeight-24);
            if (Program.Offscreen)
            {
                WindowStartupLocation=WindowStartupLocation.Manual;
                Left=-24000; Top=-24000;
                ShowActivated=false;
            }
        }
        public bool Run(Func<string> save=null)
        {
            commit=save;
            return ShowDialog()==true;
        }
        void Accept(object sender,RoutedEventArgs e)
        {
            try
            {
                var error=commit?.Invoke();
                if (string.IsNullOrEmpty(error)) DialogResult=true;
                else DlgError.Text=error;
            }
            catch(Exception ex) { ((MainWindow)Owner).Record(ex); DlgError.Text=Words.T("Failure"); }
        }
        public T Add<T>(T element,string id=null,Panel parent=null) where T : FrameworkElement
        {
            if (id!=null) AutomationProperties.SetAutomationId(element,id);
            (parent ?? Body).Children.Add(element);
            return element;
        }
        public TextBlock Note(string text,string id=null) => Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap},id);
        public void Label(string key,Panel parent=null)
        {
            var label=new TextBlock{Style=(Style)FindResource("Caption")};
            label.SetResourceReference(TextBlock.TextProperty,"L"+key);
            Add(label,null,parent);
        }
        public TextBox Field(string key,string value,string id,Panel parent=null)
        {
            Label(key,parent);
            var box=Add(new TextBox{Text=value ?? ""},id,parent);
            AutomationProperties.SetName(box,Words.T(key));
            return box;
        }
        public ComboBox Choices(string key,string[] values,string selected,Func<string,string> label,string id,Panel parent=null)
        {
            Label(key,parent);
            var combo=Add(new ComboBox{ItemsSource=values.Select(v=>new KeyValuePair<string,string>(v,label(v))).ToList(),
                SelectedValuePath="Key",DisplayMemberPath="Value",SelectedValue=selected},id,parent);
            AutomationProperties.SetName(combo,Words.T(key));
            return combo;
        }
        public Button Button(string key,string id,Action onClick,Panel parent=null)
        {
            var button=Add(new Button{Margin=new Thickness(0,5,5,0)},id,parent);
            button.SetResourceReference(ContentControl.ContentProperty,"L"+key);
            button.Click+=(s,e)=>onClick();
            return button;
        }
        public Func<string> OptionFields(Dictionary<string,string> values,bool site)
        {
            var controls=new Dictionary<Option,Func<string>>();
            var panel=new StackPanel();
            if (!site)
            {
                var toggle=Add(new CheckBox{Content=Words.T("Advanced")},"FAdvanced");
                panel.Visibility=Visibility.Collapsed;
                toggle.Checked+=(s,e)=>panel.Visibility=Visibility.Visible;
                toggle.Unchecked+=(s,e)=>panel.Visibility=Visibility.Collapsed;
            }
            Add(panel);
            foreach (var opt in Option.All)
            {
                if (!site && opt.Id=="Port") continue;
                values.TryGetValue(opt.Id,out var initial);
                if (opt.Choices=="number" || opt.Choices=="text")
                {
                    var field=Field(opt.Id,initial,"Opt"+opt.Id,panel);
                    field.ToolTip=Words.T(site ? "Default" : "Inherit");
                    controls[opt]=()=>field.Text.Trim();
                    continue;
                }
                string Display(string v)
                {
                    if (v=="") return Words.T(site ? "Default" : "Inherit");
                    if (opt.Choices=="0|1") return Words.T(v=="1" ? "Yes" : "No");
                    if (opt.Choices=="0|1|2") return Words.T(opt.Id+v);
                    return Words.T(v);
                }
                var choices=Choices(opt.Id,new[]{""}.Concat(opt.Choices.Split('|')).ToArray(),initial ?? "",Display,"Opt"+opt.Id,panel);
                controls[opt]=()=>(string)choices.SelectedValue;
            }
            return ()=>{
                foreach (var pair in controls)
                {
                    var value=pair.Value();
                    if (!string.IsNullOrEmpty(value) && !pair.Key.Valid(value)) return Words.T("Invalid",Words.T(pair.Key.Id));
                }
                foreach (var pair in controls)
                {
                    var v=pair.Value();
                    if (string.IsNullOrEmpty(v)) values.Remove(pair.Key.Id); else values[pair.Key.Id]=v;
                }
                return null;
            };
        }
    }
}
