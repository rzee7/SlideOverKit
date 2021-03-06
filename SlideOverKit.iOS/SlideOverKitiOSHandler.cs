﻿using System;
using Xamarin.Forms.Platform.iOS;
using UIKit;
using CoreGraphics;
using Xamarin.Forms;

namespace SlideOverKit.iOS
{
    public class SlideOverKitiOSHandler
    {
        PageRenderer _pageRenderer;
        ISlideOverKitPageRendereriOS _menuKit;

        IMenuContainerPage _basePage;
        IVisualElementRenderer _menuOverlayRenderer;
        UIPanGestureRecognizer _panGesture;
        IDragGesture _dragGesture;


        public SlideOverKitiOSHandler ()
        {
        }

        public void Init (ISlideOverKitPageRendereriOS menuKit)
        {
            _menuKit = menuKit;
            _pageRenderer = menuKit as PageRenderer;

            _menuKit.ViewDidAppearEvent = ViewDidAppear; 
            _menuKit.OnElementChangedEvent = OnElementChanged;
            _menuKit.ViewDidLayoutSubviewsEvent = ViewDidLayoutSubviews;
            _menuKit.ViewDidDisappearEvent = ViewDidDisappear;
            _menuKit.ViewWillTransitionToSizeEvent = ViewWillTransitionToSize;
            if (ScreenSizeHelper.ScreenHeight == 0 && ScreenSizeHelper.ScreenWidth == 0) {
                ScreenSizeHelper.ScreenHeight = UIScreen.MainScreen.Bounds.Height;
                ScreenSizeHelper.ScreenWidth = UIScreen.MainScreen.Bounds.Width;
            }
        }

        bool CheckPageAndMenu ()
        {
            if (_basePage != null && _basePage.SlideMenu != null)
                return true;
            else
                return false;
        }

        UIView _backgroundOverlay;

        void HideBackgroundOverlay ()
        {
            if (_backgroundOverlay != null) {
                _backgroundOverlay.RemoveFromSuperview ();
                _backgroundOverlay.Dispose ();
                _backgroundOverlay = null;
            }
        }

        void ShowBackgroundOverlay (double alpha)
        {
            if (!CheckPageAndMenu ())
                return;
            nfloat value = (nfloat)(alpha * _basePage.SlideMenu.BackgroundViewColor.A);
            if (_backgroundOverlay != null) {
                _backgroundOverlay.BackgroundColor = _basePage.SlideMenu.BackgroundViewColor.ToUIColor ().ColorWithAlpha (value);
                return;
            }
            _backgroundOverlay = new UIView ();		
            _backgroundOverlay.BackgroundColor = _basePage.SlideMenu.BackgroundViewColor.ToUIColor ().ColorWithAlpha (value);

            _backgroundOverlay.AddGestureRecognizer (new UITapGestureRecognizer (() => {
                this._basePage.HideMenuAction ();
            }));

            if (_basePage.SlideMenu.IsFullScreen) {
                _backgroundOverlay.Frame = new CGRect (UIApplication.SharedApplication.KeyWindow.Frame.Location, UIApplication.SharedApplication.KeyWindow.Frame.Size);
                UIApplication.SharedApplication.KeyWindow.InsertSubviewBelow (_backgroundOverlay, _menuOverlayRenderer.NativeView);
            } else {
                _backgroundOverlay.Frame = new CGRect (_pageRenderer.View.Frame.Location, _pageRenderer.View.Frame.Size);
                _pageRenderer.View.InsertSubviewBelow (_backgroundOverlay, _menuOverlayRenderer.NativeView);
            }
        }

