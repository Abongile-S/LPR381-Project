using System;
using System.Collections.Generic;
using System.IO;

namespace LPR381
{
    public class InputFileReader
    {
        public string ObjectiveType { get; private set; }
        public List<double> ObjCoefficients { get; private set; }
        public List<Constraint> Constraints { get; private set; }
        public List<string> SignRestrictions { get; private set; }

        public void Read(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);

            // ----- Line 1: Objective Function -----
            string[] objParts = lines[0].Trim().Split(' ');
            ObjectiveType = objParts[0];

            ObjCoefficients = new List<double>();
            for (int i = 1; i < objParts.Length; i++)
            {
                ObjCoefficients.Add(double.Parse(objParts[i]));
            }

            // ----- Constraints -----
            Constraints = new List<Constraint>();
            for (int i = 1; i < lines.Length - 1; i++)
            {
                string[] parts = lines[i].Trim().Split(' ');
                List<double> coeffs = new List<double>();
                string relation = "";
                double rhs = 0;

                int j = 0;
                while (j < parts.Length && parts[j] != "<=" && parts[j] != ">=" && parts[j] != "=")
                {
                    coeffs.Add(double.Parse(parts[j]));
                    j++;
                }

                relation = parts[j];
                rhs = double.Parse(parts[j + 1]);

                Constraints.Add(new Constraint(coeffs, relation, rhs));
            }

            // ----- Sign Restrictions -----
            string[] signParts = lines[lines.Length - 1].Trim().Split(' ');
            SignRestrictions = new List<string>(signParts);
        }
    }
}