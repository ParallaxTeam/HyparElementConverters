using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Creation;
using Elements;
using ADSK = Autodesk.Revit.DB;
using Elements.Conversion.Revit;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Element = Elements.Element;

namespace HyparRevitCurtainWallConverter
{
    public class CurtainWallConverter : IRevitConverter
    {
        public bool CanConvertToRevit => true;
        public bool CanConvertFromRevit => true;

        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            //pre-collect curtain walls by seeing if they have a curtain grid. also exclude in-place families.
            var curtainWallIds = collector.OfCategory(ADSK.BuiltInCategory.OST_Walls).WhereElementIsNotElementType().WherePasses(new ADSK.ElementClassFilter(typeof(ADSK.Wall))).Cast<ADSK.Wall>().Where(w => w.CurtainGrid != null).Select(w => w.Id).ToArray();

            //generate a revit filter based on those ids
            ADSK.ElementFilter curtainWallIdFilter = new ADSK.ElementIdSetFilter(curtainWallIds);

            //collect the curtain walls using the filter
            return collector.OfCategory(ADSK.BuiltInCategory.OST_Walls).WherePasses(curtainWallIdFilter);
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
        {

            return Create.MakeHyparCurtainWallFromRevitCurtainWall(revitElement, document).ToArray();
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            return CurtainWallFromHypar(hyparElement, context);
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            var types = allElements.Select(e => e.GetType());
            var elemType = typeof(Elements.CurtainWall);
            return allElements.Where(e => e.GetType().FullName == typeof(Elements.CurtainWall).FullName).ToArray();
        }

        private static ADSK.ElementId[] CurtainWallFromHypar(Element hyparElement, LoadContext context)
        {
            var doc = context.Document;
            var hyparCurtainWall = hyparElement as CurtainWall;

            List<ADSK.ElementId> elementId = new List<ADSK.ElementId>();

            IList<ADSK.Curve> curves = new List<ADSK.Curve>();

            foreach (var seg in hyparCurtainWall.Profile.Perimeter.Segments())
            {
                var line = ADSK.Line.CreateBound(seg.Start.ToXYZ(true), seg.End.ToXYZ(true));
                curves.Add(line);
            }

            //our default types for now TODO: Relate this somehow to the actual types
            var firstCurtainWallType = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.WallType)).Cast<ADSK.WallType>().First(w => w.Kind == ADSK.WallKind.Curtain);
            var mullionType = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.MullionType))
                .Cast<ADSK.MullionType>().FirstOrDefault();

            //create the curtain wall by profile
            ADSK.Wall wallByProf = ADSK.Wall.Create(doc, curves, firstCurtainWallType.Id, ADSK.ElementId.InvalidElementId, false);

            //remove top constraint
            wallByProf.get_Parameter(ADSK.BuiltInParameter.WALL_HEIGHT_TYPE).Set(ADSK.ElementId.InvalidElementId);

            //set the height real nice
            var orderedCurves = curves.OrderBy(c => c.GetEndPoint(0).Z);
            var maxZ = Math.Max(orderedCurves.Last().GetEndPoint(0).Z, orderedCurves.Last().GetEndPoint(1).Z);
            var minZ = Math.Min(orderedCurves.First().GetEndPoint(0).Z, orderedCurves.First().GetEndPoint(1).Z);
            wallByProf.get_Parameter(ADSK.BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(maxZ - minZ);

            var levels = new ADSK.FilteredElementCollector(doc).OfCategory(ADSK.BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType().Cast<ADSK.Level>().OrderBy(l => Math.Abs(l.Elevation - minZ)).ToList();

            wallByProf.get_Parameter(ADSK.BuiltInParameter.WALL_BASE_CONSTRAINT).Set(levels.First().Id);
            wallByProf.get_Parameter(ADSK.BuiltInParameter.WALL_BASE_OFFSET).Set(minZ - levels.First().Elevation);

            
            doc.Regenerate();

            try
            {
                if (wallByProf.Orientation.DotProduct(Utilities.curveListNormal(curves.ToArray())) < 0)
                {
                    wallByProf.Flip();
                }
            }
            catch (Exception e)
            {
                //suppress flip
            }


            elementId.Add(wallByProf.Id);

            doc.Regenerate();
            List<ADSK.CurtainGridLine> gridLines = new List<ADSK.CurtainGridLine>();
            //add the us and v full grid lines
            foreach (var g in hyparCurtainWall.uGridlines)
            {
                var ln = g as Elements.Geometry.Line;
                try
                {
                    var newGrid = wallByProf.CurtainGrid.AddGridLine(true, ln.PointAt(0.5).ToXYZ(true), false);
                    gridLines.Add(newGrid);
                    elementId.Add(newGrid.Id);
                }
                catch (Exception)
                {
                    //skip it, that grid can't be added.
                }
            }
            foreach (var g in hyparCurtainWall.vGridlines)
            {
                var ln = g as Elements.Geometry.Line;
                try
                {
                    var newGrid = wallByProf.CurtainGrid.AddGridLine(false, ln.PointAt(0.5).ToXYZ(true), false);
                    gridLines.Add(newGrid);
                    elementId.Add(newGrid.Id);
                }
                catch (Exception)
                {
                    //skip it, that grid can't be added.
                }
            }

            doc.Regenerate();

            foreach (var gridLine in gridLines)
            {
                foreach (Line crv in hyparCurtainWall.SkippedSegments)
                {
                    ADSK.Line ln = ADSK.Line.CreateBound(crv.Start.ToXYZ(true), crv.End.ToXYZ(true));

                    try
                    {
                        gridLine.RemoveSegment(ln);
                    }
                    catch (Exception)
                    {
                        //
                    }
                }
            }
            //add the mullions
            foreach (var gridLine in gridLines)
            {
                foreach (ADSK.Curve c in gridLine.ExistingSegmentCurves)
                {
                    gridLine.AddMullions(c, mullionType, false);
                }
            }

            var panels = wallByProf.CurtainGrid.GetPanelIds().Select(id => doc.GetElement(id) as ADSK.Panel).OrderBy(p => p.Transform.Origin).ToList();

            //find a solid panel to st the spandrels as.
            foreach (var spandrelPanel in hyparCurtainWall.SpandrelPanels)
            {
                var panelData = spandrelPanel.Name.Split(',');

                var panelNumber = Convert.ToInt32(panelData[0]);
                var panelToUse = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.PanelType)).FirstOrDefault(p => p.Name.Equals(panelData[1])) as ADSK.PanelType;

                panels[panelNumber].PanelType = panelToUse;
            }
            foreach (var glazedPanel in hyparCurtainWall.GlazedPanels)
            {
                var panelData = glazedPanel.Name.Split(',');

                var panelNumber = Convert.ToInt32(panelData[0]);
                var panelToUse = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.PanelType)).FirstOrDefault(p => p.Name.Equals(panelData[1])) as ADSK.PanelType;

                panels[panelNumber].PanelType = panelToUse;
            }
            return elementId.ToArray();
        }
    }
}
