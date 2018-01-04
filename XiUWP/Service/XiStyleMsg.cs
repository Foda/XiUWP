using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace XiUWP.Service
{
    public class XiStyleMsg
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("fg_color")]
        public int FgColor { get; set; }

        public Color Foreground
        {
            get
            {
                var bytes = BitConverter.GetBytes(FgColor);
                return Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
            }
        }

        [JsonProperty("bg_color")]
        public int BgColor { get; set; }

        public Color Background
        {
            get
            {
                var bytes = BitConverter.GetBytes(BgColor);
                return Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
            }
        }

        [JsonProperty("weight")]
        public int Weight { get; set; }

        [JsonProperty("italic")]
        public bool Italic { get; set; }

        [JsonProperty("underline")]
        public bool Underline { get; set; }
    }
}
