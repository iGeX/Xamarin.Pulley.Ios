using System;
using CoreGraphics;
using UIKit;

namespace Xamarin.Pulley.Ios
{
    public interface IPulleyPassthroughScrollViewDelegate
    {
        bool ShouldTouchPassthroughScrollView(PulleyPassthroughScrollView scrollView, CGPoint point);
        
        UIView ViewToReceiveTouch(PulleyPassthroughScrollView scrollView, CGPoint point);
    }

    public class PulleyPassthroughScrollView: UIScrollView
    {
        public IPulleyPassthroughScrollViewDelegate TouchDelegate
        {
            set => _TouchDelegate = new WeakReference<IPulleyPassthroughScrollViewDelegate>(value);
        }

        private WeakReference<IPulleyPassthroughScrollViewDelegate> _TouchDelegate;

        public override UIView HitTest(CGPoint point, UIEvent uievent)
        {
            if (_TouchDelegate.TryGetTarget(out var touchDelegate) &&
                touchDelegate.ShouldTouchPassthroughScrollView(this, point))
            {
                var view = touchDelegate.ViewToReceiveTouch(this, point);
                if (view != null)
                    return view.HitTest(view.ConvertPointFromView(point, this), uievent);
            }
                
            return base.HitTest(point, uievent);
        }
    }
}