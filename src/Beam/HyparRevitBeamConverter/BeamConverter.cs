using Autodesk.Revit.DB;
using Elements.Conversion.Revit;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB.Structure;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Element = Elements.Element;
using ADSK = Autodesk.Revit.DB;
using Line = Elements.Geometry.Line;

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
            if (!(revitElement is ADSK.FamilyInstance beamFamilyInstance)) return null;
            var beams = new List<Element> { Create.BeamFromRevitBeam(beamFamilyInstance) };

            return beams.ToArray();

        }

        public ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            Document doc = context.Document;
            Beam hyparBeam = hyparElement as Beam;

            string[] beamData = hyparBeam.Name.Split(',');

            //try to find the family symbol to use
            FamilySymbol familySymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().Where(f => f.Family.FamilyPlacementType == FamilyPlacementType.CurveDrivenStructural).FirstOrDefault(f => f.Name.Equals(beamData[1])) ??
                                        new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault(f => f.Family.FamilyPlacementType == FamilyPlacementType.CurveDrivenStructural);

            Level level = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels).WhereElementIsNotElementType().Cast<Level>().FirstOrDefault();

            ADSK.Curve revitCurve = null;
            switch (hyparBeam.Curve)
            {
                case Line line:
                    revitCurve = ADSK.Line.CreateBound(line.Start.ToXYZ(true), line.End.ToXYZ(true));
                    break;
                case Polyline polyLine:
                    revitCurve = HermiteSpline.Create(polyLine.Vertices.Select(v => v.ToXYZ(true)).ToList(), false);
                    break;
            }

            List<ElementId> newIds = new List<ElementId>();

            if (!familySymbol.IsActive) familySymbol.Activate();

            var newInstance = doc.Create.NewFamilyInstance(revitCurve, familySymbol, level, StructuralType.Beam);

            doc.Regenerate();
            //TODO: make this work just for cutbacks
            //newInstance.get_Parameter(BuiltInParameter.START_EXTENSION).Set(Elements.Units.MetersToFeet(hyparBeam.StartSetback));
            //newInstance.get_Parameter(BuiltInParameter.END_EXTENSION).Set(Elements.Units.MetersToFeet(hyparBeam.EndSetback));
            var rotation = hyparBeam.Rotation * Math.PI / 180;
            newInstance.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE).Set(rotation);
            doc.Regenerate();

            //set offset to compensate for center line
            var height = ExporterIFCUtils.GetMinSymbolHeight(familySymbol);

            newInstance.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE).Set(height / 2);

            newIds.Add(newInstance.Id);

            return newIds.ToArray();
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            var types = allElements.Select(e => e.GetType());
            var elemType = typeof(Elements.Beam);
            return allElements.Where(e => e.GetType().FullName == typeof(Elements.Beam).FullName).ToArray();
        }

    }
}