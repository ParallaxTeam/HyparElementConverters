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

            //right now we are targeting curtain walls with stuff in em
            if (curtainWall.CurtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }

            var curtainCells = curtainWall.CurtainGrid.GetCurtainCells().ToArray();

            //curtainWallElements.AddRange(PanelAreasFromCells(curtainCells));

            //generate interior mullions
            mullions.AddRange(MullionFromCurtainGrid(doc, curtainWall.CurtainGrid));


            CurtainWall hyparCurtainWall = new CurtainWall(null, null, mullions, null, null, null, null, null, false, Guid.NewGuid(), "");

            return new List<Element>() { hyparCurtainWall }.ToArray();
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

        private static Mullion[] GetInteriorMullions(ADSK.Document doc, ADSK.CurtainGrid curtainGrid)
        {
            InteriorMullionIds.Clear();
            List<Mullion> mullions = new List<Mullion>();

            var gridIds = new List<ADSK.ElementId>();
            gridIds.AddRange(curtainGrid.GetUGridLineIds());
            gridIds.AddRange(curtainGrid.GetVGridLineIds());

            var gridLines = gridIds
                    .Select(c => doc.GetElement(c) as Autodesk.Revit.DB.CurtainGridLine).ToArray();

            //this uses a transaction to find the inner mullions.
            using (ADSK.Transaction findMullionTransaction = new ADSK.Transaction(doc, "Finding matching mullion."))
            {
                findMullionTransaction.Start();
                ADSK.FailureHandlingOptions failOpt = findMullionTransaction.GetFailureHandlingOptions();
                failOpt.SetFailuresPreprocessor(new MullionFinder());
                findMullionTransaction.SetFailureHandlingOptions(failOpt);
                foreach (var gridLine in gridLines)
                {
                    foreach (ADSK.Curve segment in gridLine.ExistingSegmentCurves)
                    {
                        gridLine.RemoveSegment(segment);
                    }

                }
                findMullionTransaction.Commit();
            }

            if (!InteriorMullionIds.Any())
            {
                throw new InvalidOperationException($"There are no interior mullions on this curtain wall.");
            }

            foreach (var id in InteriorMullionIds)
            {
                var revitMullion = doc.GetElement(id) as ADSK.Mullion;
                mullions.Add(revitMullion.ToHyparMullion());
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
            

            var profiles = faces.OrderBy(f => f.Origin.DistanceTo(revitMullion.LocationCurve.GetEndPoint(0))).First().GetProfiles(true);

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

