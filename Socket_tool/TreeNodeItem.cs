using System.Collections.ObjectModel;
using System.Net.Sockets;

namespace Socket_tool
{
    internal class TreeNodeItem
    {
        public int IndexId { get; set; }
        public string DisplayName { get; set; }
        public string Name { get; set; }

        public Socket Server { get; set; }
        public Socket Client { get; set; }

        public ObservableCollection<TreeNodeItem> Children { get; } = new ObservableCollection<TreeNodeItem>();
    }
}