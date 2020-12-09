using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Creation;
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

            //curtain wall elements

            var curtainWallElements = new List<Element>();

            if (curtainWall.CurtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }

            //add the U direction mullions
            curtainWallElements.AddRange(MullionFromCurtainGrid(doc, curtainWall.CurtainGrid));
            //add the V direction mullions
            curtainWallElements.AddRange(MullionFromCurtainGrid(doc, curtainWall.CurtainGrid, false));

            //Elements.CurtainWallPanel hyparCurtainWallPanel = new CurtainWallPanel(curtainWallMullions,null,null);



            return curtainWallElements.ToArray();
        }

        private static Element[] MullionFromCurtainGrid(ADSK.Document doc, ADSK.CurtainGrid curtainGrid, bool uDirection = true)
        {
            List<Element> mullions = new List<Element>();

            var gridIds = uDirection ? curtainGrid.GetUGridLineIds() : curtainGrid.GetVGridLineIds();

            if (!gridIds.Any())
            {
                var direction = uDirection ? "u" : "v";
                throw new InvalidOperationException($"There are no gridlines in the {direction} direction.");
            }

            var gridLines = gridIds
                    .Select(c => doc.GetElement(c) as Autodesk.Revit.DB.CurtainGridLine).ToArray();

            foreach (var gridLine in gridLines)
            {
                var fullCurve = gridLine.FullCurve;

                Line centerLine = new Line(fullCurve.GetEndPoint(0).ToVector3(), fullCurve.GetEndPoint(1).ToVector3());

                //TODO: Remove this, but for now we are adding the curves. Might consider leaving for the Hyper -> Revit Translation
                ModelCurve mCurve = new ModelCurve(centerLine);
                mullions.Add(mCurve);

                //build a sweep with the default profile
                List<SolidOperation> list = new List<SolidOperation>
                {
                    new Sweep(DefaultMullionProfile(),centerLine,0,0,false)
                };

                var mullion = new Mullion(null, DefaultMullionMaterial, new Representation(list), false, Guid.NewGuid(), null);

                mullions.Add(mullion);
            }

            return mullions.ToArray();
        }

    }
}
