//Services\FilterEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.Services
{
    public static class FilterEngine
    {
        // include / exclude — коллекции FilterItem, FilterField предполагаются в проекте
        public static IEnumerable<CisRow> Apply(IEnumerable<CisRow> rows, IEnumerable<FilterItem> include, IEnumerable<FilterItem> exclude)
        {
            if (rows == null) return Enumerable.Empty<CisRow>();

            var data = rows;

            // Include
            if (include != null)
            {
                foreach (var f in include)
                {
                    if (f == null) continue;

                    if (f.Field == FilterField.name)
                    {
                        data = data.Where(r => (r.Name ?? "").IndexOf(f.Value ?? "", StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    else
                    {
                        data = data.Where(r => GetField(r, f.Field) == (f.Value ?? ""));
                    }
                }
            }

            // Exclude
            if (exclude != null)
            {
                foreach (var f in exclude)
                {
                    if (f == null) continue;

                    if (f.Field == FilterField.name)
                    {
                        data = data.Where(r => (r.Name ?? "").IndexOf(f.Value ?? "", StringComparison.OrdinalIgnoreCase) < 0);
                    }
                    else
                    {
                        data = data.Where(r => GetField(r, f.Field) != (f.Value ?? ""));
                    }
                }
            }

            return data;
        }

        private static string GetField(CisRow r, FilterField f) => f switch
        {
            FilterField.create => r.Created ?? "",
            FilterField.ip => r.Ip ?? "",
            FilterField.inn => r.Inn ?? "",
            _ => ""
        };
    }
}
