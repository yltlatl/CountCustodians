using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;


namespace CountCustodians
{
    public class DelimitedFile
    {
        #region Constructors
        //optionally specify the delimiters to use

        //TODO: Add overload to pass an encoding object
        public DelimitedFile(string path, string encoding, char recordDelimiter = '\n', char fieldDelimiter = (char)20, char multiValueDelimiter = (char)59, char quote = (char)254, bool headerRecord = true)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Null or empty path.", path);
            if (!File.Exists(path)) throw new ArgumentException(string.Format("File {0} does not exist.", path));

            var encodingObj = ValidateEncodingString(encoding);

            _str = new StreamReader(path, encodingObj);

            RecordDelimiter = recordDelimiter;
            FieldDelimiter = fieldDelimiter;
            MultiValueDelimiter = multiValueDelimiter;
            this.Quote = quote;

            GetNextRecord();
            if (headerRecord) HeaderRecord = CurrentRecord;
            ExpectedFieldCount = CurrentRecord.Count();
        }


        #endregion


        #region Properties
        public char RecordDelimiter { get; private set; }

        public char FieldDelimiter { get; private set; }

        public char MultiValueDelimiter { get; private set; }

        public char Quote { get; private set; }

        public bool EndOfFile { get; private set; }

        private int ExpectedFieldCount { get; set; }

        private StreamReader _str { get; set; }

        public string CurrentLine { get; private set; } 

        public IEnumerable<string> CurrentRecord { get; private set; }

        public IEnumerable<string> HeaderRecord { get; private set; }

        #endregion

        //Get a particular field from the record by its zero-indexed position
        public string GetFieldByPosition(int position)
        {
            //TODO: fix possible meaningless reference to HeaderRecord.Count() (or possible null reference)
            if (position < 0 || position >= HeaderRecord.Count())
                throw new ArgumentOutOfRangeException(position.ToString(CultureInfo.InvariantCulture), "Position is out of range.");

            return CurrentRecord.ElementAt(position);
        }

        public List<string> GetFieldsByPosition(List<int> positions)
        {
            return positions.Select(position => GetFieldByPosition(position)).ToList();
        }

        //Get a particular field from the record by its name from the header row
        public string GetFieldByName(string name)
        {
            //do a case-insensitive matching
            var lName = name.ToLower(CultureInfo.InvariantCulture);

            var index = HeaderRecord.ToList().FindIndex(s => s.ToLower(CultureInfo.InvariantCulture).Equals(lName));
            if (index < 0) throw new ApplicationException(string.Format("\"{0}\" is not a valid column name.", name));
            return GetFieldByPosition(index);
        }

        //Get the next record in the file
        public void GetNextRecord()
        {
            var line = _str.ReadLine();
            if (_str.EndOfStream)
            {
                EndOfFile = true;
                return;
            }
            CurrentLine = line;
            if (string.IsNullOrEmpty(line)) throw new ApplicationException("Empty line.");
            CurrentRecord = ParseLine(line);
        }

        public IEnumerable<string> ParseLine(string line)
        {
            return ParseLine(line, FieldDelimiter, Quote, ExpectedFieldCount);
        }

        public static IEnumerable<string> ParseLine(string line, char delimiter, char quote, int expectedFieldCount)
        {
            var record = line.Split(delimiter).ToList();
            if (record == null) throw new ApplicationException(string.Format("No fields found in {0}", line));
            if (record.Count != expectedFieldCount)
            {
                record = SplitLineRobustly(line, delimiter, quote);
            }
            if (expectedFieldCount != 0 && record.Count != expectedFieldCount) throw new InvalidDataException("Problem encountered parsing line to record.");
            var retVal = new List<string>();
            var q = quote.ToString(CultureInfo.InvariantCulture);
            var currentRecord = record as IList<string> ?? record.ToList();
            foreach (var s in currentRecord)
            {
                retVal.Add(ReplaceQuotesIntelligently(s, q));
            }
            return retVal;
        }

        private static string ReplaceQuotesIntelligently(string s, string q)
        {
            if (s.StartsWith(q)) s = s.Remove(0, 1);
            //for some reason endswith is returning true for small thorn where the string ends in 'h'
            //if (s.EndsWith(q)) s = s.Remove(s.Length - 1, 1);
            if (Regex.IsMatch(s, string.Format("{0}$", q))) s = s.Remove(s.Length - 1, 1);
            return s;
        }

        private List<string> SplitLineRobustly(string line)
        {
            return SplitLineRobustly(line, FieldDelimiter, Quote);
        }

        private static List<string> SplitLineRobustly(string line, char fieldDelimiter, char quote)
        {
            var retVal = new List<string>();
            var buffer = new StringBuilder();
            var quoteOpen = false;

            for (var i = 0; i < line.Length; i++)
            {
                var character = line[i];
                if (character != quote && character != fieldDelimiter)
                {
                    buffer.Append(character);
                    if (i == line.Length - 1)
                    {
                        retVal.Add(buffer.ToString());
                    }
                    continue;
                }
                if (character == quote)
                {
                    if (quoteOpen)
                    {
                        if (i == (line.Length - 1))
                        {
                            retVal.Add(buffer.ToString());
                            buffer.Clear();
                            continue;
                        }
                        if (line[i + 1] == fieldDelimiter)
                        {
                            quoteOpen = false;
                            continue;
                        }
                        buffer.Append(character);
                    }
                    else
                    {
                        quoteOpen = true;
                        continue;
                    }
                }
                if (character == fieldDelimiter)
                {
                    if (quoteOpen)
                    {
                        buffer.Append(character);
                        continue;
                    }
                    retVal.Add(buffer.ToString());
                    buffer.Clear();
                }
            }
            return retVal;
        }

        public static IEnumerable<string> ParseMultiValueField(string fieldValue, char multiValueDelimiter, bool omitEmptyValues, bool trimWhitespace)
        {
            if (string.IsNullOrEmpty(fieldValue)) return new List<string>();
            var mvdArray = new[]{multiValueDelimiter};
            var sso = omitEmptyValues ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
            var splitResult = fieldValue.Split(mvdArray, sso);
            return trimWhitespace ? splitResult.Select(v => v.Trim()).ToList() : splitResult.ToList();
        }



        //Validate encoding string argument and return an Encoding object
        private static Encoding ValidateEncodingString(string encoding)
        {
            //make the string case insensitive
            var lEncoding = encoding.ToLower(CultureInfo.InvariantCulture);

            EncodingInfo encodingInfo = null;
            try
            {
                encodingInfo = Encoding.GetEncodings().Single(e => e.Name.Contains(lEncoding));
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(string.Format("{0} is not a valid encoding", encoding), e);
            }

            return Encoding.GetEncoding(encodingInfo.Name);
        }
    }
}
