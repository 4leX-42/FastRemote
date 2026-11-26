using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quaygate
{
    // Press, hold and drag an item of a ListBox to move it; the row follows the pointer live and the
    // new order is reported once, when the button is released.
    sealed class Reorder
    {
        readonly ListBox list;
        readonly Func<int,bool> movable;
        readonly Action<int,int> move;
        readonly Action done;
        int index=-1;
        Point start;
        bool dragging;

        Reorder(ListBox list,Func<int,bool> movable,Action<int,int> move,Action done)
        {
            this.list=list; this.movable=movable; this.move=move; this.done=done;
            list.PreviewMouseLeftButtonDown+=Down;
            list.PreviewMouseMove+=Move;
            list.PreviewMouseLeftButtonUp+=Up;
            list.LostMouseCapture+=(s,e)=>Finish();
        }

        public static void Attach(ListBox list,Func<int,bool> movable,Action<int,int> move,Action done) => new Reorder(list,movable,move,done);

        int IndexAt(Point p)
        {
            var hit=list.InputHitTest(p) as DependencyObject;
            var item=hit==null ? null : ItemsControl.ContainerFromElement(list,hit) as ListBoxItem;
            return item==null ? -1 : list.ItemContainerGenerator.IndexFromContainer(item);
        }

        void Down(object sender,MouseButtonEventArgs e)
        {
            start=e.GetPosition(list);
            int at=IndexAt(start);
            index=at>=0 && movable(at) ? at : -1;
        }

        void Move(object sender,MouseEventArgs e)
        {
            if (index<0 || e.LeftButton!=MouseButtonState.Pressed) return;
            var p=e.GetPosition(list);
            if (!dragging)
            {
                if (Math.Abs(p.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance) return;
                dragging=true;
                list.CaptureMouse();
                list.SelectedIndex=index;
                list.Cursor=Cursors.SizeNS;
            }
            int target=IndexAt(new Point(Math.Min(Math.Max(p.X,2),list.ActualWidth-2),p.Y));
            if (target<0)
            {
                int count=((IList)list.ItemsSource).Count;
                target=p.Y<0 ? 0 : count-1;
            }
            while (target>=0 && !movable(target)) target--;
            if (target<0 || target==index) return;
            move(index,target);
            index=target;
            list.SelectedIndex=index;
            e.Handled=true;
        }

        void Up(object sender,MouseButtonEventArgs e)
        {
            if (dragging) { e.Handled=true; list.ReleaseMouseCapture(); }
            else index=-1;
        }

        void Finish()
        {
            if (!dragging) return;
            dragging=false; index=-1;
            list.ClearValue(FrameworkElement.CursorProperty);
            done();
        }
    }
}
