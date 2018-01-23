using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiUWP.ViewModel
{
    public class MarkdownPreviewViewModel : ReactiveObject
    {
        private string _markdownPreviewText = "";
        public string MarkdownPreviewText
        {
            get { return _markdownPreviewText; }
            set { this.RaiseAndSetIfChanged(ref _markdownPreviewText, value); }
        }
    }
}
