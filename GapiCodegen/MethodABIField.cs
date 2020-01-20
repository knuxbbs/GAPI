using System.Xml;
using GapiCodegen.Generatables;

namespace GapiCodegen
{
    public class MethodAbiField : StructAbiField
    {
        private XmlElement _element;
        
        public MethodAbiField(XmlElement element, ClassBase containerType, string infoName) :
            base(element, containerType, infoName)
        {
            _element = element;
        }

        public override string CType => "gpointer";

        public override bool IsCPointer() => true;

        public new string Name
        {
            get
            {
                var name = Element.GetAttribute("vm");

                if (string.IsNullOrEmpty(name))
                    name = Element.GetAttribute("signal_vm");

                return name;
            }
        }

        public override string StudlyName => Name;

        public override string CName => parent_structure_name != null
            ? $"{parent_structure_name}{'.'}{Name}"
            : Name;
    }
}