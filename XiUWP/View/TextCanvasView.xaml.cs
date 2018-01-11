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
        private CoreTextEditContext _editContext;
        private HashSet<char> _charsToSkip;

        public TextCanvasView()
        {
            this.InitializeComponent();
            this.Loaded += TextCanvasView_Loaded;

            _charsToSkip = new HashSet<char>()
            {
                '\b', // Backspace
                '\n', // Newline
                '\r', // Return
            };
        }

        private async void TextCanvasView_Loaded(object sender, RoutedEventArgs e)
        {
            while (App.Connection == null)
            {
                await Task.Delay(500);
            }

            _viewModel = new TextViewModel(this.RootCanvas);
            this.DataContext = _viewModel;

            // Setup input events
            RootCanvas.PointerPressed += RootCanvas_PointerPressed;
            RootCanvas.PointerReleased += RootCanvas_PointerReleased;

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
            _viewModel.UpdateVisibleLineCount();
        }

        private void CoreWindow_CharacterReceived(Windows.UI.Core.CoreWindow sender, 
            Windows.UI.Core.CharacterReceivedEventArgs args)
        {
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

        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, 
            Windows.UI.Core.KeyEventArgs args)
        {
            _viewModel.KeyPressed(args.VirtualKey);
        }

        private void RootCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
            RootCanvas.Focus(FocusState.Programmatic);
            _viewModel.PointerPressed(e.GetCurrentPoint(RootCanvas).Position);
        }

        private void RootCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
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
