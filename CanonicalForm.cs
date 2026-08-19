using System;
using System.Collections.Generic;

namespace LPR381
{
    public class CanonicalForm
    {
        public List<string> Variables { get; set; }      // x1, x2, s1, e1, a1, ...
        public List<List<double>> Tableau { get; set; }  // Coefficient rows
        public List<string> Basis { get; set; }          // Basic variables
        public List<double> RHS { get; set; }            // Right-hand side values

        public void Convert(InputFileReader reader)
        {
            int numVars = reader.ObjCoefficients.Count;
            int numConstraints = reader.Constraints.Count;

            // Start with original variables
            Variables = new List<string>();
            for (int i = 1; i <= numVars; i++)
                Variables.Add("x" + i);

            // Track slack/excess/artificial count
            int slackCount = 0;
            int excessCount = 0;
            int artificialCount = 0;

            // Initialize tableau (rows = constraints + 1 for z-row)
            Tableau = new List<List<double>>();
            RHS = new List<double>();
            Basis = new List<string>();

            // ---- Z-ROW ----
            List<double> zRow = new List<double>();
            foreach (var c in reader.ObjCoefficients)
                zRow.Add(-c);  // z - c1x1 - c2x2 - ... = 0

            // We'll add slack/excess/artificial columns later
            Tableau.Add(zRow);
            RHS.Add(0);
            Basis.Add("z");

            // ---- CONSTRAINTS ----
            for (int i = 0; i < numConstraints; i++)
            {
                var constraint = reader.Constraints[i];
                List<double> row = new List<double>();

                // Coefficients for original variables
                foreach (var c in constraint.Coefficients)
                    row.Add(c);

                // Add variables based on relation
                if (constraint.Relation == "<=")
                {
                    slackCount++;
                    Variables.Add("s" + slackCount);
                    row.Add(1);      // Slack variable coefficient
                    Basis.Add("s" + slackCount);
                }
                else if (constraint.Relation == ">=")
                {
                    excessCount++;
                    artificialCount++;
                    Variables.Add("e" + excessCount);
                    Variables.Add("a" + artificialCount);
                    row.Add(-1);     // Excess variable coefficient
                    row.Add(1);      // Artificial variable coefficient
                    Basis.Add("a" + artificialCount);
                }
                else if (constraint.Relation == "=")
                {
                    artificialCount++;
                    Variables.Add("a" + artificialCount);
                    row.Add(1);      // Artificial variable coefficient
                    Basis.Add("a" + artificialCount);
                }

                Tableau.Add(row);
                RHS.Add(constraint.RHS);
            }

            // Ensure all rows have the same number of columns
            int maxCols = 0;
            foreach (var row in Tableau)
                if (row.Count > maxCols) maxCols = row.Count;

            foreach (var row in Tableau)
                while (row.Count < maxCols)
                    row.Add(0);
        }

        public void Display()
        {
            Console.WriteLine("\n===== CANONICAL FORM =====");
            for (int i = 0; i < Tableau.Count; i++)
            {
                for (int j = 0; j < Tableau[i].Count; j++)
                {
                    Console.Write(Tableau[i][j].ToString("F3") + "\t");
                }
                Console.WriteLine("| " + RHS[i].ToString("F3"));
            }
        }
    }
}