using System.Xml;

namespace GapiCodegen
{
	public static class XmlElementExtensions
	{
		public static bool GetAttributeAsBoolean (this XmlElement elt, string name)
		{
			string value = elt.GetAttribute (name);

			if (string.IsNullOrEmpty (value)) {
				return false;
			} else {
				return XmlConvert.ToBoolean (value);
			}
		}
	}
}

