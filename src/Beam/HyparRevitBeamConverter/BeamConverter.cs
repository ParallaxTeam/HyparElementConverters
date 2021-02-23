using Autodesk.Revit.DB;
using Elements.Conversion.Revit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.IFC;

using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Element = Elements.Element;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitBeamConverter
{
    public class BeamConverter : IRevitConverter
    {
        public bool CanConvertToRevit => true;
        public bool CanConvertFromRevit => true;

        public FilteredElementCollector AddElementFilters(FilteredElementCollector collector)
        {
            List<BuiltInCategory> targetCategories = new List<BuiltInCategory>
            {
                ADSK.BuiltInCategory.OST_StructuralFraming
            };
            ElementMulticategoryFilter multicategoryFilter = new ElementMulticategoryFilter(targetCategories);

            return collector.WhereElementIsNotElementType().WherePasses(multicategoryFilter);
        }

        public Element[] FromRevit(Autodesk.Revit.DB.Element revitElement, Document document)
        {
            var beamFamilyInstance = revitElement as FamilyInstance;

            var beams = new List<Element> { Create.BeamFromRevitBeam(beamFamilyInstance) };

            return beams.ToArray();
        }

        public ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            throw new NotImplementedException();
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            throw new NotImplementedException();
        }



    }
}