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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

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
                '\b', //Backspace
                '\n', //Newline
                '\r', //Return
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
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;
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
                _viewModel.TextEntered(c.ToString());
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
    }
}
