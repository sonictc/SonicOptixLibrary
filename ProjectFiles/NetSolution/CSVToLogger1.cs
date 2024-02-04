#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.NetLogic;
using FTOptix.WebUI;
using FTOptix.DataLogger;
using FTOptix.Store;
using FTOptix.SQLiteStore;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
#endregion

public class CSVToLogger1 : BaseNetLogic
{
    [ExportMethod]
    public void Import()
    {
        var datalogger = GetDataLogger();
        if (datalogger == null)
            return;

        var csvPath = GetCSVFilePath();
        if (string.IsNullOrEmpty(csvPath))
        {
            Log.Error("CSVToDataLogger", "Empty csv file path");
            return;
        }

        char? characterSeparator = GetCharacterSeparator();
        if (characterSeparator == null || characterSeparator == '\0')
            return;

        var wrapFields = GetWrapFields();
        try
        {
            using (var csvReader = new CSVFileReader(csvPath)
            {
                FieldDelimiter = characterSeparator.Value,
                WrapFields = wrapFields
            })
            {
                if (csvReader.EndOfFile())
                {
                    Log.Error("CSVToDataLogger", $"The file {csvPath} is empty");
                    return;
                }

                var fileLines = csvReader.ReadAll();
                if (fileLines.Count == 0 || fileLines[0].Count == 0)
                    return;

                var variablesToLog = datalogger.GetObject("VariablesToLog");
                //var converterDataType = GetDataTypeFrom(pairs);
                variablesToLog.Children.Clear();

                for (var rowNumber = 1; rowNumber < fileLines.Count; ++rowNumber)
                {
                    var csvRow = fileLines[rowNumber];
                    var dataType = ResolveDataType(csvRow[1]);
                    var newVar = InformationModel.MakeVariable(csvRow[0], dataType);
                    variablesToLog.Add(newVar);
                }

                var variablesToLogAfter = datalogger.GetObject("VariablesToLog").Children;
                var row = 1;
                foreach (IUAVariable singleVariable in variablesToLogAfter)
                {

                    var csvRow = fileLines[row];
                    IUAVariable dynamicLinkVariable = Project.Current.GetVariable(csvRow[2]);
                    singleVariable.SetDynamicLink(dynamicLinkVariable, DynamicLinkMode.Read);
                    row++;
                }


            }
        }
        catch (Exception e)
        {
            Log.Error("CSVToDataLogger", $"Could not import key value converter: {e.Message}");
        }
    }

    [ExportMethod]
    public void Export()
    {
        var datalogger = GetDataLogger();
        if (datalogger == null)
            return;

        var csvPath = GetCSVFilePath();
        if (string.IsNullOrEmpty(csvPath))
        {
            Log.Error("CSVToDataLogger", "Empty csv file path");
            return;
        }

        var characterSeparator = (char)GetCharacterSeparator();
        if (characterSeparator == '\0')
            return;

        var wrapFields = GetWrapFields();
        var variablesToLog = datalogger.GetObject("VariablesToLog").Children;

        // if (!ValidatePairsDataType(converterPairs.First()))
        //     Log.Error("CSVToDataLogger", "Invalid data type for exporting KeyValue converter. Available data types are numeric, string and localized text data types.");

        try
        {
            using (var csvWriter = new CSVFileWriter(csvPath) { FieldDelimiter = characterSeparator, WrapFields = wrapFields })
            {
                // write csv header
                csvWriter.WriteLine(new string[] { "Name", "DataType", "DynamicLink" });

                foreach (IUAVariable variable in variablesToLog)
                {
                    var name = variable.BrowseName;
                    var link = variable.GetVariable("DynamicLink");
                    PathResolverResult resolvePathResult = LogicObject.Context.ResolvePath(variable, link.Value);
                    string browsePath = MakeBrowsePath(resolvePathResult.ResolvedNode);

                    string dataType = GetDataTypeString(variable.DataType);
                    //Log.Info()
                    csvWriter.WriteLine(new string[] { name, dataType, browsePath });
                }
            }

            Log.Info("CSVToDataLogger", $"{datalogger.BrowseName} successfully exported to {csvPath}");
        }
        catch (Exception e)
        {
            Log.Error("CSVToDataLogger", "Unable to export CSV file: " + e.Message);
        }
    }

    // private ConverterPairDataType GetDataTypeFrom(IUAObject converterPair)
    // {
    //     ConverterPairDataType pairDataType;

    //     if (converterPair.Children.Count > 0)
    //     {
    //         var firstPair = converterPair.Children.First();
    //         pairDataType.KeyDataType = firstPair.GetVariable("Key").DataType;
    //         pairDataType.ValueDataType = firstPair.GetVariable("Value").DataType;
    //     }
    //     else
    //         throw new CoreConfigurationException($"Data type for key-value pair has not been set");

    //     return pairDataType;
    // }

