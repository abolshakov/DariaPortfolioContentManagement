using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using Newtonsoft.Json;

namespace ContentManagement
{
    internal class PreviewItem: IImageOwner
    {
        private string _title;
        private string _image;
        private string _description;

        public PreviewItem()
        {
            Id = PersistenceManager.NextId;
            PortfolioItems = new List<PortfolioItem>();
        }

        [JsonIgnore]
        [Browsable(false)]
        public int Id { get; set; }

        [JsonProperty("title")]
		[DefaultValue("")]
        public string Title
        {
            get => _title;
            set => _title = value?.Trim();
        }

        [JsonProperty("image")]
        [DefaultValue("")]
        [Editor(typeof(ImagePicker), typeof(UITypeEditor))]
        [TypeConverter(typeof(DummyConverter))]
        public string Image
        {
            get => _image;
            set => _image = value?.Trim();
        }

        [JsonProperty("description")]
        [DefaultValue("")]
        public string Description
        {
            get => _description;
            set => _description = value?.Trim();
        }

        [JsonProperty("portfolioItems")]
        [Browsable(false)]
        public List<PortfolioItem> PortfolioItems { get; set; }
    }
}
