using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements;
using Elements.Conversion.Revit;
using ADSK = Autodesk.Revit.DB;


namespace HyparRevitRoofConverter
{
    public class RoofConverter : IRevitConverter
    {
        public bool CanConvertToRevit => false;
        public bool CanConvertFromRevit => true;

        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            throw new NotImplementedException();
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
        {
            throw new NotImplementedException();
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            throw new NotImplementedException();
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            throw new NotImplementedException();
        }

    }
}
