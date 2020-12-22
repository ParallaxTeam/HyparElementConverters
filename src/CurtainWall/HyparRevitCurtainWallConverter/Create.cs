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

            try
            {
                if (radius > 0)
                {
                    CurrentWidth = Units.FeetToMeters(Units.FeetToMeters(radius.Value));
                    return new Profile(new Circle(Units.FeetToMeters(radius.Value)).ToPolygon(10));
                }
                var side1 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH1).AsDouble();
                var side2 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH2).AsDouble();
                var thickness = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_THICK).AsDouble();
                CurrentWidth = Units.FeetToMeters(side1);
                return new Profile(Polygon.Rectangle(Units.FeetToMeters(side1 + side2), Units.FeetToMeters(thickness)));
            }
            catch (Exception)
            {
                return new Profile(Polygon.Rectangle(Units.FeetToMeters(0.25), Units.FeetToMeters(0.25)));
            }
        }


        public static Element[] MakeHyparCurtainWallFromRevitCurtainWall(Autodesk.Revit.DB.Element revitElement, ADSK.Document doc)
        {
            var curtainWall = revitElement as Autodesk.Revit.DB.Wall;

            //our lists to pack with hypar elements
            var mullions = new List<Mullion>();

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

                var vGridLines = curtainGrid.GetVGridLineIds().Select(id => doc.GetElement(id) as ADSK.CurtainGridLine).ToList();
                vGrids.AddRange(GenerateCurtainGridCurves(vGridLines));
                //generate interior mullions
                mullions.AddRange(GenerateInteriorMullions(revitGridLines));
            }

            //add perimeter mullions
            mullions.AddRange(GeneratePerimeterMullions(doc, curtainGrid));

            //get profile
            var curtainWallProfile = GetCurtainWallProfile(curtainWall);

            //add panels
            GeneratePanels(curtainGrid.GetCurtainCells().ToArray(), curtainGrid.GetPanelIds().Select(id => doc.GetElement(id) as ADSK.Panel).ToArray());

            CurtainWall hyparCurtainWall = new CurtainWall(curtainWallProfile, uGrids, vGrids,  mullions, SpandrelPanels, GlazedPanels, null, null, null, false, Guid.NewGuid(), "");

            return new List<Element>() { hyparCurtainWall }.ToArray();
        }

        private static Profile GetCurtainWallProfile(ADSK.Wall curtainWall)
        {
            var doc = curtainWall.Document;

            var modelCurveIds = curtainWall.GetDependentElements(new ADSK.ElementClassFilter(typeof(ADSK.CurveElement)));

            if (modelCurveIds.Any()) // if the wall's profile is edited, just get that
            {
                var modelCurves = modelCurveIds.Select(m => doc.GetElement(m)).Cast<ADSK.CurveElement>().ToList();

                var vertices = modelCurves.Select(m => m.GeometryCurve.GetEndPoint(0).ToVector3(true)).ToArray();

                return new Profile(new Polygon(vertices));
            }

            List<Vector3> verts = new List<Vector3>();
            var wallLoc = curtainWall.Location as ADSK.LocationCurve;
            var baseCurve = wallLoc.Curve.CreateTransformed(new ADSK.Transform(ADSK.Transform.CreateTranslation(new ADSK.XYZ(0, 0,
                curtainWall.get_Parameter(ADSK.BuiltInParameter.WALL_BASE_OFFSET).AsDouble()))));
            verts.Add(baseCurve.GetEndPoint(0).ToVector3(true)); //first point - bottom left
            var offsetCurve = baseCurve
                .CreateTransformed(new ADSK.Transform(ADSK.Transform.CreateTranslation(new ADSK.XYZ(0,0, curtainWall.LookupParameter("Unconnected Height").AsDouble()))));
            verts.Add(offsetCurve.GetEndPoint(0).ToVector3(true)); //second point - top left
            verts.Add(offsetCurve.GetEndPoint(1).ToVector3(true)); //third point - top right
            verts.Add(baseCurve.GetEndPoint(1).ToVector3(true)); //fourth point - bottom right
            return new Profile(new Polygon(verts));
        }

        private static Curve[] GenerateCurtainGridCurves(List<ADSK.CurtainGridLine> gridLines)
        {
            var modelCurves = new List<Curve>();

            foreach (var gridline in gridLines)
            {
                var line = new Line(gridline.FullCurve.GetEndPoint(0).ToVector3(true), gridline.FullCurve.GetEndPoint(1).ToVector3(true));
                    modelCurves.Add(line);
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
            string mullionTypeName = revitMullion.MullionType.Name;
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

            return new Mullion(null, DefaultMullionMaterial, new Representation(list), false, Guid.NewGuid(), mullionTypeName);
        }

        //here we generate spandrel panels and glazed panels
        private static void GeneratePanels(ADSK.CurtainCell[] curtainCells, ADSK.Panel[] revitPanels)
        {
            var doc = revitPanels.FirstOrDefault().Document;
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
                    var revmaterial = doc.GetElement(doc.GetElement(revitPanel.GetTypeId())
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
                    //var curves = cell.PlanarizedCurveLoops.ToPolyCurves();
                    var curves = cell.CurveLoops.ToPolyCurves();

                    //we serialize data as the name to keep it simple
                    string name = $"{i},{revitPanel.PanelType.Name}";

                    Panel panel = new Panel(new Polygon(curves.First().Vertices), material, null, null, false, Guid.NewGuid(), name);
                    
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

