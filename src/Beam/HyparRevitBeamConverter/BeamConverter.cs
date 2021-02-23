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
                ADSK.BuiltInCategory.OST_StructuralFraming,
                ADSK.BuiltInCategory.OST_StructuralFramingSystem
            };
            ElementMulticategoryFilter multicategoryFilter = new ElementMulticategoryFilter(targetCategories);

            return collector.WhereElementIsNotElementType().WherePasses(multicategoryFilter);
        }

        private List<ElementId> _convertedIds = new List<ElementId>();
        public Element[] FromRevit(Autodesk.Revit.DB.Element revitElement, Document document)
        {
            switch (revitElement)
            {
                case BeamSystem beamSystem:
                    {
                        var beams = new List<Element>();
                        foreach (var b in beamSystem.GetBeamIds())
                        {
                            if (_convertedIds.Contains(b)) continue;

                            var beamFamilyInstance = document.GetElement(b) as ADSK.FamilyInstance;
                            beams.Add(Create.BeamFromRevitBeam(beamFamilyInstance));
                            _convertedIds.Add(b);
                        }

                        return beams.ToArray();
                    }
                case FamilyInstance beamFamilyInstance:
                    {
                        if (_convertedIds.Contains(beamFamilyInstance.Id)) break;

                        _convertedIds.Add(beamFamilyInstance.Id);

                        var beams = new List<Element> { Create.BeamFromRevitBeam(beamFamilyInstance) };

                        return beams.ToArray();
                    }
            }

            return null;


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