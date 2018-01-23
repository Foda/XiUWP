using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private HashSet<char> _charsToSkip;

        public TextCanvasView()
        {
            this.InitializeComponent();

            _charsToSkip = new HashSet<char>()
            {
                '\b', // Backspace
                '\n', // Newline
                '\r', // Return
            };

            _viewModel = new TextViewModel(this.RootCanvas);
            this.DataContext = _viewModel;

            // Setup input events
            RootCanvas.PointerPressed += RootCanvas_PointerPressed;
            RootCanvas.PointerMoved += RootCanvas_PointerMoved;
            RootCanvas.PointerReleased += RootCanvas_PointerReleased;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;
            Window.Current.CoreWindow.ResizeCompleted += CoreWindow_ResizeCompleted;
            Window.Current.CoreWindow.PointerWheelChanged += CoreWindow_PointerWheelChanged;
        }

        public async Task OpenNewDocument(string file)
        {
            await _viewModel.OpenFile(file);
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
        }

        private async void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, 
            Windows.UI.Core.KeyEventArgs args)
        {
            if (_viewModel == null)
                return;

            await _viewModel.KeyPressed(args.VirtualKey);
        }

        private void RootCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            e.Handled = true;
            RootCanvas.Focus(FocusState.Programmatic);
            _viewModel.PointerPressed(e.GetCurrentPoint(RootCanvas).Position);
        }

        private async void RootCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            await _viewModel.PointerMoved(e.GetCurrentPoint(RootCanvas).Position);
        }

        private void RootCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            e.Handled = true;
            RootCanvas.Focus(FocusState.Programmatic);
            _viewModel.PointerReleased(e.GetCurrentPoint(RootCanvas).Position);
        }

        private void RootCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Xaml.Window.Current.CoreWindow.PointerCursor = 
                new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.IBeam, 1);
        }

        private void RootCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Xaml.Window.Current.CoreWindow.PointerCursor = 
                new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 1);
        }
    }
}
