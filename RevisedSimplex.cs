using System;
using System.Collections.Generic;
using System.Text;

namespace LPR381
{
    public class RevisedSimplex
    {
        private int numOriginalVars;
        private int numConstraints;
        private int numTotalVars;
        private double[,] A;
        private double[] c;
        private double[] b;
        private int[] basis;
        private bool isMax;
        private List<string> varNames;

        private const double BigM = 1000000;
        private int artificialStart;
        private int artificialCount;

        private StringBuilder log = new StringBuilder();
        public string Log => log.ToString();

        private void Print(string s = "")
        {
            Console.WriteLine(s);
            log.AppendLine(s);
        }

        public void Solve(InputFileReader reader)
        {
            SetupStandardForm(reader);
            RunSimplex();
        }

        private void SetupStandardForm(InputFileReader reader)
        {
            numOriginalVars = reader.ObjCoefficients.Count;
            numConstraints = reader.Constraints.Count;
            isMax = reader.ObjectiveType.Trim().ToLower() == "max";

            int slackCount = 0, surplusCount = 0;
            artificialCount = 0;
            foreach (var con in reader.Constraints)
            {
                if (con.Relation == "<=") slackCount++;
                else if (con.Relation == ">=") { surplusCount++; artificialCount++; }
                else if (con.Relation == "=") artificialCount++;
            }

            numTotalVars = numOriginalVars + slackCount + surplusCount + artificialCount;

            varNames = new List<string>();
            for (int i = 1; i <= numOriginalVars; i++) varNames.Add("x" + i);
            for (int i = 1; i <= slackCount; i++) varNames.Add("s" + i);
            for (int i = 1; i <= surplusCount; i++) varNames.Add("e" + i);
            for (int i = 1; i <= artificialCount; i++) varNames.Add("a" + i);

            int slackStart = numOriginalVars;
            int surplusStart = numOriginalVars + slackCount;
            artificialStart = numOriginalVars + slackCount + surplusCount;

            c = new double[numTotalVars];
            for (int j = 0; j < numOriginalVars; j++)
                c[j] = isMax ? reader.ObjCoefficients[j] : -reader.ObjCoefficients[j];
            for (int k = 0; k < artificialCount; k++)
                c[artificialStart + k] = -BigM;

            A = new double[numConstraints, numTotalVars];
            b = new double[numConstraints];
            basis = new int[numConstraints];

            int sIdx = 0, eIdx = 0, aIdx = 0;

            for (int i = 0; i < numConstraints; i++)
            {
                var con = reader.Constraints[i];
                for (int j = 0; j < numOriginalVars; j++)
                    A[i, j] = con.Coefficients[j];
                b[i] = con.RHS;

                if (con.Relation == "<=")
                {
                    A[i, slackStart + sIdx] = 1;
                    basis[i] = slackStart + sIdx;
                    sIdx++;
                }
                else if (con.Relation == ">=")
                {
                    A[i, surplusStart + eIdx] = -1;
                    A[i, artificialStart + aIdx] = 1;
                    basis[i] = artificialStart + aIdx;
                    eIdx++; aIdx++;
                }
                else
                {
                    A[i, artificialStart + aIdx] = 1;
                    basis[i] = artificialStart + aIdx;
                    aIdx++;
                }
            }
        }

        private void RunSimplex()
        {
            int m = numConstraints;
            double[,] BInv = Identity(m);

            Print("\n===== REVISED PRIMAL SIMPLEX ALGORITHM =====");
            Print("Objective: " + (isMax ? "max" : "min"));

            int iteration = 0;
            int maxIterations = 200;

            while (iteration < maxIterations)
            {
                double[] CB = new double[m];
                for (int i = 0; i < m; i++) CB[i] = c[basis[i]];

                double[] y = VectorTimesMatrix(CB, BInv);

                Print("\n--- Iteration " + iteration + " ---");
                Print("Basis: " + BasisNames());
                PrintMatrix("B Inverse (Product Form):", BInv);
                PrintVector("y (Simplex Multipliers):", y);

                int enteringVar = -1;
                double mostNegative = -1e-9;
                var reducedCosts = new Dictionary<int, double>();

                Print("Price Out (reduced costs for non-basic variables):");
                for (int j = 0; j < numTotalVars; j++)
                {
                    if (IsBasic(j)) continue;
                    double[] Aj = GetColumn(A, j);
                    double zj = DotProduct(y, Aj);
                    double reducedCost = zj - c[j];
                    reducedCosts[j] = reducedCost;
                    Print("  " + varNames[j] + ": z-c = " + reducedCost.ToString("F3"));

                    if (reducedCost < mostNegative)
                    {
                        mostNegative = reducedCost;
                        enteringVar = j;
                    }
                }

                double[] xB = MatrixTimesVector(BInv, b);
                double objVal = DotProduct(CB, xB);
                Print("Current Z = " + (isMax ? objVal : -objVal).ToString("F3"));

                if (enteringVar == -1)
                {
                    Print("\nOPTIMAL SOLUTION FOUND!");
                    if (!CheckFeasibility(BInv))
                        DisplaySolution(BInv);
                    return;
                }

                double[] Aentering = GetColumn(A, enteringVar);
                double[] d = MatrixTimesVector(BInv, Aentering);

                int leavingRow = -1;
                double minRatio = double.PositiveInfinity;
                for (int i = 0; i < m; i++)
                {
                    if (d[i] > 1e-9)
                    {
                        double ratio = xB[i] / d[i];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leavingRow = i;
                        }
                    }
                }

                if (leavingRow == -1)
                {
                    Print("\nUNBOUNDED SOLUTION!");
                    return;
                }

                Print("Entering: " + varNames[enteringVar] + "   Leaving: " + varNames[basis[leavingRow]]);

                BInv = ProductFormUpdate(BInv, d, leavingRow);
                basis[leavingRow] = enteringVar;
                iteration++;
            }

