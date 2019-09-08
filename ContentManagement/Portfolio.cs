using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContentManagement
{
    internal class Portfolio
    {
        [JsonProperty("projects")]
        public List<Project> Projects { get; set; }
    }
}
