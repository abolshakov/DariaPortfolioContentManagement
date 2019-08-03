using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ContentManagement
{
    public enum Category
    {
        Animation,
        Concept,
        Illustration,
        Undefined
    }

    internal class PreviewItem: IImageOwner
    {
        private string _title;
        private string _image;
        private string _description;

        public PreviewItem()
        {
            Id = PersistenceManager.NextId;
            Category = Category.Undefined;
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

        [JsonProperty("category")]
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(Category.Undefined)]
        public Category Category { get; set; }

        [JsonProperty("portfolioItems")]
        [Browsable(false)]
        public List<PortfolioItem> PortfolioItems { get; set; }
    }
}