            if (iteration >= maxIterations)
                Print("\nMax iterations reached (check for cycling).");
        }

        private bool CheckFeasibility(double[,] BInv)
        {
            if (artificialCount == 0) return false;
            double[] xB = MatrixTimesVector(BInv, b);
            for (int i = 0; i < basis.Length; i++)
            {
                if (basis[i] >= artificialStart && xB[i] > 1e-6)
                {
                    Print("\n*** MODEL IS INFEASIBLE (artificial variable " + varNames[basis[i]] + " remains positive) ***");
                    return true;
                }
            }
            return false;
        }

        private void DisplaySolution(double[,] BInv)
        {
            double[] xB = MatrixTimesVector(BInv, b);
            var solution = new double[numOriginalVars];

            for (int i = 0; i < basis.Length; i++)
                if (basis[i] < numOriginalVars)
                    solution[basis[i]] = xB[i];

            Print("\n--- OPTIMAL SOLUTION ---");
            for (int j = 0; j < numOriginalVars; j++)
                Print(varNames[j] + " = " + solution[j].ToString("F3"));

            double[] CB = new double[numConstraints];
            for (int i = 0; i < numConstraints; i++) CB[i] = c[basis[i]];
            double z = DotProduct(CB, xB);
            Print("\nOptimal Z = " + (isMax ? z : -z).ToString("F3"));
        }

        private string BasisNames()
        {
            var names = new List<string>();
            foreach (var b in basis) names.Add(varNames[b]);
            return string.Join(", ", names);
        }

        private void PrintMatrix(string label, double[,] m)
        {
            Print(label);
            int rows = m.GetLength(0), cols = m.GetLength(1);
            for (int i = 0; i < rows; i++)
            {
                var sb = new StringBuilder("  ");
                for (int j = 0; j < cols; j++)
                    sb.Append(m[i, j].ToString("F3") + "\t");
                Print(sb.ToString());
            }
        }

        private void PrintVector(string label, double[] v)
        {
            var sb = new StringBuilder(label + " ");
            foreach (var val in v) sb.Append(val.ToString("F3") + "\t");
            Print(sb.ToString());
        }

        private double[,] ProductFormUpdate(double[,] BInv, double[] d, int pivotRow)
        {
            int m = BInv.GetLength(0);
            var newBInv = new double[m, m];
            double pivotVal = d[pivotRow];

            for (int j = 0; j < m; j++)
                newBInv[pivotRow, j] = BInv[pivotRow, j] / pivotVal;

            for (int i = 0; i < m; i++)
            {
                if (i == pivotRow) continue;
                for (int j = 0; j < m; j++)
                    newBInv[i, j] = BInv[i, j] - d[i] * newBInv[pivotRow, j];
            }
            return newBInv;
        }

        private bool IsBasic(int varIndex)
        {
            foreach (var b in basis) if (b == varIndex) return true;
            return false;
        }

        private double[,] Identity(int size)
        {
            var I = new double[size, size];
            for (int i = 0; i < size; i++) I[i, i] = 1.0;
            return I;
        }

        private double[] GetColumn(double[,] matrix, int col)
        {
            int rows = matrix.GetLength(0);
            var result = new double[rows];
            for (int i = 0; i < rows; i++) result[i] = matrix[i, col];
            return result;
        }

        private double[] MatrixTimesVector(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0), cols = matrix.GetLength(1);
            var result = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double sum = 0;
                for (int j = 0; j < cols; j++) sum += matrix[i, j] * vector[j];
                result[i] = sum;
            }
            return result;
        }

        private double[] VectorTimesMatrix(double[] vector, double[,] matrix)
        {
            int rows = matrix.GetLength(0), cols = matrix.GetLength(1);
            var result = new double[cols];
            for (int j = 0; j < cols; j++)
            {
                double sum = 0;
                for (int i = 0; i < rows; i++) sum += vector[i] * matrix[i, j];
                result[j] = sum;
            }
            return result;
        }

        private double DotProduct(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
            return sum;
        }
    }
}