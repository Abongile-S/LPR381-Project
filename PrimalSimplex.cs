using System;
using System.Collections.Generic;
using System.Text;

namespace LPR381
{
    public class PrimalSimplex
    {
        private List<List<double>> tableau;
        private List<double> rhs;
        private List<string> basis;
        private List<string> variables;
        private List<string> rowLabels;
        private string objectiveType;
        private bool isMax;

        // ---- Public access to the FINAL solved tableau ----
        public List<List<double>> FinalTableau => tableau;
        public List<double> FinalRHS => rhs;
        public List<string> FinalBasis => basis;
        public List<string> FinalVariables => variables;
        public bool IsInfeasible { get; private set; } = false;
        public bool IsUnbounded { get; private set; } = false;

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
            this.objectiveType = objectiveType;
            this.isMax = canonical.IsMax;
            this.variables = new List<string>(canonical.Variables);
            this.basis = new List<string>(canonical.Basis);
            this.rhs = new List<double>(canonical.RHS);

            tableau = new List<List<double>>();
            foreach (var row in canonical.Tableau)
                tableau.Add(new List<double>(row));

            rowLabels = new List<string>();
            rowLabels.Add("z");
            for (int i = 1; i < tableau.Count; i++)
                rowLabels.Add("C" + i);

            Print("\n===== PRIMAL SIMPLEX ALGORITHM =====");
            Print("Objective: " + objectiveType);

            int iteration = 0;
            int maxIterations = 200;

            while (iteration < maxIterations)
            {
                Print("\n--- Iteration " + iteration + " ---");
                DisplayTableau();

                if (IsOptimal())
                {
                    Print("\nOPTIMAL SOLUTION FOUND!");
                    if (!CheckFeasibility())
                        DisplaySolution();
                    break;
                }

                int pivotCol = FindPivotColumn();
                if (pivotCol == -1)
                {
                    Print("\nUNBOUNDED SOLUTION!");
                    IsUnbounded = true;
                    break;
                }

                int pivotRow = FindPivotRow(pivotCol);
                if (pivotRow == -1)
                {
                    Print("\nUNBOUNDED SOLUTION!");
                    IsUnbounded = true;
                    break;
                }

                Print("Pivot Column: " + variables[pivotCol]);
                Print("Pivot Row: " + rowLabels[pivotRow]);

                Pivot(pivotRow, pivotCol);
                basis[pivotRow] = variables[pivotCol];

                iteration++;
            }

            if (iteration >= maxIterations)
                Print("\nMax iterations reached (check for cycling).");
        }

        private bool IsOptimal()
        {
            List<double> zRow = tableau[0];
            for (int j = 0; j < zRow.Count; j++)
            {
                if (zRow[j] < -0.0001)
                    return false;
            }
            return true;
        }

        private int FindPivotColumn()
        {
            List<double> zRow = tableau[0];
            int pivotCol = -1;
            double minVal = 0;

            for (int j = 0; j < zRow.Count; j++)
            {
                if (zRow[j] < minVal)
                {
                    minVal = zRow[j];
                    pivotCol = j;
                }
            }
            return pivotCol;
        }

        private int FindPivotRow(int pivotCol)
        {
            int pivotRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 1; i < tableau.Count; i++)
            {
                if (tableau[i][pivotCol] > 0.0001)
                {
                    double ratio = rhs[i] / tableau[i][pivotCol];
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotRow = i;
                    }
                }
            }
            return pivotRow;
        }

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

        private bool CheckFeasibility()
        {
            for (int i = 1; i < basis.Count; i++)
            {
                if (basis[i].StartsWith("a") && rhs[i] > 1e-6)
                {
                    Print("\n*** MODEL IS INFEASIBLE (artificial variable " + basis[i] + " remains positive) ***");
                    IsInfeasible = true;
                    return true;
                }
            }
            return false;
        }

        private void DisplayTableau()
        {
            PrintInline("      ");
            foreach (var v in variables)
                PrintInline(v + "\t");
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
            Print("\n--- OPTIMAL SOLUTION ---");

            for (int i = 0; i < variables.Count; i++)
            {
                string varName = variables[i];
                if (!varName.StartsWith("x")) continue;

                double value = 0;
                for (int j = 0; j < basis.Count; j++)
                {
                    if (basis[j] == varName)
                    {
                        value = rhs[j];
                        break;
                    }
                }
                Print(varName + " = " + value.ToString("F3"));
            }

            double finalZ = isMax ? rhs[0] : -rhs[0];
            Print("\nOptimal Z = " + finalZ.ToString("F3"));
        }
    }
}