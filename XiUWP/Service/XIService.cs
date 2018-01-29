using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace XiUWP.Service
{
    /// <summary>
    /// </summary>
    public class XIService
    {
        private string _currentFile;

        public string ViewID { get; private set; }

        private readonly Subject<XiUpdate> _xiUpdateSubject =
            new Subject<XiUpdate>();

        private readonly Subject<XiStyleMsg> _xiSetStyleSubject =
            new Subject<XiStyleMsg>();

        private readonly Subject<XiScrollToMsg> _xiScrollSubject =
            new Subject<XiScrollToMsg>();

        public IObservable<XiUpdate> UpdateObservable
        {
            get { return _xiUpdateSubject.AsObservable(); }
        }

        public IObservable<XiStyleMsg> StyleObservable
        {
            get { return _xiSetStyleSubject.AsObservable(); }
        }

        public IObservable<XiScrollToMsg> ScrollToObservable
        {
            get { return _xiScrollSubject.AsObservable(); }
        }

        public async Task OpenNewView(string file)
        {
            await StartClient();

            _currentFile = file;

            var valueSet = new ValueSet();
            valueSet.Add("operation", "new_view");
            valueSet.Add("file_path", _currentFile);

            var reply = await App.Connection.SendMessageAsync(valueSet);
            ViewID = reply.Message["view_id"].ToString();
        }

        private async Task StartClient()
        {
            App.Connection.RequestReceived += Connection_RequestReceived;
            await WriteDefaultSettings();

            var valueSet = new ValueSet();
            valueSet.Add("operation", "client_started");
            valueSet.Add("config_dir", Windows.Storage.ApplicationData.Current.LocalFolder.Path + "\\");

            await App.Connection.SendMessageAsync(valueSet);
        }

        private async Task WriteDefaultSettings()
        {
            var defaultSettingsFileName = "preferences.xiconfig";
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            Console.WriteLine(localFolder.Path);

            var defaultSettings = new StringBuilder();
            defaultSettings.AppendLine("tab_size = 4");
            defaultSettings.AppendLine("translate_tabs_to_spaces = true");
            defaultSettings.AppendLine("use_tab_stops = true");
            defaultSettings.AppendLine("auto_indent = false");
            defaultSettings.AppendLine("scroll_past_end = true");
            defaultSettings.AppendLine("wrap_width = 0");
            defaultSettings.AppendLine("line_ending = \"\r\n\"");

            var settingsFile = await localFolder.CreateFileAsync(defaultSettingsFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(settingsFile, defaultSettings.ToString());

            // Create plugins folder
            if (!System.IO.Directory.Exists(localFolder.Path + "\\plugins"))
            {
                await localFolder.CreateFolderAsync("plugins");
            }
        }

        public async Task Save()
        {
            var valueSet = new ValueSet();
            valueSet.Add("operation", "save");
            valueSet.Add("view_id", ViewID);
            valueSet.Add("file_path", _currentFile);

            await App.Connection.SendMessageAsync(valueSet);
        }

        public async Task Insert(string text)
        {
            var valueSet = new ValueSet();
            valueSet.Add("operation", "edit");
            valueSet.Add("method", "insert");
            valueSet.Add("view_id", ViewID);
            valueSet.Add("params", text);

            await App.Connection.SendMessageAsync(valueSet);
        }

        /// <summary>
        /// delete_backword
        /// delete_forward
        /// delete_word_backword
        /// delete_word_forward
        /// delete_to_end_of_paragraph
        /// delete_to_beginning_of_line
        /// insert_newline
        /// insert_tab
        /// move_up
        /// move_down
        /// move_left
        /// move_right
        /// move_to_left_end_of_line
        /// scroll_page_up
        /// undo
        /// redo
        /// </summary>
        /// <param name="gestureName"></param>
        /// <returns></returns>
        public async Task GenericEdit(string gestureName)
        {
            var valueSet = new ValueSet();
            valueSet.Add("operation", "edit");
            valueSet.Add("method", gestureName);
            valueSet.Add("view_id", ViewID);
            valueSet.Add("params", "");

            await App.Connection.SendMessageAsync(valueSet);
        }

        public async Task Click(int line, int column, int modifiers, int clickCount)
        {
            //line and column (0-based, utf-8 code units), modifiers (again, 2 is shift), and click count.
            var valueSet = new ValueSet();
            valueSet.Add("operation", "edit");
            valueSet.Add("method", "click");
            valueSet.Add("view_id", ViewID);
            valueSet.Add("params", new int[4] { line, column, modifiers, clickCount });

            await App.Connection.SendMessageAsync(valueSet);
        }

        public async Task Drag(int line, int column)
        {
            //line and column (0-based, utf-8 code units), modifiers (again, 2 is shift), and click count.
            var valueSet = new ValueSet();
            valueSet.Add("operation", "edit");
            valueSet.Add("method", "drag");
            valueSet.Add("view_id", ViewID);
            valueSet.Add("params", new int[3] { line, column, 0 });

            await App.Connection.SendMessageAsync(valueSet);
        }

        public async Task Scroll(int firstLineIdx, int lastLineIdx)
        {
            var valueSet = new ValueSet();
            valueSet.Add("operation", "edit");
            valueSet.Add("method", "scroll");
            valueSet.Add("view_id", ViewID);
            valueSet.Add("params", new int[2] { firstLineIdx, lastLineIdx });

            await App.Connection.SendMessageAsync(valueSet);
        }

        private void Connection_RequestReceived(Windows.ApplicationModel.AppService.AppServiceConnection sender,
            Windows.ApplicationModel.AppService.AppServiceRequestReceivedEventArgs args)
        {
            var message = args.Request.Message;
            var method = message["method"].ToString();

            // View specific
            if (method == "update")
            {
                var json = message["parameters"].ToString();
                DeserializeMessage<XiUpdate>(json, _xiUpdateSubject);
            }

            if (method == "scroll_to")
            {
                var json = message["parameters"].ToString();
                DeserializeMessage<XiScrollToMsg>(json, _xiScrollSubject);
            }

            // Non-view specific
            if (method == "set_style")
            {
                try
                {
                    var json = message["parameters"].ToString();
                    var update = JsonConvert.DeserializeObject<XiStyleMsg>(json);
                    
                    _xiSetStyleSubject.OnNext(update);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        private void DeserializeMessage<T>(string json, Subject<T> subject) where T : IXiBaseMsg
        {
            try
            {
                var update = JsonConvert.DeserializeObject<T>(json);
                if (update.ViewID == this.ViewID)
                {
                    subject.OnNext(update);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
