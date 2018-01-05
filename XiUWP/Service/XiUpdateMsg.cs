using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiUWP.Service
{
    public class XiUpdate
    {
        [JsonProperty("update")]
        public XiUpdateOperation Update { get; set; }

        [JsonProperty("view_id")]
        public string ViewID { get; set; }
    }

    public class XiUpdateOperation
    {
        [JsonProperty("pristine")]
        public bool Pristine { get; set; }

        [JsonProperty("ops")]
        public List<XiOperation> Operations { get; set; }
    }

    public class XiOperation
    {
        [JsonProperty("op")]
        public string Operation { get; set; }

        [JsonProperty("n")]
        public int LinesChangeCount { get; set; }

        [JsonProperty("lines")]
        public IList<XiLine> Lines { get; set; }
    }

    public class XiLine
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("cursor")]
        public IList<int> Cursor { get; set; }

        [JsonProperty("styles")]
        public IList<int> Style { get; set; }
    }
}
