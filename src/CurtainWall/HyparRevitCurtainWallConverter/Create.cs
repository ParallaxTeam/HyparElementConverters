using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Profiles;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitCurtainWallConverter
{
    public static class Create
    {
        private static ADSK.Document _doc;
        private static Elements.Material DefaultMullionMaterial => new Material("Aluminum", new Color(0.64f, 0.68f, 0.68f, 1));

        public static Element[] MakeHyparCurtainWallFromRevitCurtainWall(Autodesk.Revit.DB.Element revitElement, ADSK.Document doc)
        {
            _doc = doc;
            List<Element> newElements = new List<Element>();

            ADSK.Wall curtainWall = revitElement as ADSK.Wall;

            //check if it has a curtain grid
            if (curtainWall.CurtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }

            //add the mullions
            newElements.AddRange(GetMullions(curtainWall));

            //add the panels
            newElements.AddRange(GetPanels(curtainWall));

            return newElements.ToArray();
        }

        private static Profile GetCurtainWallProfile(ADSK.Wall curtainWall)
        {
            var doc = curtainWall.Document;

            var modelCurveIds = curtainWall.GetDependentElements(new ADSK.ElementClassFilter(typeof(ADSK.Sketch)));

            if (modelCurveIds.Any()) // if the wall's profile is edited, just get that
            {
                var sketch = modelCurveIds.Select(m => doc.GetElement(m)).Cast<ADSK.Sketch>().First();
                var polys = sketch.Profile.ToPolyCurves().First();

                return new Profile(new Polygon(polys.Vertices));
            }

            List<Vector3> verts = new List<Vector3>();
            var wallLoc = curtainWall.Location as ADSK.LocationCurve;
            var baseCurve = wallLoc.Curve.CreateTransformed(new ADSK.Transform(ADSK.Transform.CreateTranslation(new ADSK.XYZ(0, 0,
                curtainWall.get_Parameter(ADSK.BuiltInParameter.WALL_BASE_OFFSET).AsDouble()))));
            verts.Add(baseCurve.GetEndPoint(0).ToVector3(true)); //first point - bottom left
            var offsetCurve = baseCurve
                .CreateTransformed(new ADSK.Transform(ADSK.Transform.CreateTranslation(new ADSK.XYZ(0, 0, curtainWall.LookupParameter("Unconnected Height").AsDouble()))));
            verts.Add(offsetCurve.GetEndPoint(0).ToVector3(true)); //second point - top left
            verts.Add(offsetCurve.GetEndPoint(1).ToVector3(true)); //third point - top right
            verts.Add(baseCurve.GetEndPoint(1).ToVector3(true)); //fourth point - bottom right
            return new Profile(new Polygon(verts));
        }

        private static Dictionary<string,List<Panel>> GetPanels(ADSK.Wall curtainWall)
        {
            Dictionary<string, List<Panel>> dictionary = new Dictionary<string, List<Panel>>();
            var glazedPanels = new List<Panel>();
            var spandrelPanels = new List<Panel>();

            var cells = curtainWall.CurtainGrid.GetCurtainCells().ToArray();
            var panels = curtainWall.CurtainGrid.GetPanelIds().Select(id => _doc.GetElement(id) as ADSK.Panel).ToArray();

            for (int i = 0; i < cells.Length; i++)
            {
                var revitPanel = panels[i];

                bool isGlassPanel = true;

                Material material = BuiltInMaterials.Glass;
                try
                {
                    var revmaterial = _doc.GetElement(_doc.GetElement(revitPanel.GetTypeId())
                        .get_Parameter(ADSK.BuiltInParameter.MATERIAL_ID_PARAM).AsElementId()) as ADSK.Material;
                    material = revmaterial.ToElementsMaterial();
                    isGlassPanel = revmaterial.Transparency > 0;
                }
                catch (Exception)
                {
                    material = BuiltInMaterials.Glass;
                }

                var cell = cells[i];

                try
                {
                    //var curves = cell.PlanarizedCurveLoops.ToPolyCurves();
                    var curves = cell.CurveLoops.ToPolyCurves();

                    //we serialize data as the name to keep it simple
                    string name = $"{i},{revitPanel.PanelType.Name},{isGlassPanel}";

                    if (!isGlassPanel)
                    {
                        spandrelPanels.Add(new Panel(new Polygon(curves.First().Vertices), material, null, null, false, Guid.NewGuid(), name));
                    }
                    else
                    {
                        glazedPanels.Add(new Panel(new Polygon(curves.First().Vertices), material, null, null, false, Guid.NewGuid(), name));
                    }
                }
                catch (Exception)
                {
                    //suppress for now
                }
            }

            dictionary.Add("spandrel",spandrelPanels);
            dictionary.Add("glazed",glazedPanels);

            return dictionary;
        }

        private static List<Element> GetMullions(ADSK.Wall curtainWall)
        {
            List<Element> newElements = new List<Element>();

            var curtainGrid = curtainWall.CurtainGrid;

            //get the revit grid lines for use
            var curtainGridLineIds = new List<ADSK.ElementId>();
            curtainGridLineIds.AddRange(curtainGrid.GetUGridLineIds());
            curtainGridLineIds.AddRange(curtainGrid.GetVGridLineIds());
            var revitGridLines = curtainGridLineIds.Select(id => _doc.GetElement(id) as ADSK.CurtainGridLine).ToList();

            foreach (var gridLine in revitGridLines)
            {
                var profile = gridLine.AttachedMullions().First().GetMullionProfile();

                foreach (ADSK.Line existingSegment in gridLine.ExistingSegmentCurves)
                {
                    //calculate angle to make it perpendicular to face
                    var angle = existingSegment.Direction.CrossProduct(Utilities.GetWallDirection(curtainWall));
                    var dbl = angle.AngleTo(ADSK.XYZ.BasisX) / Math.PI * 180;

                    Line line = new Line(existingSegment.GetEndPoint(0).ToVector3(true),
                        existingSegment.GetEndPoint(1).ToVector3(true));

                    Beam mullion =
                        new Beam(line, profile, DefaultMullionMaterial,0,0, dbl);

                    newElements.Add(mullion);
                }

            }
            return newElements;
        }

        private static Profile GetMullionProfile(this ADSK.Mullion revitMullion)
        {
            var radius = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.CIRC_MULLION_RADIUS)?.AsDouble();
            try
            {
                if (radius > 0)
                {
                    return new Profile(new Circle(Units.FeetToMeters(radius.Value)).ToPolygon(10));
                }
                var side1 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH1).AsDouble();
                var side2 = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_WIDTH2).AsDouble();
                var thickness = revitMullion.MullionType.get_Parameter(ADSK.BuiltInParameter.RECT_MULLION_THICK).AsDouble();

                return new Profile(Polygon.Rectangle(Units.FeetToMeters(side1 + side2), Units.FeetToMeters(thickness)));
            }
            catch (Exception)
            {
                return new Profile(Polygon.Rectangle(Units.FeetToMeters(0.25), Units.FeetToMeters(0.25)));
            }
        }
    }
}
