using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiUWP.Service
{
    public interface IXiBaseMsg
    {
        [JsonProperty("view_id")]
        string ViewID { get; set; }
    }
}
