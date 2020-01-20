using System.Xml;

namespace GapiCodegen.Util
{
    public static class XmlElementExtensions
    {
        public static bool GetAttributeAsBoolean(this XmlElement element, string name)
        {
            var value = element.GetAttribute(name);

            return !string.IsNullOrEmpty(value) && XmlConvert.ToBoolean(value);
        }
    }
}
