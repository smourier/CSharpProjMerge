using System.Xml;

namespace CSharpProjMerge.Utilities
{
    public static class Extensions
    {
        public static string GetInclude(this XmlElement element)
        {
            if (element == null)
                return null;

            return element.GetAttribute("Include").Nullify();
        }
    }
}
