using System;
using System.Collections.Generic;
using System.Text;

namespace SearchLego.DataSeeder.Entities
{
   public  class ClientConfiguration
    {
        public string _id { get; set; }
        public string ClientName { get; set; }
        public List<Project> Projects { get; set; }
    }

    public class CognitiveSetting
    {
        public string ApiUrl { get; set; }
        public bool Enable { get; set; }
        public string ExcludeFields { get; set; }
        public string ConcatenateValue { get; set; }
    }

    public class Project
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public CognitiveSetting CognitiveSetting { get; set; }
    }

}
