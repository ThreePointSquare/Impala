﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using static Impala.Generated;
using static Impala.Errors;
using static Impala.Utilities;

namespace Impala
{
    public class ParIso2D : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ParIso2D class.
        /// </summary>
        public ParIso2D()
          : base("ParIsoVist2d", "ParIso2D",
              "Shoot a ring of rays at a set of obstacles.",
              "Impala", "Intersection")
        {
            //var error = new Error<(GH_Mesh, GH_Point, GH_Vector)>(NullCheck, NullHandle, this);
            //CheckError = new ErrorChecker<(GH_Mesh, GH_Point, GH_Vector)>(error);
        }

        //public ErrorChecker<(GH_Mesh, GH_Point, GH_Vector)> CheckError;
        //static Func<(GH_Mesh, GH_Point, GH_Vector), bool> NullCheck = a => (a.Item1 != null && a.Item2 != null && a.Item3 != null);


        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Point", "P", "Source point for isovist sample", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Number", "N", "Number of samples", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Radius", "R", "Radius of isovist sampling", GH_ParamAccess.item, 100);
            pManager.AddMeshParameter("Obstacles", "O", "Obstacles in sampling", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Plane", "P", "Sampling plane. XY is used by default.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntervalParameter("Interval", "I", "Sampling interval, starting at X axis moving CCW", GH_ParamAccess.item, new Interval(0, Math.PI * 2));
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Point", "X", "Isovist intersections", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Index", "I", "Index of obstacle hit", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Hit", "H", "Boolean indicating hit or miss", GH_ParamAccess.tree);
        }

        static IEnumerable<Point3d> ToPoints(Polyline p)
        {
            return p;
        }

        public static (GH_Point[], GH_Integer[], GH_Boolean[]) SolveIso2D(GH_Point gpt, GH_Plane gpl, GH_Integer gnum, GH_Number grad, GH_Interval gint, List<GH_Mesh> gobs)
        {
            var meshes = gobs.Select(ob => ob.Value).ToArray();
            var sampleCenter = gpt.Value;
            var plane = gpl.Value;
            var radius = grad.Value;
            var sintv = gint.Value;
            double samples = gnum.Value;

            var ixpts = meshes.SelectMany(m => Intersection.MeshPlane(m, plane).SelectMany(ToPoints));
            var smpt = IRange(0, gnum.Value).Select(i =>
             {
                 Vector3d baseVec = new Vector3d(plane.XAxis);
                 baseVec.Rotate(sintv.T0 + (sintv.Length) * i / samples, plane.ZAxis);
                 return (sampleCenter + baseVec);
             }).ToList();
            smpt.AddRange(ixpts);
            var circ = new Circle(plane, radius);
            var cptx = smpt.Select(p => { circ.ClosestParameter(p, out double t); return (p,t); });
            var sortVecs = cptx.OrderBy(pair => pair.t).Select(pair => {
                var amp = pair.p - sampleCenter;
                amp.Unitize();
                return new Ray3d(sampleCenter, amp);
                }).ToArray();

            var ptResults = new GH_Point[sortVecs.Length];
            var iResults = new GH_Integer[sortVecs.Length];
            var ixResults = new GH_Boolean[sortVecs.Length];

            Parallel.For(0, sortVecs.Length, j =>
            {
                var jRay = sortVecs[j];

                double bestIx = -1.0;
                int bestIdx = -1;
                for(int k = 0; k < meshes.Length; k++)
                {
                    var ix = Intersection.MeshRay(meshes[k], jRay);
                    if (ix < 0.0) continue;
                    else if (bestIx < 0.0 || ix < bestIx)
                    {
                        bestIx = ix;
                        bestIdx = k;
                    }
                }
                if (bestIx < 0.0) //failure, use radius
                {
                    ptResults[j] = new GH_Point(jRay.Position + jRay.Direction * radius);
                    iResults[j] = new GH_Integer(-1);
                    ixResults[j] = new GH_Boolean(false);
                }
                else
                {
                    //Cap radius
                    var pt2 = jRay.PointAt(bestIx);
                    if (pt2.DistanceTo(sampleCenter) > radius) pt2 = jRay.Position + jRay.Direction * radius;
                    ptResults[j] = new GH_Point(pt2);
                    iResults[j] = new GH_Integer(bestIdx);
                    ixResults[j] = new GH_Boolean(true);
                }
            });

            return (ptResults, iResults, ixResults);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var samplePt = new GH_Point();
            var numSamples = new GH_Integer();
            var radius = new GH_Number();
            var obstacles = new List<GH_Mesh>();
            var plane = new GH_Plane();
            var range = new GH_Interval();

            if (!DA.GetData(0, ref samplePt)) return;
            if (!DA.GetData(1, ref numSamples)) return;
            if (!DA.GetData(2, ref radius)) return;
            if (!DA.GetDataList(3, obstacles)) return;
            if (!DA.GetData(4, ref plane)) return;
            if (!DA.GetData(5, ref range)) return;

            //This requires a Zip5Red1xGraft3();

            if (obstacles.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No obstacles!");
                return;
            }

            if (numSamples == null || numSamples.Value <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No samples!");
                return;
            }

            if (radius == null || radius.Value <= DocumentTolerance())
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Radius too small!");
                return;
            }

            var (pt, idx, ix) = SolveIso2D(samplePt, plane, numSamples, radius, range, obstacles);
            DA.SetDataList(0, pt);
            DA.SetDataList(1, idx);
            DA.SetDataList(2, ix);
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
            get { return new Guid("6BA96C66-AAF9-4483-A1AF-522BF4D0612C"); }
        }
    }
}