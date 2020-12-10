using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Creation;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Profiles;
using Elements.Geometry.Solids;
using Elements.Properties;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitCurtainWallConverter
{
    public static class Create
    {
        private static Profile DefaultMullionProfile()
        {
            return new Profile(Polygon.Rectangle(0.0635, 0.0635));
        }
        private static Elements.Material DefaultMullionMaterial => new Material("Aluminum", new Color(0.64f, 0.68f, 0.68f, 1));

        public static Element[] MakeHyparCurtainWallFromRevitCurtainWall(Autodesk.Revit.DB.Element revitElement, ADSK.Document doc)
        {
            var curtainWall = revitElement as Autodesk.Revit.DB.Wall;

            //our lists to pack with hypar elements
            var mullions = new List<Mullion>();

            //right now we are targeting curtain walls with stuff in em
            if (curtainWall.CurtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }

            var curtainCells = curtainWall.CurtainGrid.GetCurtainCells().ToArray();

            //curtainWallElements.AddRange(PanelAreasFromCells(curtainCells));

            //generate interior mullions
            mullions.AddRange(MullionFromCurtainGrid(doc, curtainWall.CurtainGrid));


            CurtainWall hyparCurtainWall = new CurtainWall(null,null, mullions,null,null,null,null,null,false, Guid.NewGuid(),"");

            return new List<Element>(){ hyparCurtainWall }.ToArray();
        }

        private static Mullion[] MullionFromCurtainGrid(ADSK.Document doc, ADSK.CurtainGrid curtainGrid)
        {
            List<Mullion> mullions = new List<Mullion>();

            if (!curtainGrid.GetMullionIds().Any())
            {
                throw new InvalidOperationException($"There are no mullions on this curtain wall.");
            }

            var allMullions = curtainGrid.GetMullionIds().Select(id => doc.GetElement(id) as ADSK.Mullion);

            foreach (ADSK.Mullion mullion in allMullions)
            {
                mullion.get_Geometry()
                mullions.Add(mullion.ToHyparMullion());
            }

            return mullions.ToArray();
        }

        //private static Mullion[] MullionFromCurtainGrid(ADSK.Document doc, ADSK.CurtainGrid curtainGrid, bool uDirection = true)
        //{
        //    List<Mullion> mullions = new List<Mullion>();

        //    var gridIds = uDirection ? curtainGrid.GetUGridLineIds() : curtainGrid.GetVGridLineIds();

        //    if (!gridIds.Any())
        //    {
        //        var direction = uDirection ? "u" : "v";
        //        throw new InvalidOperationException($"There are no gridlines in the {direction} direction.");
        //    }

        //    var gridLines = gridIds
        //            .Select(c => doc.GetElement(c) as Autodesk.Revit.DB.CurtainGridLine).ToArray();

        //    foreach (var gridLine in gridLines)
        //    {
        //        var revitMullions = gridLine.GetDependentElements(
        //            new ADSK.ElementCategoryFilter(ADSK.BuiltInCategory.OST_CurtainWallMullions)).Select(m => doc.GetElement(m) as ADSK.Mullion);

        //        foreach (var revitMullion in revitMullions)
        //        {
        //            mullions.Add(revitMullion.ToHyparMullion());
        //        }
        //    }

        //    return mullions.ToArray();
        //}

        private static Mullion ToHyparMullion(this ADSK.Mullion revitMullion)
        {
            var side1 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH1).AsDouble();
            var side2 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH2).AsDouble();
            var thickness = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_THICK).AsDouble();

            var profile =
                new Profile(Polygon.Rectangle(Units.FeetToMeters(thickness), Units.FeetToMeters(side1 + side2)));

            var mullionCurve = revitMullion.LocationCurve;

            //TODO: Make sure these mullions get oriented correctly. Working kinda sorta right now.
            Line centerLine = new Line(mullionCurve.GetEndPoint(0).ToVector3(true), mullionCurve.GetEndPoint(1).ToVector3(true));
            var transform = centerLine.TransformAt(0);
            var transFormedProf = transform.OfProfile(profile);

            //build a sweep with the default profile
            List<SolidOperation> list = new List<SolidOperation>
            {
                new Extrude(transFormedProf,centerLine.Length(),centerLine.Direction(),false)
                //new Sweep(transFormedProf,centerLine,0,0,false)
            };
            return new Mullion(new Transform(), DefaultMullionMaterial, new Representation(list), false, Guid.NewGuid(), null);
        }

        private static Element[] PanelAreasFromCells(ADSK.CurtainCell[] curtainCells)
        {
            var panels = new List<Element>();
            foreach (var cell in curtainCells)
            {
                var enumCurveLoops = cell.CurveLoops.GetEnumerator();
                for (; enumCurveLoops.MoveNext();)
                {
                    var vertices = new List<Vector3>();
                    var crvArr = (ADSK.CurveArray)enumCurveLoops.Current;
                    var enumCurves = crvArr.GetEnumerator();
                    for (; enumCurves.MoveNext();)
                    {
                        var crv = (Autodesk.Revit.DB.Curve)enumCurves.Current;

                        vertices.Add(crv.GetEndPoint(0).ToVector3());
                    }

                    PanelArea panel = new PanelArea(new Polygon(vertices),null,BuiltInMaterials.Black,null,false,Guid.NewGuid(),null);
                    panels.Add(panel);
                }
            }
            
            return panels.ToArray();
        }
        private static IEnumerable<ADSK.Solid> GetSolidsFromGeometry(ADSK.GeometryObject g)
        {
            IEnumerable<ADSK.Solid> solids;
            if ((object)(g as ADSK.GeometryInstance) == (object)null)
            {
                solids = (!(g is ADSK.Solid) ? new ADSK.Solid[0] : new ADSK.Solid[] { g as ADSK.Solid });
            }
            else
            {
                solids = (
                    from s in ((ADSK.GeometryInstance)g).GetInstanceGeometry()
                    where s.GetType() == typeof(ADSK.Solid)
                    select s).Cast<ADSK.Solid>();
            }
            return solids;
        }
    }
}

