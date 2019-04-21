using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ContentManagement
{
    internal class MarginConverter: TypeConverter
    {
	    internal const string DefaultMargin = "0 0 0 0";

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (!(value is string))
            {
                return base.ConvertFrom(context, culture, value);
            }

            var str = Convert.ToString(value);

            if (string.IsNullOrWhiteSpace(str))
            {
	            return DefaultMargin;
            }

            var reg = new Regex(@"\d{1,4}\ \d{1,4}\ \d{1,4}\ \d{1,4}");

            if (!reg.IsMatch(str.Trim()))
            {
                throw new FormatException("Please enter margins as numbers separated by spaces in the format: Top Right Bottom Left.");
            }
            return value;
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            return destinationType == typeof(string)
	            ? value 
	            : base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
