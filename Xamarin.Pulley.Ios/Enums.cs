namespace Xamarin.Pulley.Ios
{
    public enum PulleyPosition
    {
        Collapsed = 0,
        PartiallyRevealed = 1,
        Open = 2,
        Closed = 3
    }
    
    public enum PulleyDisplayMode
    {
        Panel,
        Drawer,
        Compact,
        Automatic
    }
    
    public enum PulleyPanelCornerPlacement
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
    
    public enum PulleyCompactCornerPlacement
    {
        BottomLeft,
        BottomRight
    }
    
    public enum PulleySnapMode
    {
        NearestPosition,
        NearestPositionUnlessExceeded
    }
}