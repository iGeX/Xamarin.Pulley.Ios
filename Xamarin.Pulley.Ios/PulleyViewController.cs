using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using UIKit;

namespace Xamarin.Pulley.Ios
{
    [Register (nameof(PulleyViewController))]
    public class PulleyViewController : UIViewController, IPulleyDrawerViewControllerDelegate, 
        IPulleyPassthroughScrollViewDelegate, IUIScrollViewDelegate 
    {
        public static nfloat PulleyDefaultCollapsedHeight { get; set; } = 68.0f;
        
        public static nfloat PulleyDefaultPartialRevealHeight { get; set; } = 264.0f;

        public static nfloat BounceOverflowMargin { get; set; } = 20.0f;

        public static bool Ios10 { get; } = UIDevice.CurrentDevice.CheckSystemVersion(10, 0);
        
        public static bool Ios11 { get; } = UIDevice.CurrentDevice.CheckSystemVersion(11, 0);

        public static bool Ios13 { get; } = UIDevice.CurrentDevice.CheckSystemVersion(13, 0);
        
        private static PulleyPosition[] PulleyPositionAll { get; } =
        {
            PulleyPosition.Collapsed,
            PulleyPosition.PartiallyRevealed,
            PulleyPosition.Open,
            PulleyPosition.Closed
        };
        
        private static PulleyPosition[] PulleyPositionCompact { get; } =
        {
            PulleyPosition.Collapsed,
            PulleyPosition.Open,
            PulleyPosition.Closed
        };

        public bool IsPhone { get; } = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone;

        [Outlet]
        UIView PrimaryContentContainerView { get; set; }
        
        [Outlet]
        UIView DrawerContentContainerView { get; set; }
        
        // Internal
        private readonly UIView _PrimaryContentContainer = new UIView();

        private readonly UIView _DrawerContentContainer = new UIView();

        private readonly UIView _DrawerShadowView = new UIView();

        private readonly PulleyPassthroughScrollView _DrawerScrollView = new PulleyPassthroughScrollView();

        private readonly UIView _BackgroundDimmingView = new UIView();
        
        private UITapGestureRecognizer _DimmingViewTapRecognizer;

        private CGPoint _LastDragTargetContentOffset = CGPoint.Empty;

        private UIViewController _PrimaryContentViewController;

        public UIViewController PrimaryContentViewController
        {
            get => _PrimaryContentViewController;
            private set
            {
                _PrimaryContentViewController?.WillMoveToParentViewController(null);
                _PrimaryContentViewController?.View?.RemoveFromSuperview();
                _PrimaryContentViewController?.RemoveFromParentViewController();

                _PrimaryContentViewController = value;
                if (_PrimaryContentViewController == null)
                    return;

                AddChildViewController(_PrimaryContentViewController);
                _PrimaryContentViewController.LoadViewIfNeeded();
                if (_PrimaryContentViewController.View != null)
                    _PrimaryContentContainer.AddSubview(_PrimaryContentViewController.View);

                _PrimaryContentViewController.View.ConstrainToParent();
                _PrimaryContentViewController.DidMoveToParentViewController(this);

                if (!IsViewLoaded)
                    return;

                View?.SetNeedsLayout();
                SetNeedsSupportedDrawerPositionsUpdate();
            }
        }
        
        private UIViewController _DrawerContentViewController;

        public UIViewController DrawerContentViewController
        {
            get => _DrawerContentViewController;
            private set
            {
                _DrawerContentViewController?.WillMoveToParentViewController(null);
                _DrawerContentViewController?.View?.RemoveFromSuperview();
                _DrawerContentViewController?.RemoveFromParentViewController();

                _DrawerContentViewController = value;
                if (_DrawerContentViewController == null)
                    return;

                AddChildViewController(_DrawerContentViewController);
                _DrawerContentViewController.LoadViewIfNeeded();
                if (_DrawerContentViewController.View != null)
                    _DrawerContentContainer.AddSubview(_DrawerContentViewController.View);

                _DrawerContentViewController.View.ConstrainToParent();
                _DrawerContentViewController.DidMoveToParentViewController(this);

                if (!IsViewLoaded)
                    return;

                View?.SetNeedsLayout();
                SetNeedsSupportedDrawerPositionsUpdate();
            }
        }

        public nfloat BottomSafeSpace => PulleySafeAreaInsets.Bottom;

        private WeakReference<IPulleyDelegate> _Delegate;
        
        public IPulleyDelegate Delegate
        {
            get
            {
                IPulleyDelegate target = null;
                return _Delegate?.TryGetTarget(out target) ?? false ? target : null;
            }
            set => _Delegate = new WeakReference<IPulleyDelegate>(value);
        }

        private PulleyPosition _DrawerPosition = PulleyPosition.Collapsed; 
        
        public PulleyPosition DrawerPosition
        {
            get => _DrawerPosition;
            set
            {
                _DrawerPosition = value;
                SetNeedsStatusBarAppearanceUpdate();
            }
        }
        
        public nfloat VisibleDrawerHeight => _DrawerPosition == PulleyPosition.Closed 
            ? 0f 
            : _DrawerScrollView.Bounds.Height;

        public UIBlurEffectStyle DefaultBlurEffect => Ios13
            ? UIBlurEffectStyle.SystemUltraThinMaterial
            : UIBlurEffectStyle.ExtraLight;

        
        private UIVisualEffectView _DrawerBackgroundVisualEffectView 
            = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.Regular));
        public UIVisualEffectView DrawerBackgroundVisualEffectView
        {
            get => _DrawerBackgroundVisualEffectView;
            set
            {
                _DrawerBackgroundVisualEffectView?.RemoveFromSuperview();

                _DrawerBackgroundVisualEffectView = value;
                if (_DrawerBackgroundVisualEffectView == null || !IsViewLoaded)
                    return;

                _DrawerScrollView.InsertSubviewAbove(_DrawerBackgroundVisualEffectView, _DrawerShadowView);
                value.ClipsToBounds = true;
                value.Layer.CornerRadius = _DrawerCornerRadius;
                View?.SetNeedsLayout();
            }
        }
        
        private nfloat _DrawerTopInset = 325.0f; 
        
        public nfloat DrawerTopInset
        {
            get => _DrawerTopInset;
            set
            {
                if (_DrawerTopInset != value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _DrawerTopInset = value;
            }
        }
        
        private UIEdgeInsets _PanelInsets = new UIEdgeInsets(10, 10, 10, 10); 
        
        public UIEdgeInsets PanelInsets
        {
            get => _PanelInsets;
            set
            {
                if (_PanelInsets != value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _PanelInsets = value;
            }
        }
        
        private nfloat _PanelWidth = 325.0f; 
        
        public nfloat PanelWidth
        {
            get => _PanelWidth;
            set
            {
                if (_PanelWidth != value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _PanelWidth = value;
            }
        }
        
        private UIEdgeInsets _CompactInsets = new UIEdgeInsets(10, 10, 10, 10); 
        
        public UIEdgeInsets CompactInsets
        {
            get => _CompactInsets;
            set
            {
                if (_CompactInsets != value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _CompactInsets = value;
            }
        }
        
        private nfloat _CompactWidth = 325.0f; 
        
        public nfloat CompactWidth
        {
            get => _CompactWidth;
            set
            {
                if (_CompactWidth != value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _CompactWidth = value;
            }
        }
        
        private nfloat _DrawerCornerRadius = 13.0f; 
        
        public nfloat DrawerCornerRadius
        {
            get => _DrawerCornerRadius;
            set
            {
                if (_DrawerCornerRadius != value && IsViewLoaded)
                {
                    View?.SetNeedsLayout();
                    if (DrawerBackgroundVisualEffectView != null)
                        DrawerBackgroundVisualEffectView.Layer.CornerRadius = value;
                }

                _DrawerCornerRadius = value;
            }
        }
        
        private float _ShadowOpacity = 0.1f; 
        
        public float ShadowOpacity
        {
            get => _ShadowOpacity;
            set
            {
                if (Math.Abs(_ShadowOpacity - value) > 0.01 && IsViewLoaded)
                {
                    View?.SetNeedsLayout();
                    _DrawerShadowView.Layer.ShadowOpacity = value;
                }

                _ShadowOpacity = value;
            }
        }
        
        private nfloat _ShadowRadius = 3.0f; 
        
        public nfloat ShadowRadius
        {
            get => _ShadowRadius;
            set
            {
                if (_ShadowRadius != value && IsViewLoaded)
                {
                    View?.SetNeedsLayout();
                    _DrawerShadowView.Layer.ShadowRadius = value;
                }

                _ShadowRadius = value;
            }
        }
        
        private CGSize _ShadowOffset = new CGSize(0, -3); 
        
        public CGSize ShadowOffset
        {
            get => _ShadowOffset;
            set
            {
                if (_ShadowOffset != value && IsViewLoaded)
                {
                    View?.SetNeedsLayout();
                    _DrawerShadowView.Layer.ShadowOffset = value;
                }

                _ShadowOffset = value;
            }
        }

        private UIColor _BackgroundDimmingColor = UIColor.Black;

        public UIColor BackgroundDimmingColor
        {
            get => _BackgroundDimmingColor;
            set
            {
                _BackgroundDimmingColor = value;
                if (!IsViewLoaded)
                    return;
                _BackgroundDimmingView.BackgroundColor = _BackgroundDimmingColor;
            }
        }
        
        private nfloat _BackgroundDimmingOpacity = 0.5f;

        public nfloat BackgroundDimmingOpacity
        {
            get => _BackgroundDimmingOpacity;
            set
            {
                _BackgroundDimmingOpacity = value;
                if (IsViewLoaded)
                    Scrolled(_DrawerScrollView);
            }
        }
        
        
        private bool _DelaysContentTouches = true;

        public bool DelaysContentTouches
        {
            get => _DelaysContentTouches;
            set
            {
                _DelaysContentTouches = value;
                if (IsViewLoaded)
                    _DrawerScrollView.DelaysContentTouches = _DelaysContentTouches;
            }
        }
        
        private bool _CanCancelContentTouches = true;

        public bool CanCancelContentTouches
        {
            get => _CanCancelContentTouches;
            set
            {
                _CanCancelContentTouches = value;
                if (IsViewLoaded)
                    _DrawerScrollView.CanCancelContentTouches = _CanCancelContentTouches;
            }
        }

        public PulleyPosition InitialDrawerPosition { get; set; } = PulleyPosition.Collapsed;
        
        private PulleyDisplayMode _DisplayMode = PulleyDisplayMode.Drawer;

        public PulleyDisplayMode DisplayMode
        {
            get => _DisplayMode;
            set
            {
                if (_DisplayMode !=value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _DisplayMode = value;
            }
        }
        
        private PulleyPanelCornerPlacement _PanelCornerPlacement = PulleyPanelCornerPlacement.TopLeft;

        public PulleyPanelCornerPlacement PanelCornerPlacement
        {
            get => _PanelCornerPlacement;
            set
            {
                if (_PanelCornerPlacement !=value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _PanelCornerPlacement = value;
            }
        }
        
        private PulleyCompactCornerPlacement _CompactCornerPlacement = PulleyCompactCornerPlacement.BottomLeft;

        public PulleyCompactCornerPlacement CompactCornerPlacement
        {
            get => _CompactCornerPlacement;
            set
            {
                if (_CompactCornerPlacement !=value && IsViewLoaded)
                    View?.SetNeedsLayout();
                _CompactCornerPlacement = value;
            }
        }
        
        private bool _AllowsUserDrawerPositionChange = true;

        public bool AllowsUserDrawerPositionChange
        {
            get => _AllowsUserDrawerPositionChange;
            set
            {
                _AllowsUserDrawerPositionChange = value;
                EnforceCanScrollDrawer();
            }
        }
        
        public double AnimationDuration { get; set; } = 0.3;
        
        public double AnimationDelay { get; set; } = 0;

        public nfloat AnimationSpringDamping { get; set; } = 0;

        public nfloat AnimationSpringInitialVelocity { get; set; } = 0;
        
        private bool _AdjustDrawerHorizontalInsetToSafeArea = true;

        public bool AdjustDrawerHorizontalInsetToSafeArea
        {
            get => _AdjustDrawerHorizontalInsetToSafeArea;
            set
            {
                _AdjustDrawerHorizontalInsetToSafeArea = value;
                if (IsViewLoaded)
                    View?.SetNeedsLayout();
            }
        }
        
        public UIViewAnimationOptions AnimationOptions { get; set; } = UIViewAnimationOptions.CurveEaseOut;

        public nfloat Threshold { get; set; } = 20;
        
        public PulleySnapMode SnapMode { get; set; } = PulleySnapMode.NearestPositionUnlessExceeded;

        //UIFeedbackGenerator
        public object FeedbackGenerator { get; set; }
        
        public UIEdgeInsets PulleySafeAreaInsets
        {
            get
            {
                nfloat safeAreaTopInset;
                nfloat safeAreaBottomInset;
                nfloat safeAreaLeftInset = 0;
                nfloat safeAreaRightInset = 0;
        
                if (Ios11 && View != null)
                {
                    safeAreaBottomInset = View.SafeAreaInsets.Bottom;
                    safeAreaLeftInset = View.SafeAreaInsets.Left;
                    safeAreaRightInset = View.SafeAreaInsets.Right;
                    safeAreaTopInset = View.SafeAreaInsets.Top;
                }
                else
                {
                    safeAreaBottomInset = BottomLayoutGuide.Length;
                    safeAreaTopInset = TopLayoutGuide.Length;
                }

                return new UIEdgeInsets(safeAreaTopInset, safeAreaLeftInset, safeAreaBottomInset, safeAreaRightInset);
            }
        }
        
        public (nfloat distance, nfloat bottomSafeArea) DrawerDistanceFromBottom
        {
            get
            {
                if(IsViewLoaded)
                {
                    var lowestStop = GetStopList().Min();
                    return (_DrawerScrollView.ContentOffset.Y + lowestStop, PulleySafeAreaInsets.Bottom);
                }

                return (0, 0);
            }
        }
        
        public PulleyPosition PositionWhenDimmingBackgroundIsTapped  { get; set; } = PulleyPosition.Collapsed;

        public UIGestureRecognizer[] DrawerGestureRecognizers =>
            _DrawerScrollView.GestureRecognizers ?? new UIGestureRecognizer[0];
        
        public UIPanGestureRecognizer DrawerPanGestureRecognizer => _DrawerScrollView.PanGestureRecognizer;

        private PulleyPosition[] _SupportedPositions = PulleyPositionAll;
        
        public PulleyPosition[] SupportedPositions
        {
            get => _SupportedPositions;
            set
            {
                if (!IsViewLoaded)
                    return;

                if (value == null || value.Length == 0)
                {
                    _SupportedPositions = _CurrentDisplayMode == PulleyDisplayMode.Automatic
                        ? PulleyPositionCompact
                        : PulleyPositionAll;
                    return;
                }

                if (!value.SequenceEqual(_SupportedPositions))
                    View?.SetNeedsLayout();
                _SupportedPositions = value;

                if (_SupportedPositions.Contains(_DrawerPosition))
                    SetDrawerPosition(_DrawerPosition, false);
                else if (_CurrentDisplayMode == PulleyDisplayMode.Compact &&
                         _DrawerPosition == PulleyPosition.PartiallyRevealed &&
                         _SupportedPositions.Contains(PulleyPosition.Open))
                    SetDrawerPosition(PulleyPosition.Open, false);
                else
                    SetDrawerPosition((PulleyPosition) _SupportedPositions
                        .Where(p => p != PulleyPosition.Closed).Min(p => (int) p), false);
                
                EnforceCanScrollDrawer();
            }
        }
        
        private PulleyDisplayMode _CurrentDisplayMode = PulleyDisplayMode.Automatic;

        public PulleyDisplayMode CurrentDisplayMode
        {
            get => _CurrentDisplayMode;
            set
            {
                if(value != _CurrentDisplayMode && IsViewLoaded)
                {
                    View?.SetNeedsLayout();
                    SetNeedsSupportedDrawerPositionsUpdate();
                }
                _CurrentDisplayMode = value;

                Delegate?.DrawerDisplayModeDidChange(this);
                if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawer)
                    drawer.DrawerDisplayModeDidChange(this);
                if (_PrimaryContentViewController is IPulleyPrimaryContentControllerDelegate primary)
                    primary.DrawerDisplayModeDidChange(this);
            }
        }

        private bool _IsAnimatingDrawerPosition;
        
        private bool _IsChangingDrawerPosition;

        public nfloat HeightOfOpenDrawer
        {
            get
            {
                if (!IsViewLoaded || View == null)
                    return 0;
                
                var safeAreaTopInset = PulleySafeAreaInsets.Top;
                var safeAreaBottomInset = PulleySafeAreaInsets.Bottom;

                var height = View.Bounds.Height - safeAreaTopInset;
                if(_CurrentDisplayMode == PulleyDisplayMode.Panel)
                {
                    height -= _PanelInsets.Top + BounceOverflowMargin;
                    height -= _PanelInsets.Bottom + safeAreaBottomInset;
                }
                else if (_CurrentDisplayMode == PulleyDisplayMode.Drawer)
                    height -= _DrawerTopInset;

                return height;
            }
        }

        public PulleyViewController(
            UIViewController contentViewController, 
            UIViewController drawerViewController, 
            bool isChangingDrawerPosition = false)
                : base(null, null)
        {
            PrimaryContentViewController = contentViewController;
            DrawerContentViewController = drawerViewController;
            _IsChangingDrawerPosition = isChangingDrawerPosition;
        }
        
        public PulleyViewController(NSCoder coder, bool isChangingDrawerPosition) : base(coder)
        {
            _IsChangingDrawerPosition = isChangingDrawerPosition;
        }

        public override void LoadView()
        {
            base.LoadView();
            
            PrimaryContentContainerView?.RemoveFromSuperview();
            DrawerContentContainerView?.RemoveFromSuperview();

            _PrimaryContentContainer.BackgroundColor = UIColor.White;
            DefinesPresentationContext = true;

            _DrawerScrollView.Bounces = false;
            _DrawerScrollView.Delegate = this;
            _DrawerScrollView.ClipsToBounds = false;
            _DrawerScrollView.ShowsVerticalScrollIndicator = false;
            _DrawerScrollView.ShowsHorizontalScrollIndicator = false;

            _DrawerScrollView.DelaysContentTouches = _DelaysContentTouches;
            _DrawerScrollView.CanCancelContentTouches = _CanCancelContentTouches;

            _DrawerScrollView.BackgroundColor = UIColor.Clear;
            _DrawerScrollView.DecelerationRate = UIScrollView.DecelerationRateFast;
            _DrawerScrollView.ScrollsToTop = false;
            _DrawerScrollView.TouchDelegate = this;

            _DrawerScrollView.Layer.ShadowOpacity = _ShadowOpacity;
            _DrawerScrollView.Layer.ShadowRadius = _ShadowRadius;
            _DrawerScrollView.Layer.ShadowOffset = _ShadowOffset;
            _DrawerScrollView.BackgroundColor = UIColor.Clear;

            _DrawerContentContainer.BackgroundColor = UIColor.Clear;

            _BackgroundDimmingView.BackgroundColor = _BackgroundDimmingColor;
            _BackgroundDimmingView.UserInteractionEnabled = false;
            _BackgroundDimmingView.Alpha = 0f;

            if (_DrawerBackgroundVisualEffectView != null)
                _DrawerBackgroundVisualEffectView.ClipsToBounds = true;
            
            _DimmingViewTapRecognizer = new UITapGestureRecognizer(DimmingViewTapRecognizerAction);
            _BackgroundDimmingView.AddGestureRecognizer(_DimmingViewTapRecognizer);
            
            _DrawerScrollView.AddSubview(_DrawerShadowView);
            
            if(_DrawerBackgroundVisualEffectView != null)
            {
                _DrawerScrollView.AddSubview(_DrawerBackgroundVisualEffectView);
                _DrawerBackgroundVisualEffectView.Layer.CornerRadius = _DrawerCornerRadius;
            }

            _DrawerScrollView.AddSubview(_DrawerContentContainer);

            _PrimaryContentContainer.BackgroundColor = UIColor.White;

            if (View == null)
                return;

            View.BackgroundColor = UIColor.White;
            View.AddSubview(_PrimaryContentContainer);
            View.AddSubview(_BackgroundDimmingView);
            View.AddSubview(_DrawerScrollView);

            _PrimaryContentContainer.ConstrainToParent();
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            
            if(_PrimaryContentViewController == null || _DrawerContentViewController == null)
            {
                if (PrimaryContentContainerView == null || DrawerContentContainerView == null)
                    throw new InvalidOperationException(
                        "When instantiating from Interface Builder you must provide container views with an embedded view controller.");

                var primaryFirst = PrimaryContentContainerView.Subviews.FirstOrDefault();
                var drawerFirst = DrawerContentContainerView.Subviews.FirstOrDefault();
                foreach(var child in ChildViewControllers)
                {
                    if (ReferenceEquals(child.View, primaryFirst))
                        PrimaryContentViewController = child;
                    
                    if (ReferenceEquals(child.View, drawerFirst))
                        DrawerContentViewController = child;
                }
            
                if (PrimaryContentContainerView == null || DrawerContentContainerView == null)
                    throw new InvalidOperationException(
                        "Container views must contain an embedded view controller.");
            }

            EnforceCanScrollDrawer();
            SetDrawerPosition(InitialDrawerPosition, false);
            Scrolled(_DrawerScrollView);
            
            Delegate?.DrawerDisplayModeDidChange(this);
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawer)
                drawer.DrawerDisplayModeDidChange(this);
            if (_PrimaryContentViewController is IPulleyPrimaryContentControllerDelegate primary)
                primary.DrawerDisplayModeDidChange(this);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            SetNeedsSupportedDrawerPositionsUpdate();
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            if (View == null)
                return;

            var primary = _PrimaryContentViewController;
            if(primary?.View?.Superview != null && !ReferenceEquals(primary.View?.Superview, _PrimaryContentContainer))
            {
                _PrimaryContentContainer.AddSubview(primary.View);
                _PrimaryContentContainer.SendSubviewToBack(primary.View);
                primary.View.ConstrainToParent();
            }


            var drawer = _DrawerContentViewController;
            if(drawer?.View?.Superview != null && !ReferenceEquals(drawer.View.Superview, _DrawerContentContainer))
            {
                _DrawerContentContainer.AddSubview(drawer.View);
                _DrawerContentContainer.SendSubviewToBack(drawer.View);
                drawer.View.ConstrainToParent();
            }

            var safeAreaTopInset = PulleySafeAreaInsets.Top;
            var safeAreaBottomInset = PulleySafeAreaInsets.Bottom;
            var safeAreaLeftInset = PulleySafeAreaInsets.Left;
            var safeAreaRightInset = PulleySafeAreaInsets.Right;
            
            var automaticDisplayMode = PulleyDisplayMode.Drawer;
            if (View.Bounds.Width >= 600.0)
            {
                automaticDisplayMode = TraitCollection.HorizontalSizeClass == UIUserInterfaceSizeClass.Compact
                    ? PulleyDisplayMode.Compact
                    : PulleyDisplayMode.Panel;
            }
            
            var displayModeForCurrentLayout = _DisplayMode != PulleyDisplayMode.Automatic ? _DisplayMode : automaticDisplayMode;
            CurrentDisplayMode = displayModeForCurrentLayout;

            nfloat lowestStop;
            if(displayModeForCurrentLayout == PulleyDisplayMode.Drawer)
            {
                if (Ios11)
                    _DrawerScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.ScrollableAxes;
                else
                {
                    AutomaticallyAdjustsScrollViewInsets = false;
                    _DrawerScrollView.ContentInset = new UIEdgeInsets(0, 0, BottomLayoutGuide.Length, 0);
                    _DrawerScrollView.ScrollIndicatorInsets = new UIEdgeInsets(0, 0, BottomLayoutGuide.Length, 0);
                }
                
                var adjustedLeftSafeArea = _AdjustDrawerHorizontalInsetToSafeArea ? safeAreaLeftInset : 0.0;
                var adjustedRightSafeArea = _AdjustDrawerHorizontalInsetToSafeArea ? safeAreaRightInset : 0.0;

                if (_SupportedPositions.Contains(PulleyPosition.Open) && View != null)
                {
                    _DrawerScrollView.Frame = new CGRect(adjustedLeftSafeArea, _DrawerTopInset + safeAreaTopInset,
                        View.Bounds.Width - adjustedLeftSafeArea - adjustedRightSafeArea, HeightOfOpenDrawer);
                }
                else
                {
                    var adjustedTopInset = GetStopList().Max();
                    if (View != null)
                    {
                        _DrawerScrollView.Frame = new CGRect(adjustedLeftSafeArea, View.Bounds.Height - adjustedTopInset,
                            View.Bounds.Width - adjustedLeftSafeArea - adjustedRightSafeArea, adjustedTopInset);
                    }
                }

                _DrawerScrollView.AddSubview(_DrawerShadowView);
                
                if(_DrawerBackgroundVisualEffectView != null)
                {
                    _DrawerScrollView.AddSubview(_DrawerBackgroundVisualEffectView);
                    _DrawerBackgroundVisualEffectView.Layer.CornerRadius = _DrawerCornerRadius;
                }

                _DrawerScrollView.AddSubview(_DrawerContentContainer);

                lowestStop = GetStopList().Min();

                _DrawerContentContainer.Frame = new CGRect(0, _DrawerScrollView.Bounds.Height - lowestStop,
                    _DrawerScrollView.Bounds.Width, _DrawerScrollView.Bounds.Height + BounceOverflowMargin);
                if (_DrawerBackgroundVisualEffectView != null)
                    _DrawerBackgroundVisualEffectView.Frame = _DrawerContentContainer.Frame;
                _DrawerShadowView.Frame = _DrawerContentContainer.Frame;
                _DrawerScrollView.ContentSize = new CGSize(_DrawerScrollView.Bounds.Width,
                    _DrawerScrollView.Bounds.Height - lowestStop + _DrawerScrollView.Bounds.Height -
                    safeAreaBottomInset + (BounceOverflowMargin - 5.0));
                
                // Update rounding Mask and shadows
                var borderPath = DrawerMaskingPath(UIRectCorner.AllCorners).CGPath;

                var cardMaskLayer = new CAShapeLayer
                {
                    Path = borderPath,
                    Frame = _DrawerContentContainer.Bounds,
                    FillColor = UIColor.White.CGColor,
                    BackgroundColor = UIColor.Clear.CGColor
                };
                _DrawerContentContainer.Layer.Mask = cardMaskLayer;
                _DrawerShadowView.Layer.ShadowPath = borderPath;

                if (View != null)
                {
                    _BackgroundDimmingView.Frame = new CGRect(0.0, 0.0,
                        View.Bounds.Width, View.Bounds.Height + _DrawerScrollView.ContentSize.Height);
                }

                _DrawerScrollView.Transform = CGAffineTransform.MakeIdentity();
                _BackgroundDimmingView.Hidden = false;
            }
            else
            {
                if (Ios11)
                    _DrawerScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.ScrollableAxes;
                else
                {
                    AutomaticallyAdjustsScrollViewInsets = false;
                    _DrawerScrollView.ContentInset = new UIEdgeInsets(0, 0, 0, 0);
                    _DrawerScrollView.ScrollIndicatorInsets = new UIEdgeInsets(0, 0, 0, 0);
                }

                var collapsedHeight = PulleyDefaultCollapsedHeight;
                var partialRevealHeight = PulleyDefaultPartialRevealHeight;
                
                if(_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
                {
                    collapsedHeight = drawerVCCompliant.CollapsedDrawerHeight(safeAreaBottomInset) ?? PulleyDefaultCollapsedHeight;
                    partialRevealHeight = drawerVCCompliant.PartialRevealDrawerHeight(safeAreaBottomInset) ?? PulleyDefaultPartialRevealHeight;
                }
                
                nfloat xOrigin;
                nfloat yOrigin;
                
                if (displayModeForCurrentLayout == PulleyDisplayMode.Compact)
                {
                    lowestStop = new[]
                    {
                        View.Bounds.Size.Height - _CompactInsets.Bottom - safeAreaTopInset, 
                        collapsedHeight,
                        partialRevealHeight
                    }.Min();
                    xOrigin = _CompactCornerPlacement == PulleyCompactCornerPlacement.BottomLeft
                        ? safeAreaLeftInset + _CompactInsets.Left
                        : View.Bounds.GetMaxX() - (safeAreaRightInset + _CompactInsets.Right) - _CompactWidth;
                    yOrigin = _CompactInsets.Top + safeAreaTopInset;
                }
                else
                {
                    lowestStop = new[]
                    {
                        View.Bounds.Size.Height - _PanelInsets.Bottom - safeAreaTopInset, 
                        collapsedHeight,
                        partialRevealHeight
                    }.Min();
                    xOrigin = _PanelCornerPlacement == PulleyPanelCornerPlacement.BottomLeft || _PanelCornerPlacement == PulleyPanelCornerPlacement.TopLeft 
                        ? safeAreaLeftInset + _PanelInsets.Left
                        : View.Bounds.GetMaxX() - (safeAreaRightInset + _PanelInsets.Right) - _PanelWidth;
                    yOrigin = _PanelCornerPlacement == PulleyPanelCornerPlacement.BottomLeft || _PanelCornerPlacement == PulleyPanelCornerPlacement.BottomRight 
                        ? _PanelInsets.Top + safeAreaTopInset
                        : _PanelInsets.Top + safeAreaTopInset + BounceOverflowMargin;
                }
                
                var width = displayModeForCurrentLayout == PulleyDisplayMode.Compact ? _CompactWidth : _PanelWidth;
                if(_SupportedPositions.Contains(PulleyPosition.Open))
                    _DrawerScrollView.Frame = new CGRect(xOrigin, yOrigin, width, HeightOfOpenDrawer);
                else
                    _DrawerScrollView.Frame = new CGRect(xOrigin, yOrigin, width, 
                        _SupportedPositions.Contains(PulleyPosition.PartiallyRevealed) ? partialRevealHeight : collapsedHeight);

                SyncDrawerContentViewSizeToMatchScrollPositionForSideDisplayMode();

                if(View != null)
                        _DrawerScrollView.ContentSize = new CGSize(_DrawerScrollView.Bounds.Width,
                    View.Bounds.Height + (View.Bounds.Height - lowestStop));
                
                if (displayModeForCurrentLayout == PulleyDisplayMode.Compact)
                {
                    if (_CompactCornerPlacement == PulleyCompactCornerPlacement.BottomLeft ||
                            _CompactCornerPlacement == PulleyCompactCornerPlacement.BottomRight)
                        _DrawerScrollView.Transform = CGAffineTransform.MakeScale(1, 1);
                }
                else
                {
                    if (_PanelCornerPlacement == PulleyPanelCornerPlacement.TopLeft ||
                            _PanelCornerPlacement == PulleyPanelCornerPlacement.TopRight)
                        _DrawerScrollView.Transform = CGAffineTransform.MakeScale(1, -1);
                    
                    if (_PanelCornerPlacement == PulleyPanelCornerPlacement.BottomLeft ||
                        _PanelCornerPlacement == PulleyPanelCornerPlacement.BottomRight)
                        _DrawerScrollView.Transform = CGAffineTransform.MakeScale(1, 1);
                }

                _BackgroundDimmingView.Hidden = true;
            }

            _DrawerContentContainer.Transform = _DrawerScrollView.Transform;
            _DrawerShadowView.Transform = _DrawerScrollView.Transform;
            _DrawerBackgroundVisualEffectView.Transform = _DrawerScrollView.Transform;

            lowestStop = GetStopList().Min();
            
            Delegate?.DrawerChangedDistanceFromBottom(this, _DrawerScrollView.ContentOffset.Y + lowestStop, PulleySafeAreaInsets.Bottom);
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerDel)
                drawerDel.DrawerChangedDistanceFromBottom(this, _DrawerScrollView.ContentOffset.Y + lowestStop, PulleySafeAreaInsets.Bottom);
            if (_PrimaryContentViewController is IPulleyPrimaryContentControllerDelegate primaryDel)
                primaryDel.DrawerChangedDistanceFromBottom(this, _DrawerScrollView.ContentOffset.Y + lowestStop, PulleySafeAreaInsets.Bottom);

            MaskDrawerVisualEffectView();
            MaskBackgroundDimmingView();
            
            if (!_IsChangingDrawerPosition)
                SetDrawerPosition(_DrawerPosition, false);
        }
        
        private void EnforceCanScrollDrawer()
        {
            if (!IsViewLoaded)
                return;
            _DrawerScrollView.ScrollEnabled = _AllowsUserDrawerPositionChange && _SupportedPositions.Length > 1;
        }

        private nfloat[] GetStopList()
        {
            var drawerStops = new List<nfloat>();

            var collapsedHeight = PulleyDefaultCollapsedHeight;
            var partialRevealHeight = PulleyDefaultPartialRevealHeight;

            if(_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
            {
                collapsedHeight = drawerVCCompliant.CollapsedDrawerHeight(
                PulleySafeAreaInsets.Bottom) ?? PulleyDefaultCollapsedHeight;
                partialRevealHeight = drawerVCCompliant.PartialRevealDrawerHeight(
                PulleySafeAreaInsets.Bottom) ?? PulleyDefaultPartialRevealHeight;
            }

            if (_SupportedPositions.Contains(PulleyPosition.Collapsed))
                drawerStops.Add(collapsedHeight);

            if (_SupportedPositions.Contains(PulleyPosition.PartiallyRevealed))
                drawerStops.Add(partialRevealHeight);

            if (_SupportedPositions.Contains(PulleyPosition.Open) && View != null)
                drawerStops.Add(View.Bounds.Size.Height - _DrawerTopInset - PulleySafeAreaInsets.Top);

            return drawerStops.ToArray();
        }

        private UIBezierPath DrawerMaskingPath(UIRectCorner corners)
        {
            if (_DrawerContentViewController.IsViewLoaded)
                _DrawerContentViewController.View?.SetNeedsLayout();

            UIBezierPath path;
            var customPath = (_DrawerContentViewController.View?.Layer.Mask as CAShapeLayer)?.Path;
            if (customPath != null)
                path = UIBezierPath.FromPath(customPath);
            else
            {
                path = UIBezierPath.FromRoundedRect(_DrawerContentContainer.Bounds, corners,
                    new CGSize(_DrawerCornerRadius, _DrawerCornerRadius));
            }

            return path;
        }
        
        private void MaskDrawerVisualEffectView()
        {
            if (_DrawerBackgroundVisualEffectView == null)
                return;

            _DrawerBackgroundVisualEffectView.Layer.Mask = new CAShapeLayer
                {Path = DrawerMaskingPath(UIRectCorner.TopLeft | UIRectCorner.TopRight).CGPath};
        }
        
        private void MaskBackgroundDimmingView()
        {
            var cutoutHeight = 2 * _DrawerCornerRadius;
            var maskHeight = _BackgroundDimmingView.Bounds.Size.Height - cutoutHeight - _DrawerScrollView.ContentSize.Height;
            var borderPath = DrawerMaskingPath(UIRectCorner.TopLeft | UIRectCorner.TopRight);

            var frame = _DrawerContentContainer.Superview?.ConvertRectToView(_DrawerContentContainer.Frame, View);

            borderPath.ApplyTransform(frame != null
                ? CGAffineTransform.MakeTranslation(frame.Value.GetMinX(), maskHeight)
                : CGAffineTransform.MakeTranslation(0, maskHeight));

            var maskLayer = new CAShapeLayer();
            borderPath.AppendPath(UIBezierPath.FromRect(_BackgroundDimmingView.Bounds));
            maskLayer.FillRule = new NSString("evenOdd");

            maskLayer.Path = borderPath.CGPath;
            _BackgroundDimmingView.Layer.Mask = maskLayer;
        }
        
        public void PrepareFeedbackGenerator()
        {
            if (Ios10 && FeedbackGenerator is UIFeedbackGenerator generator)
                generator.Prepare();
        }
        
        public void TriggerFeedbackGenerator()
        {
            if (!Ios10)
                return;

            PrepareFeedbackGenerator();

            (FeedbackGenerator as UIImpactFeedbackGenerator)?.ImpactOccurred();
            (FeedbackGenerator as UISelectionFeedbackGenerator)?.SelectionChanged();
            (FeedbackGenerator as UINotificationFeedbackGenerator)?.NotificationOccurred(
                UINotificationFeedbackType.Success);
        }

        public void AddDrawerGestureRecognizer(UIGestureRecognizer gestureRecognizer)
        {
            _DrawerScrollView.AddGestureRecognizer(gestureRecognizer);
        }
        
        public void RemoveDrawerGestureRecognizer(UIGestureRecognizer gestureRecognizer)
        {
            _DrawerScrollView.RemoveGestureRecognizer(gestureRecognizer);
        }
        
        public void BounceDrawer(float bounceHeight = 50.0f, double speedMultiplier = 0.75)
        {
            if(_DrawerPosition != PulleyPosition.Collapsed && _DrawerPosition != PulleyPosition.PartiallyRevealed)
            {
                Console.WriteLine("Pulley: Error: You can only bounce the drawer when it's in the collapsed or partially revealed position.");
                return;
            }
            
            if(_CurrentDisplayMode != PulleyDisplayMode.Drawer)
            {
                Console.WriteLine("Pulley: Error: You can only bounce the drawer when it's in the .drawer display mode.");
                return;
            }

            var factors = new nfloat[] {0, 32, 60, 83, 100, 114, 124, 128, 128, 124, 114, 100, 83, 60, 32,
                0, 24, 42, 54, 62, 64, 62, 54, 42, 24, 0, 18, 28, 32, 28, 18, 0};

            var values = new List<NSObject>();
            foreach (var factor in factors)
            {
                var positionOffset = (nfloat) (factor / 128.0) * bounceHeight;
                values.Add(NSNumber.FromNFloat(_DrawerScrollView.Frame.Y + positionOffset));
            }
            
            var animation = CAKeyFrameAnimation.FromKeyPath("Bounds.origin.y");
            animation.RepeatCount = 1;
            animation.Duration = (32.0 / 30.0) * speedMultiplier;
            animation.FillMode = "forwards";
            animation.Values = values.ToArray();
            animation.RemovedOnCompletion = true;
            animation.AutoReverses = false;

            _DrawerScrollView.Layer.AddAnimation(animation, "bounceAnimation");
        }

        private CGRect BackgroundDimmingViewFrameForDrawerPosition(nfloat drawerPosition)
        {
            var cutoutHeight = 2 * _DrawerCornerRadius;
            var backgroundDimmingViewFrame = _BackgroundDimmingView.Frame;
            backgroundDimmingViewFrame.Y = 0 - drawerPosition + cutoutHeight;

            return backgroundDimmingViewFrame;
        }
        
        private void SyncDrawerContentViewSizeToMatchScrollPositionForSideDisplayMode()
        {
            if (_CurrentDisplayMode != PulleyDisplayMode.Panel && _CurrentDisplayMode != PulleyDisplayMode.Compact)
                return;

            var lowestStop = GetStopList().Min();

            _DrawerContentContainer.Frame = new CGRect(0.0, _DrawerScrollView.Bounds.Height - lowestStop,
                _DrawerScrollView.Bounds.Width, _DrawerScrollView.ContentOffset.Y + lowestStop + BounceOverflowMargin);
            if (_DrawerBackgroundVisualEffectView != null)
                _DrawerBackgroundVisualEffectView.Frame = _DrawerContentContainer.Frame;
            _DrawerShadowView.Frame = _DrawerContentContainer.Frame;
            
            var borderPath = DrawerMaskingPath(UIRectCorner.AllCorners).CGPath;

            var cardMaskLayer = new CAShapeLayer
            {
                Path = borderPath,
                Frame = _DrawerContentContainer.Bounds,
                FillColor = UIColor.White.CGColor,
                BackgroundColor = UIColor.Clear.CGColor
            };
            _DrawerContentContainer.Layer.Mask = cardMaskLayer;

            MaskDrawerVisualEffectView();
            
            if(!_IsAnimatingDrawerPosition || borderPath?.BoundingBox.Height < _DrawerShadowView.Layer.ShadowPath?.BoundingBox.Height)
                _DrawerShadowView.Layer.ShadowPath = borderPath;
        }
        
        public void SetDrawerPosition(PulleyPosition position, bool animated = true, Action<bool> completion = null)
        {
            if (!_SupportedPositions.Contains(position))
            {
                Console.WriteLine(
                    "PulleyViewController: You can't set the drawer position to something not supported by the current view controller contained in the drawer. If you haven't already, you may need to implement the PulleyDrawerViewControllerDelegate.");
                return;
            }
            _DrawerPosition = position;

            var collapsedHeight = PulleyDefaultCollapsedHeight;
            var partialRevealHeight = PulleyDefaultPartialRevealHeight;
 
            if(_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
            {
                collapsedHeight = drawerVCCompliant.CollapsedDrawerHeight(
                    PulleySafeAreaInsets.Bottom) ?? PulleyDefaultCollapsedHeight;
                partialRevealHeight = drawerVCCompliant.PartialRevealDrawerHeight(
                    PulleySafeAreaInsets.Bottom) ?? PulleyDefaultPartialRevealHeight;
            }

            nfloat stopToMoveTo;
            
            switch(_DrawerPosition)
            {
                case PulleyPosition.Collapsed:
                    stopToMoveTo = collapsedHeight;
                    break;
                case PulleyPosition.PartiallyRevealed:
                    stopToMoveTo = partialRevealHeight;
                    break;
                case PulleyPosition.Open:
                    stopToMoveTo = HeightOfOpenDrawer;
                    break;
                case PulleyPosition.Closed:
                    stopToMoveTo = 0;
                    break;
                default:
                    stopToMoveTo = 0;
                    break;
            }

            var lowestStop = GetStopList().Min();
            TriggerFeedbackGenerator();
            var @delegate = Delegate;
            
            if(animated && View?.Window != null)
            {
                _IsAnimatingDrawerPosition = true;
                UIView.AnimateNotify(AnimationDuration, AnimationDelay, AnimationSpringDamping, AnimationSpringInitialVelocity, AnimationOptions, () =>
                {
                    _DrawerScrollView.SetContentOffset(new CGPoint(0, stopToMoveTo - lowestStop), false);
                    
                    _BackgroundDimmingView.Frame = BackgroundDimmingViewFrameForDrawerPosition(stopToMoveTo);

                    @delegate?.DrawerPositionDidChange(this, PulleySafeAreaInsets.Bottom);
                    (_DrawerContentViewController as IPulleyDrawerViewControllerDelegate)?.DrawerPositionDidChange(
                        this, PulleySafeAreaInsets.Bottom);
                    (_PrimaryContentViewController as IPulleyPrimaryContentControllerDelegate)?.DrawerPositionDidChange(
                        this, PulleySafeAreaInsets.Bottom);
                    
                    if (IsPhone)
                        View?.LayoutIfNeeded();


                }, completed =>
                {
                    _IsAnimatingDrawerPosition = false;
                    SyncDrawerContentViewSizeToMatchScrollPositionForSideDisplayMode();

                    completion?.Invoke(completed);
                });
            }
            else
            {
                _DrawerScrollView.SetContentOffset(new CGPoint(0, stopToMoveTo - lowestStop), false);

                _BackgroundDimmingView.Frame = BackgroundDimmingViewFrameForDrawerPosition(stopToMoveTo);
                
                @delegate?.DrawerPositionDidChange(this, PulleySafeAreaInsets.Bottom);
                (_DrawerContentViewController as IPulleyDrawerViewControllerDelegate)?.DrawerPositionDidChange(
                    this, PulleySafeAreaInsets.Bottom);
                (_PrimaryContentViewController as IPulleyPrimaryContentControllerDelegate)?.DrawerPositionDidChange(
                    this, PulleySafeAreaInsets.Bottom);

                completion?.Invoke(true);
            }
        }
        
        public void SetPrimaryContentViewController(UIViewController controller, bool animated = true, Action<bool> completion = null)
        {
            if (controller?.View == null)
                return;

            controller.View.Frame = _PrimaryContentContainer.Bounds;
            controller.View.LayoutIfNeeded();
            
            if(animated)
            {
                UIView.TransitionNotify(_PrimaryContentContainer, 0.5, UIViewAnimationOptions.TransitionCrossDissolve, () =>
                {
                    PrimaryContentViewController = controller;
                }, completed =>
                {
                    completion?.Invoke(completed);
                });
            }
            else
            {
                PrimaryContentViewController = controller;
                completion?.Invoke(true);
            }
        }
        
        public void SetDrawerContentViewController(
            UIViewController controller, PulleyPosition? position = null, bool animated = true, Action<bool> completion = null)
        {
            if (controller?.View == null)
                return;

            controller.View.Frame = _DrawerContentContainer.Bounds;
            controller.View.LayoutIfNeeded();
            
            if(animated)
            {
                UIView.TransitionNotify(_DrawerContentContainer, 0.5, UIViewAnimationOptions.TransitionCrossDissolve, () =>
                {
                    DrawerContentViewController = controller;
                    SetDrawerPosition(position ?? _DrawerPosition, false);

                }, completed =>
                {
                    completion?.Invoke(completed);
                });
            }
            else
            {
                DrawerContentViewController = controller;
                completion?.Invoke(true);
            }
        }
        
        public void SetDrawerContentViewController(UIViewController controller, bool animated = true, Action<bool> completion = null)
        {
            SetDrawerContentViewController(controller, null, animated, completion);
        }
        
        public void SetNeedsSupportedDrawerPositionsUpdate()
        {
            if(_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
            {
                var positions = drawerVCCompliant.SupportedDrawerPositions();
                if (positions?.Length > 0)
                {
                    _SupportedPositions = _CurrentDisplayMode == PulleyDisplayMode.Compact
                        ? positions.Where(p => PulleyPositionCompact.Contains(p)).ToArray()
                        : positions;
                    return;
                }
            }

            _SupportedPositions = _CurrentDisplayMode == PulleyDisplayMode.Compact
                ? PulleyPositionCompact
                : PulleyPositionAll;
        }
        
        private void DimmingViewTapRecognizerAction(UITapGestureRecognizer gestureRecognizer)
        {
            if (gestureRecognizer == null || !ReferenceEquals(gestureRecognizer, _DimmingViewTapRecognizer))
                return;

            if (gestureRecognizer.State == UIGestureRecognizerState.Ended)
                SetDrawerPosition(PositionWhenDimmingBackgroundIsTapped);
        }

        public override UIViewController ChildViewControllerForStatusBarStyle()
        {
            return _DrawerPosition == PulleyPosition.Open
                ? _DrawerContentViewController
                : _PrimaryContentViewController;
        }

        public override UIViewController ChildViewControllerForStatusBarHidden()
        {
            return _DrawerPosition == PulleyPosition.Open
                ? _DrawerContentViewController
                : _PrimaryContentViewController;
        }

        public override void ViewWillTransitionToSize(CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
        {
            base.ViewWillTransitionToSize(toSize, coordinator);

            if (Ios10)
                coordinator.NotifyWhenInteractionChanges(context => { SetDrawerPosition(_DrawerPosition, false); });
            else
                coordinator.NotifyWhenInteractionEndsUsingBlock(context => { SetDrawerPosition(_DrawerPosition, false); });
        }
        
        public nfloat? CollapsedDrawerHeight(nfloat bottomSafeArea)
        {
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
                return drawerVCCompliant.CollapsedDrawerHeight(bottomSafeArea);
            return (nfloat?) (68.0 + bottomSafeArea);
        }
        
        public nfloat? PartialRevealDrawerHeight(nfloat bottomSafeArea)
        {
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
                return drawerVCCompliant.PartialRevealDrawerHeight(bottomSafeArea);
            return (nfloat?) (264.0 + bottomSafeArea);
        }
        
        public PulleyPosition[] SupportedDrawerPositions()
        {
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
            {
                var supportedPositions = drawerVCCompliant.SupportedDrawerPositions();
                return _CurrentDisplayMode == PulleyDisplayMode.Compact
                    ? supportedPositions.Where(p => PulleyPositionCompact.Contains(p)).ToArray()
                    : supportedPositions;
            }

            return _CurrentDisplayMode == PulleyDisplayMode.Compact ? PulleyPositionCompact : PulleyPositionAll;
        }
        
        public void DrawerPositionDidChange(PulleyViewController drawer, nfloat bottomSafeArea)
        {
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
                drawerVCCompliant.DrawerPositionDidChange(drawer, bottomSafeArea);
        }
        
        public void MakeUIAdjustmentsForFullscreen(nfloat progress, nfloat bottomSafeArea)
        {
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
                drawerVCCompliant.MakeUIAdjustmentsForFullscreen(progress, bottomSafeArea);
        }
        
        public void DrawerChangedDistanceFromBottom(PulleyViewController drawer, nfloat distance, nfloat bottomSafeArea)
        {
            if (_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
                drawerVCCompliant.DrawerChangedDistanceFromBottom(drawer, distance, bottomSafeArea);
        }

        public void DrawerDisplayModeDidChange(PulleyViewController drawer)
        {
        }

        public bool ShouldTouchPassthroughScrollView(PulleyPassthroughScrollView scrollView, CGPoint point)
        {
            return !_DrawerContentContainer.Bounds.Contains(
                _DrawerContentContainer.ConvertPointFromView(point, scrollView));
        }
        
        public UIView ViewToReceiveTouch(PulleyPassthroughScrollView scrollView, CGPoint point)
        {
            if(_CurrentDisplayMode == PulleyDisplayMode.Drawer)
                return _DrawerPosition == PulleyPosition.Open ? _BackgroundDimmingView : _PrimaryContentContainer;

            return _DrawerContentContainer.Bounds.Contains(_DrawerContentContainer.ConvertPointFromView(point, scrollView)) 
                ? _DrawerContentViewController.View 
                : _PrimaryContentContainer;
        }
        
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public virtual void DraggingEnded(UIScrollView scrollView, bool willDecelerate)
        {
            if (!ReferenceEquals(scrollView, _DrawerScrollView))
                return;

            var collapsedHeight = PulleyDefaultCollapsedHeight;
            var partialRevealHeight = PulleyDefaultPartialRevealHeight;
            
            if(_DrawerContentViewController is IPulleyDrawerViewControllerDelegate drawerVCCompliant)
            {
                collapsedHeight = drawerVCCompliant.CollapsedDrawerHeight(
                    PulleySafeAreaInsets.Bottom) ?? PulleyDefaultCollapsedHeight;
                partialRevealHeight = drawerVCCompliant.PartialRevealDrawerHeight(
                    PulleySafeAreaInsets.Bottom) ?? PulleyDefaultPartialRevealHeight;
            }

            var drawerStops = new List<nfloat>();
            var currentDrawerPositionStop = (nfloat) 0f;
            
            if(_SupportedPositions.Contains(PulleyPosition.Open))
            {
                drawerStops.Add(HeightOfOpenDrawer);

                if (_DrawerPosition == PulleyPosition.Open)
                    currentDrawerPositionStop = drawerStops.LastOrDefault();
            }
            
            if(_SupportedPositions.Contains(PulleyPosition.PartiallyRevealed))
            {
                drawerStops.Add(partialRevealHeight);
                if (_DrawerPosition == PulleyPosition.PartiallyRevealed)
                    currentDrawerPositionStop = drawerStops.LastOrDefault();
            }
            
            if(_SupportedPositions.Contains(PulleyPosition.Collapsed))
            {
                drawerStops.Add(collapsedHeight);

                if (_DrawerPosition == PulleyPosition.Collapsed)
                    currentDrawerPositionStop = drawerStops.LastOrDefault();
            }

            var lowestStop = drawerStops.Min();
            var distanceFromBottomOfView = lowestStop + _LastDragTargetContentOffset.Y;

            var currentClosestStop = lowestStop;
            
            foreach(var currentStop in drawerStops)
            {
                if (Math.Abs(currentStop - distanceFromBottomOfView) <
                        Math.Abs(currentClosestStop - distanceFromBottomOfView))
                    currentClosestStop = currentStop;
            }

            var closestValidDrawerPosition = _DrawerPosition;
            
            if(Math.Abs(currentClosestStop - HeightOfOpenDrawer) <= float.Epsilon && _SupportedPositions.Contains(PulleyPosition.Open))
                closestValidDrawerPosition = PulleyPosition.Open;
            else if(Math.Abs(currentClosestStop - collapsedHeight) <= float.Epsilon && _SupportedPositions.Contains(PulleyPosition.Collapsed))
                closestValidDrawerPosition = PulleyPosition.Collapsed;
            else if (_SupportedPositions.Contains(PulleyPosition.PartiallyRevealed))
                closestValidDrawerPosition = PulleyPosition.PartiallyRevealed;
            
            var snapModeToUse = closestValidDrawerPosition == _DrawerPosition ? SnapMode : PulleySnapMode.NearestPosition;
            
            switch (snapModeToUse) 
            {
                case PulleySnapMode.NearestPosition:
                    SetDrawerPosition(closestValidDrawerPosition);
                    break;
                case PulleySnapMode.NearestPositionUnlessExceeded:
                    var distance = currentDrawerPositionStop - distanceFromBottomOfView;
                    var positionToSnapTo = _DrawerPosition;
                    if(Math.Abs(distance) > Threshold)
                    {
                        var positions = distance < 0 
                                ? _SupportedPositions.OrderBy(p => (int) p) 
                                :_SupportedPositions.OrderByDescending(p => (int) p); 
                            
                        foreach(var position in positions.Where(p => p != PulleyPosition.Closed))
                        {
                            if((int) position > (int) _DrawerPosition)
                            {
                                positionToSnapTo = position;
                                break;
                            }
                        }
                    }

                    SetDrawerPosition(positionToSnapTo);
                    break;
            }
        }
        
        public void DraggingStarted(UIScrollView scrollView)
        {
            if(ReferenceEquals(scrollView, _DrawerScrollView))
                _IsChangingDrawerPosition = true;
        }
        
        public virtual void WillEndDragging(UIScrollView scrollView, CGPoint velocity, ref CGPoint targetContentOffset)
        {
            PrepareFeedbackGenerator();

            if (!ReferenceEquals(scrollView, _DrawerScrollView))
                return;

            _LastDragTargetContentOffset = targetContentOffset;
            targetContentOffset = scrollView.ContentOffset;
            _IsChangingDrawerPosition = false;
        }
        
        public virtual void Scrolled(UIScrollView scrollView)
        {
            if (!ReferenceEquals(scrollView, _DrawerScrollView))
                return;

            var partialRevealHeight = (_DrawerContentViewController as IPulleyDrawerViewControllerDelegate)
                ?.PartialRevealDrawerHeight(PulleySafeAreaInsets.Bottom) ?? PulleyDefaultPartialRevealHeight;

            var lowestStop = GetStopList().Min();

            var drawer = _DrawerContentViewController as IPulleyDrawerViewControllerDelegate;
            var primary = _PrimaryContentViewController as IPulleyPrimaryContentControllerDelegate;

            if (scrollView.ContentOffset.Y - PulleySafeAreaInsets.Bottom > partialRevealHeight - lowestStop 
                && _SupportedPositions.Contains(PulleyPosition.Open))
            {
                var fullRevealHeight = HeightOfOpenDrawer;
                var progress = fullRevealHeight == partialRevealHeight
                    ? 1.0
                    : (scrollView.ContentOffset.Y - (partialRevealHeight - lowestStop)) /
                      (fullRevealHeight - partialRevealHeight);
                
                Delegate?.MakeUIAdjustmentsForFullscreen((nfloat) progress, PulleySafeAreaInsets.Bottom);
                drawer?.MakeUIAdjustmentsForFullscreen((nfloat) progress, PulleySafeAreaInsets.Bottom);
                primary?.MakeUIAdjustmentsForFullscreen((nfloat) progress, PulleySafeAreaInsets.Bottom);

                _BackgroundDimmingView.Alpha = (nfloat) progress * _BackgroundDimmingOpacity;
                _BackgroundDimmingView.UserInteractionEnabled = true;
            }
            else
            {
                if(_BackgroundDimmingView.Alpha >= 0.001)
                {
                    _BackgroundDimmingView.Alpha = 0.0f;
                
                    Delegate?.MakeUIAdjustmentsForFullscreen(0, PulleySafeAreaInsets.Bottom);
                    drawer?.MakeUIAdjustmentsForFullscreen(0, PulleySafeAreaInsets.Bottom);
                    primary?.MakeUIAdjustmentsForFullscreen(0, PulleySafeAreaInsets.Bottom);

                    _BackgroundDimmingView.UserInteractionEnabled = false;
                }
            }
            
            Delegate?.DrawerChangedDistanceFromBottom(this, scrollView.ContentOffset.Y + lowestStop, PulleySafeAreaInsets.Bottom);
            drawer?.DrawerChangedDistanceFromBottom(this, scrollView.ContentOffset.Y + lowestStop, PulleySafeAreaInsets.Bottom);
            primary?.DrawerChangedDistanceFromBottom(this, scrollView.ContentOffset.Y + lowestStop, PulleySafeAreaInsets.Bottom);

            _BackgroundDimmingView.Frame =
                BackgroundDimmingViewFrameForDrawerPosition(scrollView.ContentOffset.Y + lowestStop);

            SyncDrawerContentViewSizeToMatchScrollPositionForSideDisplayMode();
        }
    }
}