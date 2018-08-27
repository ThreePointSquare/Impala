﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

using static Impala.Errors;
using static Impala.Generated;
using static Impala.Utilities;

namespace Impala
{
    /// <summary>
    /// Create a fast, random distribution of points within a box volume.
    /// </summary>
    public class HaltonSequence : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Halton Sequence component
        /// </summary>
        public HaltonSequence()
          : base("HaltonSequence", "PopHalton",
              "Generate random points within a bounding area",
              "Impala", "Physical")
        {
            var error = new Error<(GH_Box, GH_Integer)>(NullCheck, NullHandle, this);    
            CheckError = new ErrorChecker<(GH_Box, GH_Integer)>(error);
        }

        private static ErrorChecker<(GH_Box, GH_Integer)> CheckError;
        private static Func<(GH_Box, GH_Integer), bool> NullCheck = a => (a.Item1 != null && a.Item2 != null);
        
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var intervalDim = 10;
            var boxInterval = new Interval(-intervalDim, intervalDim);
            pManager.AddBoxParameter("Box", "B", "Bounding box for point generation", GH_ParamAccess.tree, new Box(Plane.WorldXY, boxInterval, boxInterval, boxInterval));
            pManager.AddIntegerParameter("Number", "N", "Number of points to generate", GH_ParamAccess.tree, 100);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Point", "P", "Generated points", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Determine the i'th point in the halton sequence of base b
        /// </summary>
        public static double Halton(int i, double b)
        {
            double f = 1;
            double r = 0;

            while (i > 0)
            {
                f = f / b;
                r = r + f * (i % b);
                i = (int) Math.Floor(i / b);
            }

            return r;
        }

        //Determine if two numbers are relatively prime
        private static bool IsCoPrime(int a, int b)
        {
            if (((a | b) & 1) == 0) return false;
            while ((a & 1) == 0) a >>= 1;
            if (a == 1) return true;

            while (b != 0)
            {
                while ((b & 1) == 0) b >>= 1;
                if ((b == 1)) return true;
                if (a > b)
                {
                    int c = b;
                    b = a;
                    a = c; 
                }
                b -= a;
            }
            return false;
        }

        //Find the smallest larger coprime number
        private static int FindSmallestCoprime(params int[] tests)
        {
            int i = tests.Max() + 1;
            while (true)
            {
                if (tests.All(x => IsCoPrime(i, x))) return i;
                i += 1;
            }
        }

        /// <summary>
        /// Solve method for Halton
        /// </summary>
        /// <param name="gbounds">Bounding box to fill</param>
        /// <param name="gnum">Number of points to create</param>
        /// <returns></returns>
        public static GH_Point[] GenHaltonSeq(GH_Box gbounds, GH_Integer gnum)
        {
            Box bnds = gbounds.Value;

            int num = gnum.Value;
            int sd1 = BaseHaltonSeed;
            int sd2 = FindSmallestCoprime(sd1);
            int sd3 = FindSmallestCoprime(sd1,sd2);

            var xrng = bnds.X.Length; var xst = bnds.X.Min;
            var s1 = IRange(0, num).Select(i => Halton(i, sd1) * xrng + xst).ToArray();
            var yrng = bnds.Y.Length; var yst = bnds.Y.Min;
            var s2 = IRange(0, num).Select(i => Halton(i, sd2) * yrng + yst).ToArray();
            var zrng = bnds.Z.Length; var zst = bnds.Z.Min;
            var s3 = IRange(0, num).Select(i => Halton(i, sd3) * zrng + zst).ToArray();

            return IRange(0, num).Select(i => new GH_Point(new Point3d(s1[i], s2[i], s3[i]))).ToArray();
        }

        private static int BaseHaltonSeed => 2;

        /// <summary>
        /// Loop through data structure. 
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Box> boxTree)) return;
            if (!DA.GetDataTree(1, out GH_Structure<GH_Integer> numTree)) return;

            var ptx = Zip2xGraft1(boxTree, numTree, GenHaltonSeq, CheckError);

            DA.SetDataTree(0, ptx);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Impala.Properties.Resources.__0023_Halton;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("19A5738C-50A8-403E-8999-BA077D5DE6F8"); }
        }
    }

}