using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinUIOrderApp.Models
{
    public class CisItem
    {
        public string Cis
        {
            get; set;
        }
        public string Name
        {
            get; set;
        }
        public string RequestedCis
        {
            get; set;
        }
        public string ProductName
        {
            get; set;
        }
        public string Status
        {
            get; set;
        }
        public string OwnerName
        {
            get; set;
        }
        public string Gtin
        {
            get; set;
        } = string.Empty;

        public string Ki
        {
            get; set;
        } = string.Empty;

        public string FullCode
        {
            get; set;
        } = string.Empty;
    }
}