using System.Xml;
using GapiCodegen.Generatables;

namespace GapiCodegen
{
    public class MethodAbiField : StructAbiField
    {
        public MethodAbiField(XmlElement element, ClassBase containerType, string infoName) :
            base(element, containerType, infoName)
        {
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

        public override string CName => ParentStructureName != null
            ? $"{ParentStructureName}{'.'}{Name}"
            : Name;
    }
}
