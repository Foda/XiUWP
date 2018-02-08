using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace XiBackend
{
    class Program
    {
        private static string ConfigDir = @"";
        static AppServiceConnection connection = null;
        static CoreConnection _coreConnection;

        static void Main(string[] args)
        {
            var filename = "xi-core.exe";
            _coreConnection = new CoreConnection(filename, async (data) => 
            {
                dynamic obj = data;
                var valueSet = new ValueSet();
                valueSet.Add("method", obj["method"].ToString());
                valueSet.Add("parameters", obj["params"].ToString());

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(string.Format("Xi message: {0}", obj["method"]));

                await connection.SendMessageAsync(valueSet);
            });

            Thread appServiceThread = new Thread(new ThreadStart(ThreadProc));
            appServiceThread.Start();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("*****************************");
            Console.WriteLine("***** Xi Process Broker *****");
            Console.WriteLine("*****************************");
            Console.ReadLine();
        }

        static async void ThreadProc()
        {
            connection = new AppServiceConnection();
            connection.AppServiceName = "XiBackend";
            connection.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            connection.RequestReceived += Connection_RequestReceived;

            AppServiceConnectionStatus status = await connection.OpenAsync();
            switch (status)
            {
                case AppServiceConnectionStatus.Success:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Connection established - waiting for requests");
                    Console.WriteLine();
                    break;
                case AppServiceConnectionStatus.AppNotInstalled:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The app AppServicesProvider is not installed.");
                    return;
                case AppServiceConnectionStatus.AppUnavailable:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The app AppServicesProvider is not available.");
                    return;
                case AppServiceConnectionStatus.AppServiceUnavailable:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("The app AppServicesProvider is installed but it does not provide the app service {0}.", connection.AppServiceName));
                    return;
                case AppServiceConnectionStatus.Unknown:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("An unkown error occurred while we were trying to open an AppServiceConnection."));
                    return;
            }
        }

        private static async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var operation = args.Request.Message["operation"].ToString();

            if (operation == "client_started")
            {
                var configPath = args.Request.Message["config_dir"].ToString();
                var req = new Dictionary<string, dynamic> { { "config_dir", configPath } };
                _coreConnection.SendRpcAsync("client_started", req);
            }
            else if (operation == "new_view")
            {
                var deferral = args.GetDeferral();
                string path = args.Request.Message["file_path"].ToString();

                var req = new Dictionary<string, dynamic> { { "file_path", path } };
                var id = (_coreConnection.SendRpc(operation, req) as JValue).Value as string;

                var valueSet = new ValueSet();
                valueSet.Add("view_id", id);

                await args.Request.SendResponseAsync(valueSet);

                deferral.Complete();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(string.Format("New View: {0}", id));
            }
            else if (operation == "save")
            {
                string path = args.Request.Message["file_path"].ToString();
                string id = args.Request.Message["view_id"].ToString();

                var req = new Dictionary<string, dynamic>
                {
                    {
                        "file_path", path
                    },
                    {
                        "view_id", id
                    }
                };
                _coreConnection.SendRpcAsync(operation, req);
            }
            else if (operation == "edit")
            {
                string method = args.Request.Message["method"].ToString();
                string viewId = args.Request.Message["view_id"].ToString();
                string parameters = args.Request.Message["params"].ToString();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(string.Format("Received message: {0}", method));
                Console.WriteLine(string.Format("Params: {0}", parameters));

                var editMsg = new Dictionary<string, dynamic>
                {
                    { "method", method },
                    { "view_id", viewId }
                };

                if (method == "insert" || method == "find")
                {
                    var insertParams = new Dictionary<string, dynamic>
                    {
                        { "chars", parameters }
                    };

                    if (method == "find")
                    {
                        insertParams.Add("case_sensitive", false);
                    }

                    editMsg.Add("params", insertParams);
                }

                if (method == "click" || method == "scroll" || method == "drag" || method == "request")
                {
                    var intArrayParams = args.Request.Message["params"] as int[];
                    editMsg.Add("params", intArrayParams);
                }

                var req = new Dictionary<string, dynamic>
                {
                    { "edit", editMsg }
                };

                _coreConnection.SendRpcAsync(operation, editMsg);
            }
        }
    }
}
