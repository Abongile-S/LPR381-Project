using System;
using System.Collections.Generic;
using System.Text;

namespace LPR381
{
    public class CanonicalForm
    {
        public List<string> Variables { get; set; }
        public List<List<double>> Tableau { get; set; }
        public List<string> Basis { get; set; }
        public List<double> RHS { get; set; }
        public bool IsMax { get; private set; }

        private const double BigM = 1000000;

        public void Convert(InputFileReader reader)
        {
            IsMax = reader.ObjectiveType.Trim().ToLower() == "max";
            int numVars = reader.ObjCoefficients.Count;

            int slackCount = 0, surplusCount = 0, artificialCount = 0;
            foreach (var con in reader.Constraints)
            {
                if (con.Relation == "<=") slackCount++;
                else if (con.Relation == ">=") { surplusCount++; artificialCount++; }
                else if (con.Relation == "=") artificialCount++;
            }

            int totalCols = numVars + slackCount + surplusCount + artificialCount;
            int slackStart = numVars;
            int surplusStart = numVars + slackCount;
            int artificialStart = numVars + slackCount + surplusCount;

            Variables = new List<string>();
            for (int i = 1; i <= numVars; i++) Variables.Add("x" + i);
            for (int i = 1; i <= slackCount; i++) Variables.Add("s" + i);
            for (int i = 1; i <= surplusCount; i++) Variables.Add("e" + i);
            for (int i = 1; i <= artificialCount; i++) Variables.Add("a" + i);

            var effectiveCoeffs = new List<double>();
            foreach (var c in reader.ObjCoefficients)
                effectiveCoeffs.Add(IsMax ? c : -c);

            Tableau = new List<List<double>>();
            Basis = new List<string>();
            RHS = new List<double>();

            var zRow = new List<double>(new double[totalCols]);
            for (int j = 0; j < numVars; j++)
                zRow[j] = -effectiveCoeffs[j];
            for (int k = 0; k < artificialCount; k++)
                zRow[artificialStart + k] = BigM;

            Tableau.Add(zRow);
            RHS.Add(0);
            Basis.Add("z");

            int sIdx = 0, eIdx = 0, aIdx = 0;
            var artificialRows = new List<int>();

            foreach (var con in reader.Constraints)
            {
                var row = new List<double>(new double[totalCols]);
                for (int j = 0; j < numVars; j++)
                    row[j] = con.Coefficients[j];

                string basisVar;

                if (con.Relation == "<=")
                {
                    row[slackStart + sIdx] = 1;
                    basisVar = "s" + (sIdx + 1);
                    sIdx++;
                }
                else if (con.Relation == ">=")
                {
                    row[surplusStart + eIdx] = -1;
                    row[artificialStart + aIdx] = 1;
                    basisVar = "a" + (aIdx + 1);
                    artificialRows.Add(Tableau.Count);
                    eIdx++; aIdx++;
                }
                else
                {
                    row[artificialStart + aIdx] = 1;
                    basisVar = "a" + (aIdx + 1);
                    artificialRows.Add(Tableau.Count);
                    aIdx++;
                }

                Tableau.Add(row);
                RHS.Add(con.RHS);
                Basis.Add(basisVar);
            }

            foreach (int rowIndex in artificialRows)
            {
                var row = Tableau[rowIndex];
                double rowRhs = RHS[rowIndex];
                for (int j = 0; j < totalCols; j++)
                    zRow[j] -= BigM * row[j];
                RHS[0] -= BigM * rowRhs;
            }
        }

        public string GetDisplayString()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("===== CANONICAL FORM (Big M Method) =====");
            sb.Append("      ");
            foreach (var v in Variables) sb.Append(v + "\t");
            sb.AppendLine("RHS");

            for (int i = 0; i < Tableau.Count; i++)
            {
                sb.Append((i == 0 ? "z" : "C" + i) + "[" + Basis[i] + "]\t");
                for (int j = 0; j < Tableau[i].Count; j++)
                    sb.Append(Tableau[i][j].ToString("F3") + "\t");
                sb.AppendLine(RHS[i].ToString("F3"));
            }
            return sb.ToString();
        }

        public void Display()
        {
            Console.Write(GetDisplayString());
        }
    }
}