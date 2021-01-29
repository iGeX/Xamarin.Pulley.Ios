using System;

namespace Xamarin.Pulley.Ios
{
    public interface IPulleyDelegate
    {
        void DrawerPositionDidChange(PulleyViewController drawer, nfloat bottomSafeArea);
    
        void MakeUIAdjustmentsForFullscreen(nfloat progress, nfloat bottomSafeArea);
    
        void DrawerChangedDistanceFromBottom(PulleyViewController drawer, nfloat distance, nfloat bottomSafeArea);

        void DrawerDisplayModeDidChange(PulleyViewController drawer);
    }
}