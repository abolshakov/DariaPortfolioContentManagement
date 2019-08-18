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

    internal class PortfolioItem: IImageOwner
    {
	    private int _id;
        private string _title;
        private string _image;
        private string _description;

        public PortfolioItem()
        {
            Category = Category.Undefined;
            ProjectItems = new List<ProjectItem>();
        }

        [Browsable(false)]
        [JsonProperty("id")]
        public int Id
        {
	        get
	        {
		        if (_id == 0)
		        {
			        _id = PersistenceManager.NextId;
		        }
		        return _id;
	        }
	        set => _id = value;
        }

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

        [JsonProperty("projectItems")]
        [Browsable(false)]
        public List<ProjectItem> ProjectItems { get; set; }
    }
}
