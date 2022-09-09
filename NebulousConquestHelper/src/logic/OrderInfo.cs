using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace NebulousConquestHelper
{
    [XmlType("OrderInfo")]
    [Serializable]
    public class OrderInfo
    {
        public bool Complete { get; set; }
        public string CombatUnit { get; set; }
        public OrderType OrderType { get; set; }
    }

    public enum OrderType
    {
        Move,
        Repair,
        Refit,
        Build
    }
}
