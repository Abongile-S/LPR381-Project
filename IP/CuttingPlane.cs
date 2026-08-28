using System;
using System.Collections.Generic;
using System.Text;

namespace LPR381
{
    public class CuttingPlane
    {
        private List<List<double>> tableau;
        private List<double> rhs;
        private List<string> basis;
        private List<string> variables;
        private List<string> rowLabels;
        private bool isMax;
        private int gomoryCount = 0;

        private StringBuilder log = new StringBuilder();
        public string Log => log.ToString();

        private void Print(string s = "")
        {
            Console.WriteLine(s);
            log.AppendLine(s);
        }

        private void PrintInline(string s)
        {
            Console.Write(s);
            log.Append(s);
        }

        public void Solve(CanonicalForm canonical, string objectiveType)
        {
            isMax = canonical.IsMax;
            variables = new List<string>(canonical.Variables);
            basis = new List<string>(canonical.Basis);
            rhs = new List<double>(canonical.RHS);

            tableau = new List<List<double>>();
            foreach (var row in canonical.Tableau)
                tableau.Add(new List<double>(row));

            RebuildRowLabels();

            Print("\n===== CUTTING PLANE ALGORITHM (Gomory) =====");
            Print("Objective: " + objectiveType);

            Print("\n--- Solving LP Relaxation with Primal Simplex ---");
            bool ok = SolvePrimalToOptimal();
            if (!ok) return; // infeasible or unbounded, already printed

            int maxCuts = 20;
            while (gomoryCount < maxCuts)
            {
                int fracRow = FindFractionalRow();
                if (fracRow == -1)
                {
                    Print("\nAll basic decision variables are integer.");
                    Print("INTEGER OPTIMAL SOLUTION FOUND!");
                    DisplaySolution();
                    return;
                }

                gomoryCount++;
                Print("\n=== Adding Gomory Cut #" + gomoryCount + " (from row " + rowLabels[fracRow] + ", basis " + basis[fracRow] + ") ===");
                AddGomoryCut(fracRow);

                Print("\n--- Tableau after adding cut ---");
                DisplayTableau();

                Print("\n--- Re-optimizing with Dual Simplex ---");
                bool feasible = SolveDualToFeasible();
                if (!feasible)
                {
                    Print("\n*** MODEL IS INFEASIBLE (no more valid pivots in Dual Simplex) ***");
                    return;
                }
            }

            Print("\nMax number of cuts reached (" + maxCuts + ") without an all-integer solution.");
        }

        // ================= PRIMAL SIMPLEX (solve LP relaxation) =================

        private bool SolvePrimalToOptimal()
        {
            int iteration = 0;
            int maxIterations = 200;

            while (iteration < maxIterations)
            {
                Print("\n--- Primal Iteration " + iteration + " ---");
                DisplayTableau();

                if (IsPrimalOptimal())
                {
                    Print("\nLP Relaxation optimal reached.");
                    if (CheckArtificialInfeasibility())
                        return false;
                    return true;
                }

                int pivotCol = FindPrimalPivotColumn();
                if (pivotCol == -1) { Print("\nUNBOUNDED SOLUTION!"); return false; }

                int pivotRow = FindPrimalPivotRow(pivotCol);
                if (pivotRow == -1) { Print("\nUNBOUNDED SOLUTION!"); return false; }

                Print("Pivot Column: " + variables[pivotCol] + "   Pivot Row: " + rowLabels[pivotRow]);
                Pivot(pivotRow, pivotCol);
                basis[pivotRow] = variables[pivotCol];
                iteration++;
            }

            Print("\nMax iterations reached (check for cycling).");
            return false;
        }

        private bool IsPrimalOptimal()
        {
            for (int j = 0; j < tableau[0].Count; j++)
                if (tableau[0][j] < -0.0001) return false;
            return true;
        }

        private int FindPrimalPivotColumn()
        {
            int pivotCol = -1;
            double minVal = 0;
            for (int j = 0; j < tableau[0].Count; j++)
            {
                if (tableau[0][j] < minVal) { minVal = tableau[0][j]; pivotCol = j; }
            }
            return pivotCol;
        }

        private int FindPrimalPivotRow(int pivotCol)
        {
            int pivotRow = -1;
            double minRatio = double.MaxValue;
            for (int i = 1; i < tableau.Count; i++)
            {
                if (tableau[i][pivotCol] > 0.0001)
                {
                    double ratio = rhs[i] / tableau[i][pivotCol];
                    if (ratio < minRatio) { minRatio = ratio; pivotRow = i; }
                }
            }
            return pivotRow;
        }

        private bool CheckArtificialInfeasibility()
        {
            for (int i = 1; i < basis.Count; i++)
            {
                if (basis[i].StartsWith("a") && rhs[i] > 1e-6)
                {
                    Print("\n*** MODEL IS INFEASIBLE (artificial variable " + basis[i] + " remains positive) ***");
                    return true;
                }
            }
            return false;
        }

        // ================= DUAL SIMPLEX (restore feasibility after a cut) =================

