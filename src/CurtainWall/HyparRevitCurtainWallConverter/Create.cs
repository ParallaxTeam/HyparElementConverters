using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Solids;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitCurtainWallConverter
{
    public static class Create
    {
        public static List<ADSK.ElementId> InteriorMullionIds = new List<ADSK.ElementId>();

        private static Elements.Material DefaultMullionMaterial => new Material("Aluminum", new Color(0.64f, 0.68f, 0.68f, 1));

        public static Element[] MakeHyparCurtainWallFromRevitCurtainWall(Autodesk.Revit.DB.Element revitElement, ADSK.Document doc)
        {
            var curtainWall = revitElement as Autodesk.Revit.DB.Wall;

            //our lists to pack with hypar elements
            var mullions = new List<Mullion>();
            var curtainGridLines = new List<ModelCurve>();
            var glazedPanels = new List<Panel>();
            //right now we are targeting curtain walls with stuff in em
            var curtainGrid = curtainWall.CurtainGrid;
            if (curtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }
            //get the revit grid lines for use
            var curtainGridLineIds = new List<ADSK.ElementId>();
            curtainGridLineIds.AddRange(curtainGrid.GetUGridLineIds());
            curtainGridLineIds.AddRange(curtainGrid.GetVGridLineIds());
            var revitGridLines = curtainGridLineIds.Select(id => doc.GetElement(id) as ADSK.CurtainGridLine).ToList();

            //get profile
            var curtainWallProfile = GetCurtainWallProfile(curtainWall);

            if (curtainGridLineIds.Any())
            {
                //generate curtain grid
                curtainGridLines.AddRange(GenerateCurtainGridCurves(revitGridLines));

                //generate interior mullions
                mullions.AddRange(MullionFromCurtainGrid(revitGridLines));
            }

            glazedPanels.AddRange(PanelsFromCells(curtainGrid.GetCurtainCells().ToArray(), curtainGrid.GetPanelIds().Select(id => doc.GetElement(id) as ADSK.Panel).ToArray()));

            CurtainWall hyparCurtainWall = new CurtainWall(curtainWallProfile, curtainGridLines, mullions, glazedPanels, null, null, null, null, false, Guid.NewGuid(), "");

            return new List<Element>() { hyparCurtainWall }.ToArray();
        }

        private static Profile GetCurtainWallProfile(ADSK.Wall curtainWall)
        {
            Polygon outerPolygon = null;
            List<Polygon> voids = new List<Polygon>();

            var polygons = curtainWall.GetProfile();
            if (polygons == null)
            {
                return null;
            }

            outerPolygon = polygons[0];
            if (polygons.Count > 1)
            {
                voids.AddRange(polygons.Skip(1));
            }

            //build our profile
            return new Profile(outerPolygon, voids, Guid.NewGuid(), null);
        }

        private static ModelCurve[] GenerateCurtainGridCurves(List<ADSK.CurtainGridLine> gridLines)
        {
            var modelCurves = new List<ModelCurve>();

            foreach (var gridline in gridLines)
            {
                foreach (ADSK.Curve curve in gridline.ExistingSegmentCurves)
                {
                    var line = new Line(curve.GetEndPoint(0).ToVector3(true), curve.GetEndPoint(1).ToVector3(true));
                    modelCurves.Add(new ModelCurve(line));
                }
            }

            return modelCurves.ToArray();
        }
        private static Mullion[] MullionFromCurtainGrid(List<ADSK.CurtainGridLine> gridLines)
        {
            List<Mullion> mullions = new List<Mullion>();

            foreach (var gridLine in gridLines)
            {
                var attachedMullions = gridLine.AttachedMullions();
                if (!attachedMullions.Any()) continue;

                foreach (var mullion in attachedMullions)
                {
                    mullions.Add(mullion.ToHyparMullion());
                }

            }
            return mullions.ToArray();
        }

        private static Mullion ToHyparMullion(this ADSK.Mullion revitMullion)
        {
            var side1 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH1).AsDouble();
            var side2 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH2).AsDouble();
            var thickness = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_THICK).AsDouble();

            Profile profile = new Profile(Polygon.Rectangle(Units.FeetToMeters(side1 + side2), Units.FeetToMeters(thickness)));

            var mullionCurve = revitMullion.LocationCurve;

            //TODO: Make sure these mullions get oriented correctly. Working kinda sorta right now.
            Line centerLine = new Line(mullionCurve.GetEndPoint(0).ToVector3(true),
                mullionCurve.GetEndPoint(1).ToVector3(true));

            var transProf = centerLine.TransformAt(0).OfProfile(profile);

            //build a sweep with the default profile
            List<SolidOperation> list = new List<SolidOperation>
            {
                new Extrude(transProf, centerLine.Length(), centerLine.Direction(), false)
                //new Sweep(profile,centerLine,0,0,false)
            };
            return new Mullion(null, DefaultMullionMaterial, new Representation(list), false, Guid.NewGuid(), null);
        }

        private static Panel[] PanelsFromCells(ADSK.CurtainCell[] curtainCells, ADSK.Panel[] revitPanels)
        {
            var panels = new List<Panel>();

            for (int i = 0; i < curtainCells.Length; i++)
            {
                var revitPanel = revitPanels[i];
                Material material = null;
                try
                {
                    var revmaterial = revitPanel.Document.GetElement(revitPanel.Document.GetElement(revitPanel.GetTypeId())
                        .get_Parameter(ADSK.BuiltInParameter.MATERIAL_ID_PARAM).AsElementId()) as ADSK.Material;
                    material = revmaterial.ToElementsMaterial();
                }
                catch (Exception)
                {
                    material = BuiltInMaterials.Glass;
                }
                
                var cell = curtainCells[i];

                try
                {
                    var curves = cell.CurveLoops.ToPolyCurves();
                    Panel panel = new Panel(new Polygon(curves.First().Vertices), material, null, null, false, Guid.NewGuid(), null);
                    panels.Add(panel);
                }
                catch (Exception e)
                {
                    //suppress for now
                }
            }

            return panels.ToArray();
        }
    }
}

