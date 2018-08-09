﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;

using static Impala.Generic;
using static Impala.Errors;

namespace Impala
{
    public class ParMeshCP : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ParMeshCP class.
        /// </summary>
        public ParMeshCP()
          : base("ParMeshCP", "ParMeshCP",
              "Closest point on a mesh to a sample point",
              "Impala", "Physical")
        {
            var error = new Error<(GH_Point, GH_Mesh, GH_Number)>(NullCheck, NullHandle, this);
            CheckError = new ErrorChecker<(GH_Point, GH_Mesh, GH_Number)>(error);
        }

        public ErrorChecker<(GH_Point, GH_Mesh, GH_Number)> CheckError;
        static Func<(GH_Point, GH_Mesh, GH_Number), bool> NullCheck = a => (a.Item1 != null && a.Item2 != null && a.Item3 != null);


        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points to test from", GH_ParamAccess.tree);
            pManager.AddMeshParameter("Mesh", "M", "Meshes to project to", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Threshold", "T", "Maximum allowed point distance (as Item)", GH_ParamAccess.tree, Double.MaxValue);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Closest Points on mesh", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Face Index", "I", "Index of closest face", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Projected", "X", "Was the point in threshold?", GH_ParamAccess.tree);  
        }

        public static (GH_Point, GH_Integer, GH_Boolean) MeshCP(GH_Point p, GH_Mesh m, GH_Number t)
        {
            Point3d pt = p.Value;
            Mesh msh = m.Value;
            double tol = t.Value;

            var meshpt = msh.ClosestMeshPoint(pt, tol); 

            if (meshpt != null)
            {
                Point3d closePoint = meshpt.Point;
                return (new GH_Point(closePoint), new GH_Integer(meshpt.FaceIndex), new GH_Boolean(true));
            }
            else
            {
                return (null, null, new GH_Boolean(false));
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
        
            if (!DA.GetDataTree(0, out GH_Structure<GH_Point> pointTree)) return;
            if (!DA.GetDataTree(1, out GH_Structure<GH_Mesh> meshTree)) return;
            if (!DA.GetDataTree(2, out GH_Structure<GH_Number> tolTree)) return;

            var (points, parameters, projects) = ZipMaxTree3x3(pointTree, meshTree, tolTree, MeshCP, CheckError);

            DA.SetDataTree(0, points);
            DA.SetDataTree(1, parameters);
            DA.SetDataTree(2, projects);
            return;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("fbfc1bea-e875-4996-973c-cb94865796d6"); }
        }
    }

    public class ParMeshCPAuto : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ParMeshCP class.
        /// </summary>
        public ParMeshCPAuto()
          : base("ParMeshCPAuto", "ParMeshCPAuto",
              "Closest point on a mesh to a sample point",
              "Impala", "Physical")
        {
            var error = new Error<(GH_Point, GH_Mesh, GH_Number)>(NullCheck, NullHandle, this);
            CheckError = new ErrorChecker<(GH_Point, GH_Mesh, GH_Number)>(error);
        }

        public ErrorChecker<(GH_Point, GH_Mesh, GH_Number)> CheckError;
        static Func<(GH_Point, GH_Mesh, GH_Number), bool> NullCheck = a => (a.Item1 != null && a.Item2 != null && a.Item3 != null);


        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points to test from", GH_ParamAccess.tree);
            pManager.AddMeshParameter("Mesh", "M", "Meshes to project to", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Threshold", "T", "Maximum allowed point distance (as Item)", GH_ParamAccess.tree, Double.MaxValue);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Closest Points on mesh", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Face Index", "I", "Index of closest face", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Projected", "X", "Was the point in threshold?", GH_ParamAccess.tree);
        }

        public static IGH_Goo[] MeshCPAuto(GH_Point p, GH_Mesh m, GH_Number t)
        {
            Point3d pt = p.Value;
            Mesh msh = m.Value;
            double tol = t.Value;

            var meshpt = msh.ClosestMeshPoint(pt, tol);

            if (meshpt != null)
            {
                Point3d closePoint = meshpt.Point;
                return new IGH_Goo[] { new GH_Point(closePoint), new GH_Integer(meshpt.FaceIndex), new GH_Boolean(true) };
            }
            else
            {
                return new IGH_Goo[] { null, null, new GH_Boolean(false) } ;
            }
        }


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            if (!DA.GetDataTree(0, out GH_Structure<GH_Point> pointTree)) return;
            if (!DA.GetDataTree(1, out GH_Structure<GH_Mesh> meshTree)) return;
            if (!DA.GetDataTree(2, out GH_Structure<GH_Number> tolTree)) return;

            var results = ZipMaxTree3xN(pointTree, meshTree, tolTree, MeshCPAuto, CheckError,3);

            DA.SetDataTree(0, DupCast(results[0],GooToPoint));
            DA.SetDataTree(1, DupCast(results[1],GooToInt));
            DA.SetDataTree(2, DupCast(results[2],GooToBool));
            return;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("000C4272-7DEE-4835-8B37-734457D3CFEA"); }
        }
    }
}