using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CountCustodians
{
    class Program
    {
        static void Main(string[] args)
        {
            //probably better to actually test this path now rather than wait until the end
            var outputPath = args[0];
            
            var aliasPath = args[1];
            var apdf = new DelimitedFile(aliasPath, "utf-8", '\n', '\t', ';', (char) 254, true);
            var custodianAliasList = new Dictionary<string, List<string>>();
            while (!apdf.EndOfFile)
            {
                var fields = apdf.GetFieldByPosition(1).Split(new[] {(char) 59}).ToList();
                var bareFields = new List<string>();
                foreach (var field in fields)
                {
                    var bareField = field.TrimStart().TrimEnd().Replace("\"", string.Empty);
                    bareFields.Add(bareField);
                }
                custodianAliasList.Add(apdf.GetFieldByPosition(0), bareFields);
                apdf.GetNextRecord();
            }

            var inputPath = args[2];
            var idf = new DelimitedFile(inputPath, "utf-8");
            Console.WriteLine("Field names:");
            foreach (var record in idf.HeaderRecord)
            {
                Console.WriteLine(record);
            }
            var custodianDocList = new Dictionary<string, List<string>>();
            idf.GetNextRecord();
            var lineNumber = 1;
            while (!idf.EndOfFile)
            {
                Console.WriteLine("Processing line {0}", lineNumber);
                var controlId = idf.GetFieldByName("Control ID");
                var familyId = idf.GetFieldByPosition(1);
                var header = idf.GetFieldByName("Headers");
                var custodianStr = new StringBuilder();
                custodianStr.Append(idf.GetFieldByPosition(2));
                custodianStr.Append(string.Format(";{0}", idf.GetFieldByPosition(3)));
                custodianStr.Append(string.Format(";{0}", idf.GetFieldByPosition(4)));
                var allCustodians = DelimitedFile.ParseMultiValueField(custodianStr.ToString(), (char) 59, true, true);
                var custodians = new List<string>();
                foreach (var custodian in allCustodians.Where(custodian => !custodians.Any(c => c.Equals(custodian))))
                {
                    custodians.Add(custodian);
                }
                foreach (var custodian in custodianAliasList.Keys.Where(custodian => !custodians.Any(c => c.Equals(custodian))))
                {
                    foreach (var alias in custodianAliasList[custodian])
                    {
                        if (!custodians.Any(c => c.Equals(custodian)))
                        {
                            if (header.Contains(alias))
                            {
                                custodians.Add(custodian);
                            }
                            else if (custodianDocList.ContainsKey(custodian))
                            {
                                if (custodianDocList[custodian].Contains(familyId))
                                {
                                    custodians.Add(custodian);
                                }
                            }
                        }
                    }
                }
                foreach (var custodian in custodians)
                {
                    if (custodianDocList.ContainsKey(custodian))
                    {
                        custodianDocList[custodian].Add(controlId);
                    }
                    else
                    {
                        custodianDocList.Add(custodian, new List<string> { controlId });
                    }
                }
                idf.GetNextRecord();
                lineNumber++;
            }


            using (var str = new StreamWriter(outputPath))
            {
                str.AutoFlush = true;
                foreach (var custodian in custodianDocList)
                {
                    var strBldr = new StringBuilder(custodian.Key);
                    strBldr.Append("\t");
                    strBldr.Append(custodian.Value.Count);
                    str.WriteLine(strBldr.ToString());
                }
            }
        }
    }
}
