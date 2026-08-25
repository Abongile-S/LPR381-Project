using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381
{
    public static class BinaryLPUtils
    {
        public const double EPS = 1e-8;

        public class LPResult
        {
            public bool Feasible { get; set; }
            public double Objective { get; set; }
            public double[] Solution { get; set; }

            public LPResult()
            {
                Feasible = false;
                Objective = double.NegativeInfinity;
                Solution = new double[0];
            }
        }

        public class LPConstraint
        {
            public double[] Coefficients { get; set; }
            public string Relation { get; set; }
            public double RHS { get; set; }

            public LPConstraint(double[] coefficients, string relation, double rhs)
            {
                Coefficients = coefficients;
                Relation = relation;
                RHS = rhs;
            }
        }

        // =========================================================
        // SOLVE BINARY LP RELAXATION
        // =========================================================
        //
        // Solves:
        //
        // Max c'x
        //
        // subject to:
        //      original constraints
        //      lower bounds
        //      upper bounds
        //      branch constraints
        //
        // using vertex enumeration.
        //
        // This is deliberately small-project friendly and does not
        // require an external LP library.
        // =========================================================

        public static LPResult SolveLP(
            InputFileReader reader,
            List<LPConstraint> extraConstraints = null)
        {
            int n = reader.ObjCoefficients.Count;

            List<LPConstraint> constraints =
                new List<LPConstraint>();

            // Original constraints
            foreach (var con in reader.Constraints)
            {
                constraints.Add(
                    new LPConstraint(
                        con.Coefficients.Select(Convert.ToDouble).ToArray(),
                        con.Relation,
                        Convert.ToDouble(con.RHS)
                    )
                );
            }

            // Extra constraints from B&B or cutting planes
            if (extraConstraints != null)
            {
                constraints.AddRange(extraConstraints);
            }

            // Binary LP relaxation:
            //
            // x_i >= 0
            // x_i <= 1
            //
            for (int i = 0; i < n; i++)
            {
                double[] upper = new double[n];
                upper[i] = 1;

                constraints.Add(
                    new LPConstraint(upper, "<=", 1)
                );
            }

            // Convert all constraints to <= form
            List<LPConstraint> inequalities =
                ConvertToInequalities(constraints, n);

            // Add zero solution as candidate
            double[] bestX = new double[n];
            double bestZ = 0.0;
            bool found = IsFeasible(bestX, inequalities);

            // A vertex in n dimensions is formed by n active constraints.
            if (inequalities.Count >= n)
            {
                foreach (int[] combination in
                    GenerateCombinations(inequalities.Count, n))
                {
                    double[,] matrix = new double[n, n];
                    double[] rhs = new double[n];

                    for (int r = 0; r < n; r++)
                    {
                        LPConstraint c = inequalities[combination[r]];

                        for (int j = 0; j < n; j++)
                            matrix[r, j] = c.Coefficients[j];

                        rhs[r] = c.RHS;
                    }

                    double[] x;

                    if (!SolveLinearSystem(matrix, rhs, out x))
                        continue;

                    if (!IsFeasible(x, inequalities))
                        continue;

                    double z = 0;

                    for (int j = 0; j < n; j++)
                        z += reader.ObjCoefficients[j] * x[j];

                    if (!found || z > bestZ + EPS)
                    {
                        found = true;
                        bestZ = z;
                        bestX = (double[])x.Clone();
                    }
                }
            }

            return new LPResult
            {
                Feasible = found,
                Objective = bestZ,
                Solution = bestX
            };
        }

        // =========================================================
        // CONVERT <=, >= AND = INTO <=
        // =========================================================

        private static List<LPConstraint> ConvertToInequalities(
            List<LPConstraint> constraints,
            int n)
        {
            List<LPConstraint> result =
                new List<LPConstraint>();

            foreach (LPConstraint c in constraints)
            {
                string relation = c.Relation.Trim();

                if (relation == "<=")
                {
                    result.Add(
                        new LPConstraint(
                            (double[])c.Coefficients.Clone(),
                            "<=",
                            c.RHS
                        )
                    );
                }
                else if (relation == ">=")
                {
                    double[] neg =
                        c.Coefficients.Select(v => -v).ToArray();

                    result.Add(
                        new LPConstraint(
                            neg,
                            "<=",
                            -c.RHS
                        )
                    );
                }
                else if (relation == "=")
                {
                    result.Add(
                        new LPConstraint(
                            (double[])c.Coefficients.Clone(),
                            "<=",
                            c.RHS
                        )
                    );

                    double[] neg =
                        c.Coefficients.Select(v => -v).ToArray();

                    result.Add(
                        new LPConstraint(
                            neg,
                            "<=",
                            -c.RHS
                        )
                    );
                }
            }

            return result;
        }

        // =========================================================
        // FEASIBILITY
        // =========================================================

        public static bool IsFeasible(
            double[] x,
            List<LPConstraint> constraints)
        {
            foreach (LPConstraint c in constraints)
            {
                double lhs = 0;

                for (int i = 0; i < x.Length; i++)
                    lhs += c.Coefficients[i] * x[i];

                if (lhs > c.RHS + 1e-7)
                    return false;
            }

            return true;
        }

        // =========================================================
        // INTEGER CHECK
        // =========================================================

        public static bool IsInteger(double[] x)
        {
            foreach (double value in x)
            {
                if (Math.Abs(value - Math.Round(value)) > 1e-6)
                    return false;
            }

            return true;
        }

        // =========================================================
        // INTEGER SOLUTION
        // =========================================================

        public static double[] RoundBinary(double[] x)
        {
            double[] result = new double[x.Length];

            for (int i = 0; i < x.Length; i++)
                result[i] = Math.Round(x[i]);

            return result;
        }

        // =========================================================
        // LINEAR SYSTEM SOLVER
        // =========================================================

        private static bool SolveLinearSystem(
            double[,] A,
            double[] b,
            out double[] x)
        {
            int n = b.Length;

            x = new double[n];

            double[,] M = new double[n, n + 1];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    M[i, j] = A[i, j];

                M[i, n] = b[i];
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;

                for (int row = col + 1; row < n; row++)
                {
                    if (Math.Abs(M[row, col]) >
                        Math.Abs(M[pivot, col]))
                    {
                        pivot = row;
                    }
                }

                if (Math.Abs(M[pivot, col]) < EPS)
                    return false;

                if (pivot != col)
                {
                    for (int j = col; j <= n; j++)
                    {
                        double temp = M[col, j];
                        M[col, j] = M[pivot, j];
                        M[pivot, j] = temp;
                    }
                }

                double divisor = M[col, col];

                for (int j = col; j <= n; j++)
                    M[col, j] /= divisor;

                for (int row = 0; row < n; row++)
                {
                    if (row == col)
                        continue;

                    double factor = M[row, col];

                    for (int j = col; j <= n; j++)
                        M[row, j] -= factor * M[col, j];
                }
            }

            for (int i = 0; i < n; i++)
                x[i] = M[i, n];

            return true;
        }

        // =========================================================
        // COMBINATION GENERATOR
        // =========================================================

        private static IEnumerable<int[]> GenerateCombinations(
            int n,
            int k)
        {
            int[] result = new int[k];

            foreach (int[] combination in
                GenerateCombinationsRecursive(n, k, 0, result, 0))
            {
                yield return combination;
            }
        }

        private static IEnumerable<int[]> GenerateCombinationsRecursive(
            int n,
            int k,
            int start,
            int[] result,
            int depth)
        {
            if (depth == k)
            {
                yield return (int[])result.Clone();
                yield break;
            }

            for (int i = start; i <= n - (k - depth); i++)
            {
                result[depth] = i;

                foreach (int[] combination in
                    GenerateCombinationsRecursive(
                        n,
                        k,
                        i + 1,
                        result,
                        depth + 1))
                {
                    yield return combination;
                }
            }
        }

        // =========================================================
        // OBJECTIVE
        // =========================================================

        public static double Objective(
            InputFileReader reader,
            double[] x)
        {
            double z = 0;

            for (int i = 0; i < x.Length; i++)
                z += reader.ObjCoefficients[i] * x[i];

            return z;
        }

        // =========================================================
        // FORMAT SOLUTION
        // =========================================================

        public static string FormatSolution(double[] x)
        {
            return "[" +
                   string.Join(", ",
                       x.Select(v => v.ToString("F3"))) +
                   "]";
        }
    }
}