using Elements;
using Elements.Conversion.Revit;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitRoofConverter
{
    public class RoofConverter : IRevitConverter
    {
        public bool CanConvertToRevit => true;
        public bool CanConvertFromRevit => true;

        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            //allows us to exclude in place roofs.
            ADSK.ElementClassFilter classFilter = new ADSK.ElementClassFilter(typeof(ADSK.RoofBase), false);
            return collector.OfCategory(ADSK.BuiltInCategory.OST_Roofs);
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
        {
            return HyparRoofFromRevitRoof(revitElement);
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            return RevitRoofFromHypar(hyparElement, context);
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            var types = allElements.Select(e => e.GetType());
            var elemType = typeof(Elements.Roof);
            return allElements.Where(e => e.GetType().FullName == typeof(Elements.Roof).FullName).ToArray();
        }

        private static Element[] HyparRoofFromRevitRoof(ADSK.Element revitRoof)
        {
            //return list
            var returnList = new List<Element>();

            //document and element as roofbase (the base class for all Revit roofs)
            ADSK.Document doc = revitRoof.Document;
            ADSK.RoofBase roofBase = revitRoof as ADSK.RoofBase;

            //get the top and bottom materials
            var materials = roofBase.GetMaterials();
            materials.TryGetValue("top", out var topMaterial);
            materials.TryGetValue("bottom", out var bottomMaterial);

            //use bounding box for elevation and high point
            var bBox = roofBase.get_BoundingBox(null);
            double elevation = Units.FeetToMeters(bBox.Min.Z);
            double highPoint = Units.FeetToMeters(bBox.Max.Z);

            //get thickness and area. these parameters apply to footprint or extrusion roofs
            double thickness =
                Units.FeetToMeters(roofBase.get_Parameter(ADSK.BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM)
                    .AsDouble());
            double area =
                Units.FeetToMeters(roofBase.get_Parameter(ADSK.BuiltInParameter.HOST_AREA_COMPUTED)
                    .AsDouble());

            //get our return information ready
            Mesh topside = null;
            Mesh underside = null;
            Mesh envelope = null;

            //footprint roofs section
            //- these have nice API methods for getting top and bottom faces
            if (roofBase is ADSK.FootPrintRoof footprintRoof)
            {
                //create topside
                var topReferences = ADSK.HostObjectUtils.GetTopFaces(footprintRoof);
                var topFaces = topReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).ToList();
                topside = Create.FacesToMesh(topFaces);

                //create underside
                var bottomReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);
                var bottomFaces = bottomReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r))
                    .ToList();
                underside = Create.FacesToMesh(bottomFaces);

                //create whole envelope
                var faces = footprintRoof.ExtractRoofFaces();
                envelope = Create.FacesToMesh(faces);
            }

            //profile roofs section
            //- these do not have nice API methods for getting top and bottom faces 😥 but we have our own now 😅
            if (roofBase is ADSK.ExtrusionRoof extrusionRoof)
            {
                //create topside and underside
                topside = extrusionRoof.ProfileRoofToMesh();
                underside = extrusionRoof.ProfileRoofToMesh(false);

                //build whole envelope
                var faces = extrusionRoof.ExtractRoofFaces();
                envelope = Create.FacesToMesh(faces);
            }

            //get the perimeter (same for both roof types)
            Polygon outerPerimeter = roofBase.ExtractRoofFootprint().First();

            //build the roof
            Roof hyparRoof = new Roof(envelope, topside, underside, outerPerimeter, elevation, highPoint, thickness,
                area, new Transform(), topMaterial, null, false, Guid.NewGuid(), "Roof");
            returnList.Add(hyparRoof);

            //create mesh element for visualization
            returnList.Add(new MeshElement(envelope, topMaterial));

            return returnList.ToArray();
        }

        private static ADSK.ElementId[] RevitRoofFromHypar(Element hyparElement, LoadContext context)
        {
            //our return list of element ids
            List<ADSK.ElementId> returnElementIds = new List<ADSK.ElementId>();

            //document and roof
            var doc = context.Document;
            var hyparRoof = hyparElement as Elements.Roof;

            //instantiate our tesselated shape builder for meshes
            var tsb = new ADSK.TessellatedShapeBuilder()
            {
                Fallback = ADSK.TessellatedShapeBuilderFallback.Salvage,
                Target = ADSK.TessellatedShapeBuilderTarget.Mesh,
                GraphicsStyleId = ADSK.ElementId.InvalidElementId,
            };
            tsb.OpenConnectedFaceSet(false);

            //extract triangles and iterate through adding faces to the mesh
            var triangles = hyparRoof.Envelope.Triangles.ToList();
            foreach (var t in triangles)
            {
                var vertices = t.Vertices.Select(v => v.Position.ToXYZ(true)).ToList();
                var face = new ADSK.TessellatedFace(vertices, ADSK.ElementId.InvalidElementId);
                tsb.AddFace(face);
            }

            //build the shape
            tsb.CloseConnectedFaceSet();
            tsb.Build();
            var result = tsb.GetBuildResult();

            //generate a direct shape with it
            ADSK.DirectShape dShape = ADSK.DirectShape.CreateElement(doc, new ADSK.ElementId(-2000035));
            dShape.SetShape(result.GetGeometricalObjects());
            dShape.SetName("HYPAR_Roof");

            //return the new stuff
            returnElementIds.Add(dShape.Id);

            return returnElementIds.ToArray();
        }
    }
}