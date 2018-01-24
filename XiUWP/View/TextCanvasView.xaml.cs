﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using XiUWP.ViewModel;

namespace XiUWP.View
{
    public sealed partial class TextCanvasView : UserControl
    {
        private TextViewModel _viewModel;
        private DispatcherTimer _blinkStartTimer;
        private HashSet<char> _charsToSkip = new HashSet<char>()
        {
            '\b', // Backspace
            '\n', // Newline
            '\r', // Return
        };

        public TextCanvasView()
        {
            this.InitializeComponent();
            
            _viewModel = new TextViewModel(this.RootCanvas, this.GutterCanvas);
            this.DataContext = _viewModel;

            _blinkStartTimer = new DispatcherTimer();
            _blinkStartTimer.Interval = TimeSpan.FromMilliseconds(200);
            _blinkStartTimer.Tick += _blinkStartTimer_Tick;
        }

        public async Task OpenNewDocument(string file)
        {
            await _viewModel.OpenFile(file);
            HookupInput();
            OpenHint.Visibility = Visibility.Collapsed;
        }

        private void HookupInput()
        {
            // Setup input events
            RootCanvas.DoubleTapped += RootCanvas_DoubleTapped;
            RootCanvas.PointerPressed += RootCanvas_PointerPressed;
            RootCanvas.PointerReleased += RootCanvas_PointerReleased;

            Observable
                .FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => RootCanvas.PointerMoved += h,
                    h => RootCanvas.PointerMoved -= h)
                .Where(_ => _viewModel.IsDraggingMouse)
                .Select(x => x.EventArgs.GetCurrentPoint(RootCanvas).Position)
                .Sample(TimeSpan.FromMilliseconds(50))
                .ObserveOnDispatcher(Windows.UI.Core.CoreDispatcherPriority.Low)
                .Subscribe((point) => _viewModel.PointerMoved(point));

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;
            Window.Current.CoreWindow.ResizeCompleted += CoreWindow_ResizeCompleted;
            Window.Current.CoreWindow.PointerWheelChanged += CoreWindow_PointerWheelChanged;
        }

        private void CoreWindow_PointerWheelChanged(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.PointerEventArgs args)
        {
            MainVertScrollbar.Value += (args.CurrentPoint.Properties.MouseWheelDelta * -0.25);
        }

        private void CoreWindow_ResizeCompleted(Windows.UI.Core.CoreWindow sender, object args)
        {
            if (_viewModel == null)
                return;

            _viewModel.UpdateVisibleLineCount();
        }

        private void CoreWindow_CharacterReceived(Windows.UI.Core.CoreWindow sender, 
            Windows.UI.Core.CharacterReceivedEventArgs args)
        {
            if (_viewModel == null)
                return;

            RootCanvas.Focus(FocusState.Programmatic);

            var c = Convert.ToChar(args.KeyCode);
            if (_charsToSkip.Contains(c))
            {
                return;
            }
            else
            {
                args.Handled = true;
                _viewModel.TextEntered(c);
            }

            ResetBlinkTimer();
        }

        private async void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, 
            Windows.UI.Core.KeyEventArgs args)
        {
            if (_viewModel == null)
                return;

            RootCanvas.Focus(FocusState.Programmatic);
            await _viewModel.KeyPressed(args.VirtualKey);

            ResetBlinkTimer();
        }

        private void RootCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            e.Handled = true;
            RootCanvas.Focus(FocusState.Programmatic);

            var pointerPoint = e.GetCurrentPoint(RootCanvas);
            _viewModel.PointerPressed(pointerPoint.Position);
        }

        private void RootCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            e.Handled = true;
            RootCanvas.Focus(FocusState.Programmatic);

            var pointerPoint = e.GetPosition(RootCanvas);
            _viewModel.PointerPressed(pointerPoint, 2);
        }

        private void RootCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            e.Handled = true;
            _viewModel.PointerReleased(e.GetCurrentPoint(RootCanvas).Position);
        }

        private void ResetBlinkTimer()
        {
            BlinkAnim.Stop();
            _blinkStartTimer.Stop();
            _blinkStartTimer.Start();
        }

        private void _blinkStartTimer_Tick(object sender, object e)
        {
            BlinkAnim.Begin();
            _blinkStartTimer.Stop();
        }
    }
}