    // private IUAObject MakePairFrom(List<string> stringPair, int pairNumber, ConverterPairDataType converterPairDatatype)
    // {
    //     var pair = InformationModel.MakeObject($"Pair{pairNumber}", FTOptix.CoreBase.ObjectTypes.ValueMapPair);
    //     var keyVariable = pair.GetVariable("Key");
    //     keyVariable.Value = GetValue(stringPair[0], converterPairDatatype.KeyDataType);
    //     keyVariable.DataType = converterPairDatatype.KeyDataType;

    //     var valueVariable = pair.GetVariable("Value");
    //     valueVariable.Value = GetValue(stringPair[1], converterPairDatatype.ValueDataType);
    //     valueVariable.DataType = converterPairDatatype.ValueDataType;
    //     return pair;
    // }

    // private string GetValueToWrite(IUAVariable variable)
    // {
    //     if (variable.DataType == OpcUa.DataTypes.LocalizedText)
    //         return ((LocalizedText)variable.Value).TextId;
    //     else
    //         return variable.Value;
    // }
    private static string GetDataTypeString(NodeId type)
    {
        switch (type.ToString())
        {
            case "0/6": return "Int32";
            case "0/1": return "Boolean";
            case "0/4": return "Int16";
            case "0/10": return "Float";
            case "0/11": return "Double";
            default: return "";
        }
    }

    private static NodeId ResolveDataType(string type)
    {
        switch (type)
        {
            case "Int32": return OpcUa.DataTypes.Int32;
            case "Int16": return OpcUa.DataTypes.Int16;
            case "Float": return OpcUa.DataTypes.Float;
            case "Double": return OpcUa.DataTypes.Double;
            case "Boolean": return OpcUa.DataTypes.Boolean;
            default: return OpcUa.DataTypes.Int32;
        }
    }
    private static string MakeBrowsePath(IUANode node)
    {
        string path = node.BrowseName;
        var current = node.Owner;

        while (current != Project.Current)
        {
            path = current.BrowseName + "/" + path;
            current = current.Owner;
        }
        return path;
    }

    private UAValue GetValue(string csvData, NodeId dataType)
    {
        if (dataType == OpcUa.DataTypes.LocalizedText)
            return new LocalizedText(Project.Current.NodeId.NamespaceIndex, csvData);
        else
            return csvData;
    }

    private bool ValidatePairsDataType(IUANode pair)
    {
        var context = LogicObject.Context;
        var keyDataType = context.GetDataType(pair.GetVariable("Key").DataType);
        var valueDataType = context.GetDataType(pair.GetVariable("Value").DataType);
        return IsSupportedDataType(keyDataType) && IsSupportedDataType(valueDataType);
    }

    private bool IsSupportedDataType(IUADataType dataType)
    {
        return dataType.IsSubTypeOf(OpcUa.DataTypes.Number) || dataType.IsSubTypeOf(OpcUa.DataTypes.String) || dataType.IsSubTypeOf(OpcUa.DataTypes.LocalizedText);
    }

    private DataLogger GetDataLogger()
    {
        var dataloggerVariable = LogicObject.GetVariable("DataLogger");
        if (dataloggerVariable == null)
        {
            Log.Error("CSVToDataLogger", "Could not obtain logger variable");
            return null;
        }

        var dataloggerNode = InformationModel.Get<DataLogger>(dataloggerVariable.Value);
        if (dataloggerNode == null)
            Log.Error("CSVToDataLogger", "Could not obtain logger node");

        return dataloggerNode;
    }

    private string GetCSVFilePath()
    {
        var csvPathVariable = LogicObject.GetVariable("CSVFile");
        if (csvPathVariable == null)
        {
            Log.Error("CSVToDataLogger", "CSVPath variable not found");
            return "";
        }

        return new ResourceUri(csvPathVariable.Value).Uri;
    }

    private bool GetWrapFields()
    {
        var wrapFieldsVariable = LogicObject.GetVariable("WrapFields");
        if (wrapFieldsVariable == null)
        {
            Log.Error("CSVToDataLogger", "WrapFields variable not found");
            return false;
        }

        return wrapFieldsVariable.Value;
    }

    private char? GetCharacterSeparator()
    {
        var separatorVariable = LogicObject.GetVariable("CSVSeparator");
        if (separatorVariable == null)
        {
            Log.Error("CSVToDataLogger", "CharacterSeparator variable not found");
            return null;
        }

        string separator = separatorVariable.Value;

        if (separator.Length != 1 || separator == string.Empty)
        {
            Log.Error("CSVToDataLogger", "Wrong CharacterSeparator configuration. Please insert a char");
            return null;
        }

        if (char.TryParse(separator, out char result))
            return result;

        return null;
    }

    // private struct ConverterPairDataType
    // {
    //     public NodeId KeyDataType;
    //     public NodeId ValueDataType;
    // }

    #region CSVFileReader
    private class CSVFileReader : IDisposable
    {
        public char FieldDelimiter { get; set; } = ',';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public bool IgnoreMalformedLines { get; set; } = false;

