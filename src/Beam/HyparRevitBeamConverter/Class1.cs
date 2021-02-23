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
            //pre-collect the ids of beams that are not in place families
            var ids = collector.OfCategory(ADSK.BuiltInCategory.OST_StructuralFraming).Cast<FamilyInstance>()
                .Where(f => !f.Symbol.Family.IsInPlace).Select(i => i.Id).ToList();
            //build a filter for these
            ADSK.ElementIdSetFilter idSetFilter = new ADSK.ElementIdSetFilter(ids);

            return collector.OfCategory(ADSK.BuiltInCategory.OST_StructuralFraming).WherePasses(idSetFilter);
        }

        public Element[] FromRevit(Autodesk.Revit.DB.Element revitElement, Document document)
        {
            throw new NotImplementedException();
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