        private bool SolveDualToFeasible()
        {
            int maxIterations = 100;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                int leavingRow = -1;
                double mostNegative = -1e-9;
                for (int i = 1; i < rhs.Count; i++)
                {
                    if (rhs[i] < mostNegative) { mostNegative = rhs[i]; leavingRow = i; }
                }

                if (leavingRow == -1) return true; // all RHS >= 0, feasible again

                int enteringCol = -1;
                double bestRatio = double.MaxValue;
                for (int j = 0; j < tableau[leavingRow].Count; j++)
                {
                    if (tableau[leavingRow][j] < -1e-9)
                    {
                        double ratio = tableau[0][j] / (-tableau[leavingRow][j]);
                        if (ratio < bestRatio) { bestRatio = ratio; enteringCol = j; }
                    }
                }

                if (enteringCol == -1) return false; // no valid pivot -> infeasible

                Print("Dual Pivot -- Leaving Row: " + rowLabels[leavingRow] + " (" + basis[leavingRow] + ")   Entering: " + variables[enteringCol]);
                Pivot(leavingRow, enteringCol);
                basis[leavingRow] = variables[enteringCol];

                Print("\n--- Dual Iteration " + iteration + " ---");
                DisplayTableau();
            }

            Print("\nMax dual simplex iterations reached.");
            return false;
        }

        // ================= SHARED PIVOT LOGIC =================

        private void Pivot(int pivotRow, int pivotCol)
        {
            double pivotElement = tableau[pivotRow][pivotCol];

            for (int j = 0; j < tableau[pivotRow].Count; j++)
                tableau[pivotRow][j] /= pivotElement;
            rhs[pivotRow] /= pivotElement;

            for (int i = 0; i < tableau.Count; i++)
            {
                if (i == pivotRow) continue;
                double factor = tableau[i][pivotCol];
                for (int j = 0; j < tableau[i].Count; j++)
                    tableau[i][j] -= factor * tableau[pivotRow][j];
                rhs[i] -= factor * rhs[pivotRow];
            }
        }

        // ================= GOMORY CUT GENERATION =================

        private int FindFractionalRow()
        {
            for (int i = 1; i < tableau.Count; i++)
            {
                if (basis[i].StartsWith("x") && Math.Abs(rhs[i] - Math.Round(rhs[i])) > 1e-6)
                    return i;
            }
            return -1;
        }

        private void AddGomoryCut(int rowIndex)
        {
            // 1. Take a snapshot of the fractional row's values BEFORE touching anything
            var sourceRow = new List<double>(tableau[rowIndex]);
            double sourceRhs = rhs[rowIndex];

            // 2. Extend every existing row with a new zero column for the new Gomory slack
            //    (indexed for-loop over the OUTER list, modifying only INNER lists -- safe)
            int newVarIndex = tableau[0].Count;
            for (int i = 0; i < tableau.Count; i++)
                tableau[i].Add(0.0);

            gomoryCount++; // used for naming; note: also incremented in Solve() caller before this call, that's fine, just keeps names unique
            string newVarName = "g" + gomoryCount;
            variables.Add(newVarName);

            // 3. Build the brand-new cut row as its own separate object
            int width = tableau[0].Count;
            var cutRow = new List<double>(new double[width]);
            for (int j = 0; j < sourceRow.Count; j++)
            {
                double frac = sourceRow[j] - Math.Floor(sourceRow[j]);
                cutRow[j] = -frac;
            }
            double fracRhs = sourceRhs - Math.Floor(sourceRhs);
            cutRow[newVarIndex] = 1.0;
            double cutRhs = -fracRhs;

            Print("Cut: " + string.Join(" ", cutRow.ConvertAll(v => v.ToString("F3"))) + " | RHS = " + cutRhs.ToString("F3"));

            // 4. Only now append the finished row to the tableau/rhs/basis lists
            tableau.Add(cutRow);
            rhs.Add(cutRhs);
            basis.Add(newVarName);

            RebuildRowLabels();
        }

        private void RebuildRowLabels()
        {
            rowLabels = new List<string> { "z" };
            for (int i = 1; i < tableau.Count; i++)
                rowLabels.Add("C" + i);
        }

        // ================= DISPLAY =================

        private void DisplayTableau()
        {
            PrintInline("      ");
            foreach (var v in variables) PrintInline(v + "\t");
            Print("RHS");

            for (int i = 0; i < tableau.Count; i++)
            {
                PrintInline(rowLabels[i] + "[" + basis[i] + "]\t");
                for (int j = 0; j < tableau[i].Count; j++)
                    PrintInline(tableau[i][j].ToString("F3") + "\t");
                Print(rhs[i].ToString("F3"));
            }
        }

        private void DisplaySolution()
        {
            Print("\n--- OPTIMAL INTEGER SOLUTION ---");
            for (int i = 0; i < variables.Count; i++)
            {
                string varName = variables[i];
                if (!varName.StartsWith("x")) continue;

                double value = 0;
                for (int j = 0; j < basis.Count; j++)
                {
                    if (basis[j] == varName) { value = rhs[j]; break; }
                }
                Print(varName + " = " + value.ToString("F3"));
            }

            double finalZ = isMax ? rhs[0] : -rhs[0];
            Print("\nOptimal Z = " + finalZ.ToString("F3"));
        }
    }
}