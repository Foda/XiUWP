using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiUWP.Service
{
    public class XiScrollToMsg
    {
        [JsonProperty("col")]
        public int Column { get; set; }

        [JsonProperty("line")]
        public int Line { get; set; }

        [JsonProperty("view_id")]
        public string ViewId { get; set; }
    }
}
