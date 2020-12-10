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
using Elements.Validators;
using HyparRevitCurtainWallConverter.Properties;
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
            //right now we are targeting curtain walls with stuff in em
            if (curtainWall.CurtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }

            //generate interior mullions
            //mullions.AddRange(MullionFromCurtainGrid(doc, curtainWall.CurtainGrid));

            //generate curtain grid
            curtainGridLines.AddRange(GenerateCurtainGridCurves(doc,curtainWall.CurtainGrid));

            //get profile
            var curtainWallProfile = GetCurtainWallProfile(curtainWall);

            CurtainWall hyparCurtainWall = new CurtainWall(curtainWallProfile, curtainGridLines, mullions, null, null, null, null, null, false, Guid.NewGuid(), "");

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

        private static ModelCurve[] GenerateCurtainGridCurves(ADSK.Document doc, ADSK.CurtainGrid curtainGrid)
        {
            var modelCurves = new List<ModelCurve>();
            var curtainGridLineIds = new List<ADSK.ElementId>();
            curtainGridLineIds.AddRange(curtainGrid.GetUGridLineIds());
            curtainGridLineIds.AddRange(curtainGrid.GetVGridLineIds());

            if (!curtainGridLineIds.Any())
            {
                throw new InvalidOperationException($"There are curtain grids in this curtain wall.");
            }

            foreach (var gridline in curtainGridLineIds.Select(id => doc.GetElement(id) as ADSK.CurtainGridLine))
            {
                foreach (ADSK.Curve curve in gridline.ExistingSegmentCurves)
                {
                    var line = new Line(curve.GetEndPoint(0).ToVector3(true), curve.GetEndPoint(1).ToVector3(true));
                    modelCurves.Add(new ModelCurve(line));
                }
            }

            return modelCurves.ToArray();
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
                mullions.Add(mullion.ToHyparMullion());
            }

            return mullions.ToArray();
        }

        private static Mullion ToHyparMullion(this ADSK.Mullion revitMullion)
        {
            List<ADSK.PlanarFace> faces = new List<ADSK.PlanarFace>();
            IEnumerable<ADSK.Solid> solids = revitMullion.get_Geometry(new ADSK.Options()).SelectMany<ADSK.GeometryObject, ADSK.Solid>((ADSK.GeometryObject g) => GetSolidsFromGeometry(g)); ;

            foreach (ADSK.Solid solid in solids)
            {
                foreach (ADSK.Face face in solid.Faces)
                {
                    ADSK.PlanarFace planarFace = face as ADSK.PlanarFace;
                    faces.Add(planarFace);
                }
            }

            var orderedFaces = faces.OrderBy(f => f.Area);

            var profiles = orderedFaces.First().GetProfiles(true);

            Profile profile = null;
            try
            {
                profile = profiles.Aggregate<Profile>((Profile p1, Profile p2) => p1.Union(p2, 1E-05));
            }
            catch
            {
                profile = (
                    from p in profiles
                    orderby p.Perimeter.Start.Z
                    select p).First<Profile>();
            }

            var mullionCurve = revitMullion.LocationCurve;
            

            //TODO: Make sure these mullions get oriented correctly. Working kinda sorta right now.
            Line centerLine = new Line(mullionCurve.GetEndPoint(0).ToVector3(true), mullionCurve.GetEndPoint(1).ToVector3(true));

            //build a sweep with the default profile
            List <SolidOperation> list = new List<SolidOperation>
            {
                new Extrude(profile, centerLine.Length(), centerLine.Direction(), false)
                //new Sweep(profile,centerLine,0,0,false)
            };
            return new Mullion(null, DefaultMullionMaterial, new Representation(list), false, Guid.NewGuid(), null);
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

                    PanelArea panel = new PanelArea(new Polygon(vertices), null, BuiltInMaterials.Black, null, false, Guid.NewGuid(), null);
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

