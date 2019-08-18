using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContentManagement
{
    internal class Portfolio
    {
        [JsonProperty("portfolioItems")]
        public List<PortfolioItem> PortfolioItems { get; set; }
    }
}