        void LayoutMenu ()
        {
            if (!CheckPageAndMenu ())
                return;
            
            // areadly add gesture
            if (_dragGesture != null)
                return; 
                    
            var menu = _basePage.SlideMenu;

            _dragGesture = DragGestureFactory.GetGestureByView (menu);
            _dragGesture.RequestLayout = (l, t, r, b, density) => {
                _menuOverlayRenderer.NativeView.Frame = new CGRect (l, t, (r - l), (b - t));
                _menuOverlayRenderer.NativeView.SetNeedsLayout ();
            };
            _dragGesture.NeedShowBackgroundView = (open, alpha) => {
                UIView.CommitAnimations ();
                if (open)
                    ShowBackgroundOverlay (alpha);
                else
                    HideBackgroundOverlay ();
            };

            _basePage.HideMenuAction = () => {
                UIView.BeginAnimations ("OpenAnimation");
                UIView.SetAnimationDuration (((double)menu.AnimationDurationMillisecond) / 1000);
                _dragGesture.LayoutHideStatus ();

            };

            _basePage.ShowMenuAction = () => {
                UIView.BeginAnimations ("OpenAnimation");
                UIView.SetAnimationDuration (((double)menu.AnimationDurationMillisecond) / 1000);
                _dragGesture.LayoutShowStatus ();
            };

            if (_menuOverlayRenderer == null) {
                _menuOverlayRenderer = RendererFactory.GetRenderer (menu);

                _panGesture = new UIPanGestureRecognizer (() => {
                    var p0 = _panGesture.LocationInView (_pageRenderer.View);
                    if (_panGesture.State == UIGestureRecognizerState.Began) {
                        _dragGesture.DragBegin (p0.X, p0.Y);

                    } else if (_panGesture.State == UIGestureRecognizerState.Changed
                               && _panGesture.NumberOfTouches == 1) {  
                        _dragGesture.DragMoving (p0.X, p0.Y);

                    } else if (_panGesture.State == UIGestureRecognizerState.Ended) {
                        _dragGesture.DragFinished ();
                    }
                });
                _menuOverlayRenderer.NativeView.AddGestureRecognizer (_panGesture);             
            }

            var rect = _dragGesture.GetHidePosition ();
            menu.Layout (new Xamarin.Forms.Rectangle (
                rect.left,
                rect.top,
                (rect.right - rect.left),
                (rect.bottom - rect.top)));
            _menuOverlayRenderer.NativeView.Hidden = !menu.IsVisible;
            _menuOverlayRenderer.NativeView.Frame = new CGRect (
                rect.left,
                rect.top,
                (rect.right - rect.left), 
                (rect.bottom - rect.top));
            _menuOverlayRenderer.NativeView.SetNeedsLayout ();

        }

        public void OnElementChanged (VisualElementChangedEventArgs e)
        {
            _basePage = e.NewElement as IMenuContainerPage;
        }

        public void ViewDidLayoutSubviews ()
        {
            LayoutMenu ();
        }

        public void ViewDidAppear (bool animated)
        {  
            if (!CheckPageAndMenu ())
                return;
            if (_basePage.SlideMenu.IsFullScreen)
                UIApplication.SharedApplication.KeyWindow.AddSubview (_menuOverlayRenderer.NativeView);
            else
                _pageRenderer.View.AddSubview (_menuOverlayRenderer.NativeView);
        }

        public void ViewDidDisappear (bool animated)
        {        
            if (_menuOverlayRenderer != null)
                _menuOverlayRenderer.NativeView.RemoveFromSuperview ();
            HideBackgroundOverlay ();
        }


        public void ViewWillTransitionToSize (CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
        {
            // This API sometime cannot return the correct value.
            // Maybe this is the Xamarin or iOS bug
            // https://bugzilla.xamarin.com/show_bug.cgi?id=37064
            var menu = _basePage.SlideMenu;
            double NavigationBarHeight = 0;
            // this is used for rotation 
            double bigValue = UIScreen.MainScreen.Bounds.Height > UIScreen.MainScreen.Bounds.Width ? UIScreen.MainScreen.Bounds.Height : UIScreen.MainScreen.Bounds.Width;
            double smallValue = UIScreen.MainScreen.Bounds.Height < UIScreen.MainScreen.Bounds.Width ? UIScreen.MainScreen.Bounds.Height : UIScreen.MainScreen.Bounds.Width;
            if (toSize.Width < toSize.Height) {
                ScreenSizeHelper.ScreenHeight = bigValue;
                // this is used for mutiltasking
                ScreenSizeHelper.ScreenWidth = toSize.Width < smallValue ? toSize.Width : smallValue;
                NavigationBarHeight = bigValue - toSize.Height;
            } else {
                ScreenSizeHelper.ScreenHeight = smallValue;
                ScreenSizeHelper.ScreenWidth = toSize.Width < bigValue ? toSize.Width : bigValue;
                NavigationBarHeight = smallValue - toSize.Height;
            }
            if (_dragGesture == null)
                return;
            
            menu.PageBottomOffset = NavigationBarHeight;

            _dragGesture.UpdateLayoutSize (menu);
            var rect = _dragGesture.GetHidePosition ();
            menu.Layout (new Xamarin.Forms.Rectangle (
                rect.left,
                rect.top,
                (rect.right - rect.left),
                (rect.bottom - rect.top)));
            _dragGesture.LayoutHideStatus ();

        }
    }
}

