using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB.IFC;
using Elements;
using ADSK = Autodesk.Revit.DB;
using Elements.Conversion.Revit;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Element = Elements.Element;

namespace HyparRevitCurtainWallConverter
{
    public class CurtainWallConverter : IRevitConverter
    {
        public bool CanConvertToRevit => true;
        public bool CanConvertFromRevit => true;

        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            //collect the curtain walls using the filter
            return collector.OfCategory(ADSK.BuiltInCategory.OST_Walls);
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
        {
            if (((ADSK.Wall)revitElement).WallType.Kind != ADSK.WallKind.Curtain) return null;
            return Create.MakeHyparCurtainWallFromRevitCurtainWall(revitElement, document).ToArray();
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            return CurtainWallFromHypar(hyparElement, context);
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            var types = allElements.Select(e => e.GetType());
            var elemType = typeof(Elements.CurtainWallPanel);
            return allElements.Where(e => e.GetType().FullName == typeof(Elements.CurtainWallPanel).FullName).ToArray();
        }

        private static ADSK.ElementId[] CurtainWallFromHypar(Element hyparElement, LoadContext context)
        {
            var doc = context.Document;
            var hyparCurtainWall = hyparElement as CurtainWallPanel;

            List<ADSK.ElementId> newStuff = new List<ADSK.ElementId>();



            newStuff.AddRange(DirectShapesFromCurtainWall(hyparCurtainWall, context));

            return newStuff.ToArray();
        }

        
        private static ADSK.ElementId[] DirectShapesFromCurtainWall(CurtainWallPanel hyparCurtainWall, LoadContext context)
        {
            List<ADSK.ElementId> newStuff = new List<ADSK.ElementId>();
            foreach (var panel in hyparCurtainWall.GlazedPanels)
            {
                var curves = panel.Perimeter.Segments().Select(s => s.ToRevitCurve(true));
                var profileLoop = ADSK.CurveLoop.Create(curves.ToList());
                var profileLoops = new List<ADSK.CurveLoop> { profileLoop };
                var solid = ADSK.GeometryCreationUtilities.CreateExtrusionGeometry(profileLoops, panel.Normal().ToXYZ(true), 0.125);

                var elemId = new ADSK.ElementId(ADSK.BuiltInCategory.OST_CurtainWallPanels);
                var ds = ADSK.DirectShape.CreateElement(context.Document, elemId);
                ds.SetShape(new[] { solid }, ADSK.DirectShapeTargetViewType.Default);

                newStuff.Add(elemId);
            }

            return newStuff.ToArray();
        }
    }
}
