using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace XiUWP.Model
{
    [Serializable]
    public class EditAction
    {
        public VirtualKey Modifier { get; }
        public VirtualKey Key { get; }
        public string Command { get; }

        public EditAction(VirtualKey modifier, VirtualKey key, string command)
        {
            Modifier = modifier;
            Key = key;
            Command = command;
        }
    }
}
