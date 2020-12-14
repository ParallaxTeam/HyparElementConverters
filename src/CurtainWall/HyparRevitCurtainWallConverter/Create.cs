using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.Electrical;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Profiles;
using Elements.Geometry.Solids;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitCurtainWallConverter
{
    public static class Create
    {
        public static List<ADSK.ElementId> InteriorMullionIds = new List<ADSK.ElementId>();
        public static List<Panel> GlazedPanels = new List<Panel>();
        public static List<Panel> SpandrelPanels = new List<Panel>();
        private static double CurrentWidth { get; set; } = 0;
        private static Elements.Material DefaultMullionMaterial => new Material("Aluminum", new Color(0.64f, 0.68f, 0.68f, 1));

        private static Profile GetMullionProfile(ADSK.Mullion revitMullion)
        {
            var radius = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.CIRC_MULLION_RADIUS)?.AsDouble();

            switch (radius)
            {
                case null:
                    var side1 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH1).AsDouble();
                    var side2 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH2).AsDouble();
                    var thickness = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_THICK).AsDouble();
                    CurrentWidth = Units.FeetToMeters(side1);
                    return new Profile(Polygon.Rectangle(Units.FeetToMeters(side1 + side2), Units.FeetToMeters(thickness)));
                default:
                    CurrentWidth = Units.FeetToMeters(Units.FeetToMeters(radius.Value));
                    return new Profile(new Circle(Units.FeetToMeters(radius.Value)).ToPolygon(10));
            }
        }


        public static Element[] MakeHyparCurtainWallFromRevitCurtainWall(Autodesk.Revit.DB.Element revitElement, ADSK.Document doc)
        {
            var curtainWall = revitElement as Autodesk.Revit.DB.Wall;

            //our lists to pack with hypar elements
            var interiorMullions = new List<Mullion>();
            var perimeterMullions = new List<Mullion>();
            var uGrids = new List<Curve>();
            var vGrids = new List<Curve>();
            var skippedSegments = new List<Curve>();

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

            if (curtainGridLineIds.Any())
            {
                //generate curtain grid
                var uGridLines = curtainGrid.GetUGridLineIds().Select(id => doc.GetElement(id) as ADSK.CurtainGridLine).ToList();
                uGrids.AddRange(GenerateCurtainGridCurves(uGridLines));
                skippedSegments.AddRange(GenerateCurtainGridCurves(uGridLines,true));

                var vGridLines = curtainGrid.GetVGridLineIds().Select(id => doc.GetElement(id) as ADSK.CurtainGridLine).ToList();
                vGrids.AddRange(GenerateCurtainGridCurves(vGridLines));
                skippedSegments.AddRange(GenerateCurtainGridCurves(vGridLines, true));
                //generate interior mullions
                interiorMullions.AddRange(GenerateInteriorMullions(revitGridLines));
            }

            //add perimeter mullions
            perimeterMullions.AddRange(GeneratePerimeterMullions(doc, curtainGrid));

            //get profile
            var curtainWallProfile = GetCurtainWallProfile(curtainWall);

            //add panels
            GeneratePanels(curtainGrid.GetCurtainCells().ToArray(), curtainGrid.GetPanelIds().Select(id => doc.GetElement(id) as ADSK.Panel).ToArray());

            CurtainWall hyparCurtainWall = new CurtainWall(curtainWallProfile, uGrids, vGrids, skippedSegments, interiorMullions, perimeterMullions, SpandrelPanels, GlazedPanels, null, null, null, false, Guid.NewGuid(), "");

            return new List<Element>() { hyparCurtainWall }.ToArray();
        }

        private static Profile GetCurtainWallProfile(ADSK.Wall curtainWall)
        {
            var doc = curtainWall.Document;

            var normalWall = new ADSK.FilteredElementCollector(doc).OfClass(typeof(ADSK.WallType)).Cast<ADSK.WallType>().First(w => w.Kind == ADSK.WallKind.Basic);

            List<Polygon> polygons = null;

            //to get a curtain wall's profile, we change the type temporarily. #thanksRevit
            using (ADSK.TransactionGroup t = new ADSK.TransactionGroup(doc, "Temp change wall"))
            {
                t.Start();
                ADSK.Transaction changeWall = new ADSK.Transaction(doc, "Changing wall");
                changeWall.Start();
                //disallow join to get an accurate profile
                ADSK.WallUtils.DisallowWallJoinAtEnd(curtainWall,0);
                ADSK.WallUtils.DisallowWallJoinAtEnd(curtainWall, 1);
                curtainWall.WallType = normalWall;
                changeWall.Commit();
                polygons = curtainWall.GetProfile();
                t.RollBack();
            }

            Polygon outerPolygon = null;
            List<Polygon> voids = new List<Polygon>();

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

        private static Curve[] GenerateCurtainGridCurves(List<ADSK.CurtainGridLine> gridLines, bool segments = false)
        {
            var modelCurves = new List<Curve>();

            foreach (var gridline in gridLines)
            {
                if (segments)
                {
                    foreach (ADSK.Curve seg in gridline.SkippedSegmentCurves)
                    {
                        var line = new Line(seg.GetEndPoint(0).ToVector3(true), seg.GetEndPoint(1).ToVector3(true));
                        modelCurves.Add(line);
                    }
                }
                else
                {
                    var line = new Line(gridline.FullCurve.GetEndPoint(0).ToVector3(true), gridline.FullCurve.GetEndPoint(1).ToVector3(true));
                    modelCurves.Add(line);
                }
            }
            return modelCurves.ToArray();
        }
        private static Mullion[] GeneratePerimeterMullions(ADSK.Document doc, ADSK.CurtainGrid curtainGrid)
        {
            List<Mullion> mullions = new List<Mullion>();

            foreach (var id in curtainGrid.GetMullionIds())
            {
                if (InteriorMullionIds.Contains(id)) continue;

                var mullion = doc.GetElement(id) as ADSK.Mullion;

                mullions.Add(mullion.ToHyparMullion(CurrentWidth));
            }
            return mullions.ToArray();
        }

        //this gets the interior mullions from grid lines
        private static Mullion[] GenerateInteriorMullions(List<ADSK.CurtainGridLine> gridLines)
        {
            InteriorMullionIds.Clear();

            List<Mullion> mullions = new List<Mullion>();

            foreach (var gridLine in gridLines)
            {
                var attachedMullions = gridLine.AttachedMullions();
                if (!attachedMullions.Any()) continue;

                foreach (var mullion in attachedMullions)
                {
                    InteriorMullionIds.Add(mullion.Id);

                    mullions.Add(mullion.ToHyparMullion());
                }
            }
            return mullions.ToArray();
        }

        private static Mullion ToHyparMullion(this ADSK.Mullion revitMullion, double offset = 0)
        {
            Profile prof = GetMullionProfile(revitMullion);

            ADSK.Line mullionCurve = revitMullion.LocationCurve as ADSK.Line;

            Line centerLine = new Line(mullionCurve.GetEndPoint(0).ToVector3(true),
                mullionCurve.GetEndPoint(1).ToVector3(true));

            //get the transform from the mullion so we can orient the profile
            var tForm = revitMullion.GetTransform().ToElementsTransform(true);

            //this kinda sucks but it works okay for the perimeter mullions
            if (offset > 0)
            {
                var orig = tForm.YZ().Normal;
                tForm.Move(orig.X * -offset, orig.Y * -offset, orig.Z * -offset);
            }

            //transform the profile
            var transProfile = tForm.OfProfile(prof);

            //build a sweep with the default profile
            List<SolidOperation> list = new List<SolidOperation>
            {
                new Extrude(transProfile, centerLine.Length(), centerLine.Direction(), false)
            };

            return new Mullion(null, DefaultMullionMaterial, new Representation(list), false, Guid.NewGuid(), null);
        }
        //here we generate spandrel panels and glazed panels
        private static void GeneratePanels(ADSK.CurtainCell[] curtainCells, ADSK.Panel[] revitPanels)
        {
            //clear our lists
            GlazedPanels.Clear();
            SpandrelPanels.Clear();

            for (int i = 0; i < curtainCells.Length; i++)
            {
                var revitPanel = revitPanels[i];

                bool isGlassPanel = true;

                Material material;
                try
                {
                    var revmaterial = revitPanel.Document.GetElement(revitPanel.Document.GetElement(revitPanel.GetTypeId())
                        .get_Parameter(ADSK.BuiltInParameter.MATERIAL_ID_PARAM).AsElementId()) as ADSK.Material;
                    material = revmaterial.ToElementsMaterial();
                    isGlassPanel = revmaterial.Transparency > 0;
                }
                catch (Exception)
                {
                    material = BuiltInMaterials.Glass;
                }

                var cell = curtainCells[i];

                try
                {
                    var curves = cell.PlanarizedCurveLoops.ToPolyCurves();
                    Panel panel = new Panel(new Polygon(curves.First().Vertices), material, null, null, false, Guid.NewGuid(), $"panel-{i}");
                    if (isGlassPanel)
                    {
                        GlazedPanels.Add(panel);
                    }
                    else
                    {
                        SpandrelPanels.Add(panel);
                    }
                }
                catch (Exception)
                {
                    //suppress for now
                }
            }
        }
    }
}

