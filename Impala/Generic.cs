﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Impala.MathComponents
{

    /// <summary>
    /// Collection of static generic methods that operate on GH_Structures and 
    /// provide built-in looping and parallel functionality.
    /// </summary>
    public static class Generic
    {
        /// <summary>
        /// Creates the list of target paths for the looping logic. 
        /// </summary>
        /// <param name="structures"> Input structures to analyze </param>
        /// <returns>The paths of the structure that serve as the base of the output</returns>
        static List<GH_Path> GetPathList(params IGH_Structure[] structures)
        {
            var lenList = (from structure in structures select structure.get_Path(structure.LongestPathIndex()).Length).ToList();
            List<GH_Path> PathList = new List<GH_Path>();

            int maxLen = 0;
            for (int i = 0; i < structures.Length; i++)
            {
                if (lenList[i] > maxLen)
                {
                    maxLen = lenList[i];
                    PathList.Clear();
                    PathList.AddRange(structures[i].Paths);
                }
            }
            return PathList;
        }

        /// <summary>
        /// Fetches a path from the path list, and creates it if there is no existing path at this index.
        /// </summary>
        /// <param name="paths"> List of paths generated from GetPathList </param>
        /// <param name="i"> Desired path index </param>
        /// <returns> Path at that index </returns>
        static private GH_Path GetPath(List<GH_Path> paths, int i)
        {
            GH_Path path = new GH_Path();
            if (i < paths.Count)
            {
                path = new GH_Path(paths[i]);
            }
            else
            {
                path = paths[paths.Count - 1].Increment(path.Length - 1, (i - paths.Count) + 1);
            }
            return path;
        }

 

        /// <summary>
        /// Applies GH's default tree matching logic when applying an action that takes in two datatrees and outputs one.
        /// </summary>
        public static GH_Structure<R> ZipMaxManual<T, Q, R>(GH_Structure<T> a, GH_Structure<Q> b, Func<T, Q, R> action, ErrorChecker<(T,Q)> error)
            where T : IGH_Goo
            where Q : IGH_Goo
            where R : IGH_Goo
        {
            var result = new GH_Structure<R>();
            var maxbranch = Math.Max(a.Branches.Count, b.Branches.Count);
            var paths = GetPathList(a, b);

            for (int i = 0; i < maxbranch; i++)
            {
                var ba = a.Branches[Math.Min(i, a.Branches.Count - 1)];
                var bb = b.Branches[Math.Min(i, b.Branches.Count - 1)];
                if (ba.Count > 0 && bb.Count > 0)
                {
                    int maxlen = Math.Max(ba.Count, bb.Count);
                    R[] temp = new R[maxlen];
                    for (int j = 0; j < maxlen; j++)
                    {
                        T ax = ba[Math.Min(ba.Count - 1, j)];
                        Q bx = bb[Math.Min(bb.Count - 1, j)];
                        // Check and input
                        temp[j] = error.Validate((ax, bx)) ? action(ax, bx) : default;
                    }
                    result.AppendRange(temp,GetPath(paths,i));
                }
                else
                {
                    result.EnsurePath(GetPath(paths, i));
                }
            }
            return result;
        }

        /// <summary>
        /// Helper conversion delegate to use in the DuplicateCast method to be able to operate generically on GH_Structures
        /// </summary>
        public static IGH_Goo Gooify<T>(T data)
            where T : IGH_Goo
        {
            return data;
        }

        public static (int,int)[] GetPartitions<T,Q>(GH_Structure<T> a, GH_Structure<Q> b, int granularity)
            where T : IGH_Goo
            where Q : IGH_Goo
        {
            var PathLengths = new List<int>();
            var maxbranch = Math.Max(a.Branches.Count, b.Branches.Count);
            for (int i = 0; i < maxbranch; i++)
            {
                PathLengths.Add(Math.Max(a[Math.Min(a.Branches.Count - 1, i)].Count, 
                                         b[Math.Min(b.Branches.Count - 1, i)].Count));
            }
            var partitions = new List<(int, int)>();

            var prevIdx = 0; 
            var tempGran = 0; 
            for (int i = 0; i < PathLengths.Count; i++)
            {
                tempGran += PathLengths[i];
                if (tempGran > granularity)
                {
                    partitions.Add((prevIdx, i));
                    prevIdx = i + 1;
                    tempGran = 0;
                }
            }
            if (prevIdx < PathLengths.Count - 1)
            {
                partitions.Add((prevIdx, PathLengths.Count - 1));
            }

            return partitions.ToArray();
        }

        public static (int, int)[] GetPartitions1D<T>(GH_Structure<T> a, int granularity)
           where T : IGH_Goo
        {
            var PathLengths = a.Branches.Select(br => br.Count).ToList();
            var partitions = new List<(int, int)>();

            var prevIdx = 0;
            var tempGran = 0;
            for (int i = 0; i < PathLengths.Count; i++)
            {
                tempGran += PathLengths[i];
                if (tempGran > granularity)
                {
                    partitions.Add((prevIdx, i));
                    prevIdx = i + 1;
                    tempGran = 0;
                }
            }
            if (prevIdx < PathLengths.Count - 1)
            {
                partitions.Add((prevIdx, PathLengths.Count - 1));
            }

            return partitions.ToArray();
        }

        /// <summary>
        /// Applies GH's looping in a 2->1 scenario with the outer level (per-branch) parallelised.
        /// </summary>
        public static GH_Structure<R> ZipMaxParallel1D<T, Q, R>(GH_Structure<T> a, GH_Structure<Q> b, Func<T, Q, R> action, ErrorChecker<(T,Q)> error, int granularity)
            where T : IGH_Goo
            where Q : IGH_Goo
            where R : IGH_Goo
        {
            var result = new GH_Structure<R>();
            var maxbranch = Math.Max(a.Branches.Count, b.Branches.Count);
            var partitions = GetPartitions(a, b, granularity);

            var paths = GetPathList(a, b);
            for (int i = 0; i < maxbranch; i++)
            {
                result.EnsurePath(GetPath(paths, i));
            }
            Parallel.For(0, partitions.Length, p =>
            {
                var part = partitions[p];
                for (int i = part.Item1; i <= part.Item2; i++)
                {
                    var ba = a.Branches[Math.Min(i, a.Branches.Count - 1)];
                    var bb = b.Branches[Math.Min(i, b.Branches.Count - 1)];
                    if (ba.Count > 0 && bb.Count > 0)
                    {
                        int maxlen = Math.Max(ba.Count, bb.Count);
                        R[] temp = new R[maxlen];
                        for (int j = 0; j < maxlen; j++)
                        {
                            T ax = ba[Math.Min(ba.Count - 1, j)];
                            Q bx = bb[Math.Min(bb.Count - 1, j)];
                            temp[j] = error.Validate((ax, bx)) ? action(ax, bx) : default;
                        }
                        result.AppendRange(temp, GetPath(paths, i));
                    }
                }
            });
            return result;
        }

        /// <summary>
        /// Applies GH's looping in a 2->1 scenario with both per-branch and per-list parallelism.
        /// </summary>
        public static GH_Structure<R> ZipMaxParallel2D<T, Q, R>(GH_Structure<T> a, GH_Structure<Q> b, Func<T, Q, R> action)
            where T : IGH_Goo
            where Q : IGH_Goo
            where R : IGH_Goo
        {
            var result = new GH_Structure<R>();
            var maxbranch = Math.Max(a.Branches.Count, b.Branches.Count);
            var paths = GetPathList(a, b);
            for (int i = 0; i < maxbranch; i++)
            {
                result.EnsurePath(GetPath(paths, i));
            }
            Parallel.For(0, maxbranch, i =>
            {
                var ba = a.Branches[Math.Min(i, a.Branches.Count - 1)];
                var bb = b.Branches[Math.Min(i, b.Branches.Count - 1)];
                if (ba.Count > 0 && bb.Count > 0)
                {
                    int maxlen = Math.Max(ba.Count, bb.Count);
                    R[] temp = new R[maxlen];
                    Parallel.For(0, maxlen, j =>
                    {
                        T ax = ba[Math.Min(ba.Count - 1, j)];
                        Q bx = bb[Math.Min(bb.Count - 1, j)];
                        temp[j] = action(ax, bx);
                    });
                    result.AppendRange(temp, GetPath(paths, i));
                }
            });
            return result;
        }


        /// <summary>
        /// Applies standard functional zip to GH_Structure types, trimming unused elements.
        /// </summary>
        public static GH_Structure<R> ZipTree<T, Q, R>(GH_Structure<T> a, GH_Structure<Q> b, Func<T, Q, R> action)
            where T : IGH_Goo
            where Q : IGH_Goo
            where R : IGH_Goo
        {
            var result = new GH_Structure<R>();
            var minBranch = Math.Min(a.Branches.Count, b.Branches.Count);
            for (int i = 0; i < minBranch; i++)
            {
                var ba = a.Branches[i];
                var bb = b.Branches[i];
                result.AppendRange(ba.Zip(bb, action));
            }
            return result;
        }

        /// <summary>
        /// Applies a function to every element in a tree without modifying its structure.
        /// </summary>
        public static GH_Structure<Q> MapStructure<T, Q>(GH_Structure<T> init, Func<T, Q> action, ErrorChecker<T> error)
            where T : IGH_Goo
            where Q : IGH_Goo
        {
            var result = new GH_Structure<Q>();
            for (int i = 0; i < init.Branches.Count; i++)
            {
                result.AppendRange(init.Branches[i].Select(x => error.Validate(x) ? action(x) : default), init.Paths[i]);
            }
            return result;
        }


        public static GH_Structure<Q> ReduceStructure<T,Q>(GH_Structure<T> init, Func<List<T>,Q> action, ErrorChecker<List<T>> error, int granularity)
            where T : IGH_Goo
            where Q : IGH_Goo
        {
            var result = new GH_Structure<Q>();
            var partitions = GetPartitions1D(init, granularity);
            for (int i = 0; i < init.Branches.Count; i++)
            {
                result.EnsurePath(init.Paths[i]);
            }

            Parallel.For(0, partitions.Length, i =>
            {
                var partition = partitions[i];
                for (int j = partition.Item1; j <= partition.Item2; j++)
                {
                    var x = init.Branches[j];
                    if (error.Validate(x))
                    {
                        result.Append(action(x), init.Paths[j]);
                    }
                }
            });

            return result;
        }



        /// <summary>
        /// Applies a function with a specified granularity (number of items per parallel branch) 
        /// </summary>
        public static GH_Structure<Q> MapStructureParallel<T,Q>(GH_Structure<T> init, Func<T,Q> action, ErrorChecker<T> error, int granularity)
            where T : IGH_Goo
            where Q : IGH_Goo
        {
            var result = new GH_Structure<Q>();
            var parts = init.DataCount / granularity;
            var num = init.Branches.Count / parts;
            var partitions = GetPartitions1D(init, granularity);

            for(int i = 0; i < init.Branches.Count; i++)
            {
                result.EnsurePath(init.Paths[i]);
            }

            Parallel.For(0, partitions.Length, i =>
            {
                var partition = partitions[i];
                for (int j = partition.Item1; j <= partition.Item2; j++)
                {
                    result.AppendRange(init.Branches[j].Select(x => error.Validate(x) ? action(x) : default), init.Paths[j]);
                }
            });

            return result;    
        }

        /// <summary>
        /// Aapplies GH's default tree matching logic when applying an action that takes in two datatrees. 
        /// </summary>
        public static GH_Structure<R> ZipMaxAuto<T, Q, R>(GH_Structure<T> a, GH_Structure<Q> b, Func<T, Q, R> action)
            where T : IGH_Goo
            where Q : IGH_Goo
            where R : IGH_Goo
        {
            var result = new GH_Structure<R>();
            var maxbranch = Math.Max(a.Branches.Count, b.Branches.Count);
            var paths = GetPathList(a, b);
            for (int i = 0; i < maxbranch; i++)
            {
                var ba = a.Branches[Math.Min(i, a.Branches.Count - 1)];
                var bb = b.Branches[Math.Min(i, b.Branches.Count - 1)];
                if (ba.Count > 0 && bb.Count > 0)
                {
                    var temp = ZipListMax(ba, bb, action);
                    var targ = GetPath(paths, i);
                    result.AppendRange(temp, targ);
                }
                else
                {
                    result.EnsurePath(GetPath(paths, i));
                }
            }
            return result;
        }

        // This is sadly less effective than the manual array allocation.
        // Todo: bench this on long lists and lots of short ones
        private static IEnumerable<R> ZipListMax<T, Q, R>(List<T> a, List<Q> b, Func<T, Q, R> action)
        {
            int maxlen = Math.Max(a.Count, b.Count);
            for (int i = 0; i < maxlen; i++)
            {
                T ax = a[Math.Min(a.Count - 1, i)];
                Q bx = b[Math.Min(b.Count - 1, i)];
                yield return action(ax, bx);
            }
        }
    }
}
