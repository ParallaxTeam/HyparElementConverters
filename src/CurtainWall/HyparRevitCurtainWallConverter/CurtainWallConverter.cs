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


            bool asRevitElements = hyparCurtainWall.uGridlines.Any() && hyparCurtainWall.vGridlines.Any();

            if (!asRevitElements)
            {
                return DirectShapesFromCurtainWall(hyparCurtainWall, context);
            }


            //our default types for now TODO: Relate this somehow to the actual types
            var firstCurtainWallType = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.WallType)).Cast<ADSK.WallType>().First(w => w.Kind == ADSK.WallKind.Curtain);
            var mullionType = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.MullionType))
                .Cast<ADSK.MullionType>().FirstOrDefault();

            //add all the panels to one list
            var allPanels = new List<Panel>();
            allPanels.AddRange(hyparCurtainWall.GlazedPanels);
            allPanels.AddRange(hyparCurtainWall.SpandrelPanels);

            List<ADSK.ElementId> returnElementIds = new List<ADSK.ElementId>();

            IList<ADSK.Curve> curves = new List<ADSK.Curve>();
            foreach (var seg in hyparCurtainWall.Profile.Perimeter.Segments())
            {
                var line = ADSK.Line.CreateBound(seg.Start.ToXYZ(true), seg.End.ToXYZ(true));
                curves.Add(line);
            }

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
            catch (Exception)
            {
                //suppress flip
            }

            returnElementIds.Add(wallByProf.Id);

            doc.Regenerate();

            List<ADSK.CurtainGridLine> gridLines = new List<ADSK.CurtainGridLine>();
            //add the u and v full grid lines
            foreach (var g in hyparCurtainWall.uGridlines)
            {
                var ln = g as Line;
                try
                {
                    var newGrid = wallByProf.CurtainGrid.AddGridLine(true, ln.PointAt(0.5).ToXYZ(true), false);
                    gridLines.Add(newGrid);
                    returnElementIds.Add(newGrid.Id);
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
                    returnElementIds.Add(newGrid.Id);
                }
                catch (Exception)
                {
                    //skip it, that grid can't be added.
                }
            }

            //remove all the segments to start fresh
            foreach (var g in gridLines)
            {
                if (g.AllSegmentCurves.Size == 0)
                {
                    continue;
                }
                foreach (ADSK.Curve s in g.AllSegmentCurves)
                {
                    g.RemoveSegment(s);
                }
            }

            //try to add back the lines based on panels
            const double epsilon = 0.1;
            foreach (var p in allPanels)
            {
                foreach (var s in p.Perimeter.Segments())
                {
                    ADSK.Curve curve = s.ToRevitCurve(true);
                    try
                    {
                        var curtainGridLine = gridLines
                            .First(g => g.FullCurve.Distance(curve.GetEndPoint(0)) < epsilon && g.FullCurve.Distance(curve.GetEndPoint(1)) < epsilon);

                        curtainGridLine.AddSegment(curve);
                    }
                    catch (Exception)
                    {
                        //suppress for now
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

            var panels = wallByProf.CurtainGrid.GetPanelIds().Select(id => doc.GetElement(id) as ADSK.Panel).ToList();

            //build panels with index and matching type (if available)
            foreach (var panel in allPanels)
            {
                var panelData = panel.Name.Split(',');
                var panelNumber = Convert.ToInt32(panelData[0]);
                var panelToUse = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.PanelType)).FirstOrDefault(p => p.Name.Equals(panelData[1])) as ADSK.PanelType;
                panels[panelNumber].PanelType = panelToUse;
            }


            return returnElementIds.ToArray();
        }




        private static ADSK.ElementId[] DirectShapesFromCurtainWall(CurtainWall hyparCurtainWall, LoadContext context)
        {
            List<ADSK.ElementId> newStuff = new List<ADSK.ElementId>();
            foreach (var panel in hyparCurtainWall.GlazedPanels)
            {
                var curves = panel.Perimeter.Segments().Select(s => s.ToRevitCurve(true));
                var profileLoop = ADSK.CurveLoop.Create(curves.ToList());
                var profileLoops = new List<ADSK.CurveLoop> { profileLoop };
                var solid = ADSK.GeometryCreationUtilities.CreateExtrusionGeometry(profileLoops, panel.Normal().ToXYZ(true), 1);

                var elemId = new ADSK.ElementId(ADSK.BuiltInCategory.OST_CurtainWallPanels);
                var ds = ADSK.DirectShape.CreateElement(context.Document, elemId);
                ds.SetShape(new[] { solid }, ADSK.DirectShapeTargetViewType.Default);
                
                newStuff.Add(elemId);
            }

            return newStuff.ToArray();
        }
    }
}
