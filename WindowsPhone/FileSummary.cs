using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ExifLib;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WindowsPhone
{
    class ExifDatum
    {
        public string Name { get; private set; }

        public object Value { get; private set; }

        public string DisplayValue { get; private set; }

        public ExifDatum(string name, string value)
        {
            Name = name;
            Value = value;
            DisplayValue = value;
        }

        public ExifDatum(FieldInfo fieldInfo, object value)
        {
            Name = ParseName(fieldInfo.Name);
            Value = value;
            DisplayValue = value == null ? null : GenericValueStringify(value);
        }

        private string ParseName(string name)
        {
            if (name[0] != '<')
            {
                return name;
            }

            return name.Substring(1, name.IndexOf('>') - 1);
        }

        public static string GenericValueStringify(object value)
        {
            var type = value.GetType();

            switch (type.FullName)
            {
                case "System.Boolean":
                    return (Boolean)value ? "True" : "False";

                case "System.Byte[]":
                    return "(byte data)";

                case "System.Double[]":
                    return String.Join(" / ", (double[])value);

                case "System.Int32[]":
                    return String.Join(" / ", (Int32[])value);

                case "System.String[]":
                    return String.Join(" / ", (string[])value);

                case "System.UInt32[]":
                    return String.Join(" / ", (UInt32[])value);
            }

            return value.ToString();
        }
    }

    class FileSummary
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public List<ExifDatum> BasicData { get; set; }

        public List<ExifDatum> ExifData { get; set; }

        public List<ExifDatum> FullData { get; set; }

        public FileSummary(BitmapImage image, StorageFile file, IDictionary<string, object> properties, System.IO.Stream stream)
        {
            JpegInfo exifInfo = ExifReader.ReadJpeg(stream);

            this.BasicData = new List<ExifDatum>
            {
                new ExifDatum("Name", file.Name),
                new ExifDatum("Path", file.Path),
                new ExifDatum("Created", file.DateCreated.ToString()),
                new ExifDatum("Dimensions", image.PixelWidth.ToString() + "px by " + image.PixelHeight.ToString() + "px")
            };

            if (properties.ContainsKey("size"))
            {
                double num = double.Parse(properties["size"].ToString(), System.Globalization.CultureInfo.InvariantCulture);

                this.BasicData.Add(new ExifDatum("Size", ConvertBitSize(num)));
            }

            this.ExifData = exifInfo
                .GetType()
                .GetRuntimeFields()
                .Select(field => new ExifDatum(field, field.GetValue(exifInfo)))
                .Where(datum => datum.DisplayValue != null)
                .OrderBy(datum => datum.Name)
                .ToList();

            this.FullData = properties
                .Where(field => field.Value != null && field.Key[0] != '{')
                .Select(field => ConvertPairToDatum(field))
                .OrderBy(datum => datum.Name)
                .ToList();
        }

        public static ExifDatum ConvertPairToDatum(KeyValuePair<string, object> datum)
        {
            string name = datum.Key;
            string value = ExifDatum.GenericValueStringify(datum.Value);

            if (name.StartsWith("System."))
            {
                name = name.Substring(7);
            }

            if (name.Length > 35)
            {
                name = name.Replace(".", ".\r\n");
            }

            return new ExifDatum(name, value);
        }

        public static string ConvertBitSize(double bytes, bool si = false)
        {
            int units = si ? 1000 : 1024;

            if (bytes < units)
            {
                return bytes + " B";
            }

            int exp = (int)(Math.Log(bytes) / Math.Log(units));

            string pre = (si ? "kMGTPE" : "KMGTPE")[exp - 1].ToString();

            double result = Math.Round(bytes / Math.Pow(units, exp), 2);

            return String.Format("{0} {1}B", result, pre);
        }
    }
}