        public CSVFileReader(string filePath, System.Text.Encoding encoding)
        {
            streamReader = new StreamReader(filePath, encoding);
        }

        public CSVFileReader(string filePath)
        {
            streamReader = new StreamReader(filePath, System.Text.Encoding.UTF8);
        }

        public CSVFileReader(StreamReader streamReader)
        {
            this.streamReader = streamReader;
        }

        public bool EndOfFile()
        {
            return streamReader.EndOfStream;
        }

        public List<string> ReadLine()
        {
            if (EndOfFile())
                return null;

            var line = streamReader.ReadLine();

            var result = WrapFields ? ParseLineWrappingFields(line) : ParseLineWithoutWrappingFields(line);

            currentLineNumber++;
            return result;

        }

        public List<List<string>> ReadAll()
        {
            var result = new List<List<string>>();
            while (!EndOfFile())
                result.Add(ReadLine());

            return result;
        }

        private List<string> ParseLineWithoutWrappingFields(string line)
        {
            if (string.IsNullOrEmpty(line) && !IgnoreMalformedLines)
                throw new FormatException($"Error processing line {currentLineNumber}. Line cannot be empty");

            return line.Split(FieldDelimiter).ToList();
        }

        private List<string> ParseLineWrappingFields(string line)
        {
            var fields = new List<string>();
            var buffer = new StringBuilder("");
            var fieldParsing = false;

            int i = 0;
            while (i < line.Length)
            {
                if (!fieldParsing)
                {
                    if (IsWhiteSpace(line, i))
                    {
                        ++i;
                        continue;
                    }

                    // Line and column numbers must be 1-based for messages to user
                    var lineErrorMessage = $"Error processing line {currentLineNumber}";
                    if (i == 0)
                    {
                        // A line must begin with the quotation mark
                        if (!IsQuoteChar(line, i))
                        {
                            if (IgnoreMalformedLines)
                                return null;
                            else
                                throw new FormatException($"{lineErrorMessage}. Expected quotation marks at column {i + 1}");
                        }

                        fieldParsing = true;
                    }
                    else
                    {
                        if (IsQuoteChar(line, i))
                            fieldParsing = true;
                        else if (!IsFieldDelimiter(line, i))
                        {
                            if (IgnoreMalformedLines)
                                return null;
                            else
                                throw new FormatException($"{lineErrorMessage}. Wrong field delimiter at column {i + 1}");
                        }
                    }

                    ++i;
                }
                else
                {
                    if (IsEscapedQuoteChar(line, i))
                    {
                        i += 2;
                        buffer.Append(QuoteChar);
                    }
                    else if (IsQuoteChar(line, i))
                    {
                        fields.Add(buffer.ToString());
                        buffer.Clear();
                        fieldParsing = false;
                        ++i;
                    }
                    else
                    {
                        buffer.Append(line[i]);
                        ++i;
                    }
                }
            }

            return fields;
        }

        private bool IsEscapedQuoteChar(string line, int i)
        {
            return line[i] == QuoteChar && i != line.Length - 1 && line[i + 1] == QuoteChar;
        }

        private bool IsQuoteChar(string line, int i)
        {
            return line[i] == QuoteChar;
        }

        private bool IsFieldDelimiter(string line, int i)
        {
            return line[i] == FieldDelimiter;
        }

        private bool IsWhiteSpace(string line, int i)
        {
            return Char.IsWhiteSpace(line[i]);
        }

        private StreamReader streamReader;
        private int currentLineNumber = 1;

        #region IDisposable support
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
                streamReader.Dispose();

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
    #endregion CSVFileReader

    #region CSVFileWriter
    private class CSVFileWriter : IDisposable
    {
        public char FieldDelimiter { get; set; } = ',';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public CSVFileWriter(string filePath)
        {
            streamWriter = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        }

        public CSVFileWriter(string filePath, System.Text.Encoding encoding)
        {
            streamWriter = new StreamWriter(filePath, false, encoding);
        }

        public CSVFileWriter(StreamWriter streamWriter)
        {
            this.streamWriter = streamWriter;
        }

        public void WriteLine(string[] fields)
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < fields.Length; ++i)
            {
                if (WrapFields)
                    stringBuilder.AppendFormat("{0}{1}{0}", QuoteChar, EscapeField(fields[i]));
                else
                    stringBuilder.AppendFormat("{0}", fields[i]);

                if (i != fields.Length - 1)
                    stringBuilder.Append(FieldDelimiter);
            }

            streamWriter.WriteLine(stringBuilder.ToString());
            streamWriter.Flush();
        }

        private string EscapeField(string field)
        {
            var quoteCharString = QuoteChar.ToString();
            return field.Replace(quoteCharString, quoteCharString + quoteCharString);
        }

        private readonly StreamWriter streamWriter;

        #region IDisposable Support
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
                streamWriter.Dispose();

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
    #endregion CSVFileWriter
}
