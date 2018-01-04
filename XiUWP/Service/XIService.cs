using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

namespace XiUWP.Service
{
    /// <summary>
    /// TODO:
    /// scroll (0,18)
    /// </summary>
    public class XIService
    {
        public string ViewID { get; private set; }

        private readonly Subject<XiUpdateOperation> _xiUpdateSubject =
            new Subject<XiUpdateOperation>();

        private readonly Subject<XiStyleMsg> _xiSetStyleSubject =
            new Subject<XiStyleMsg>();

        public IObservable<XiUpdateOperation> UpdateObservable
        {
            get { return _xiUpdateSubject.AsObservable(); }
        }

        public IObservable<XiStyleMsg> StyleObservable
        {
            get { return _xiSetStyleSubject.AsObservable(); }
        }

        public XIService()
        {
            App.Connection.RequestReceived += Connection_RequestReceived;
        }

        public async Task OpenNewView()
        {
            var valueSet = new ValueSet();
            valueSet.Add("operation", "new_view");
            valueSet.Add("file_path", @"C:\Projects\Test\document.md");

            var reply = await App.Connection.SendMessageAsync(valueSet);
            ViewID = reply.Message["view_id"].ToString();
        }

        public async Task SaveView()
        {
            var valueSet = new ValueSet();
            valueSet.Add("operation", "save");
            valueSet.Add("view_id", ViewID);
            valueSet.Add("file_path", @"C:\Projects\Test\document.md");

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

        private void Connection_RequestReceived(Windows.ApplicationModel.AppService.AppServiceConnection sender,
            Windows.ApplicationModel.AppService.AppServiceRequestReceivedEventArgs args)
        {
            var message = args.Request.Message;
            var method = message["method"].ToString();

            if (method == "update")
            {
                try
                {
                    var json = message["parameters"].ToString();
                    var update = JsonConvert.DeserializeObject<XiUpdate>(json);

                    // Only actually care about the meat of the update
                    _xiUpdateSubject.OnNext(update.Update);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine(ex);
                }
            }

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
    }
}
