using System;

namespace Xamarin.Pulley.Ios
{
    public interface IPulleyDrawerViewControllerDelegate : IPulleyDelegate
    {
        nfloat? CollapsedDrawerHeight(nfloat bottomSafeArea);

        nfloat? PartialRevealDrawerHeight(nfloat bottomSafeArea);

        PulleyPosition[] SupportedDrawerPositions();
    }
}