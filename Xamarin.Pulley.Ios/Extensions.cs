using System.Linq;
using Foundation;
using UIKit;

namespace Xamarin.Pulley.Ios
{
    public static class Extensions
    {
        public static void ConstrainToParent(this UIView view)
        {
            view?.ConstrainToParent(UIEdgeInsets.Zero);
        }
    
        public static void ConstrainToParent(this UIView view, UIEdgeInsets insets)
        {
            var parent = view?.Superview;
            if (parent == null)
                return;

            view.TranslatesAutoresizingMaskIntoConstraints = false;
            
            var metrics = new NSDictionary(
                new NSString("left"), insets.Left,
                new NSString("right"), insets.Right,
                new NSString("top"), insets.Top,
                new NSString("bottom"), insets.Bottom
            );
                
            parent.AddConstraints(
                NSLayoutConstraint.FromVisualFormat("H:|-(left)-[view]-(right)-|", NSLayoutFormatOptions.AlignAllLeft, 
                    metrics, NSDictionary.FromObjectAndKey(view, new NSString("view"))).Concat(
                NSLayoutConstraint.FromVisualFormat("V:|-(top)-[view]-(bottom)-|", NSLayoutFormatOptions.AlignAllLeft, 
                    metrics, NSDictionary.FromObjectAndKey(view, new NSString("view")))).ToArray());
        }
    }
}