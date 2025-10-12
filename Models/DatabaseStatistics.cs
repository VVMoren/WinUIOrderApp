using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Models/DatabaseStatistics.cs
namespace WinUIOrderApp.Models
{
    public class DatabaseStatistics
    {
        public int TotalCount
        {
            get; set;
        }
        public int AvailableCount
        {
            get; set;
        }
        public int UsedCount
        {
            get; set;
        }
    }
}