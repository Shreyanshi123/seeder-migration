using System;
using System.Collections.Generic;
using System.Text;

namespace SearchLego.DataSeeder.Entities.Cognitive
{
    public class DataDictionaryResponse
    {
        public string IndexName { get; set; }
        public bool IsSuccess { get; set; }
        public int TotalRecords { get; set; }
    }
}
