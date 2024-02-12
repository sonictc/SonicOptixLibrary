#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.NetLogic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
#endregion

public class CSVLog : BaseNetLogic
{
    public override void Start(){
        var productionRunning = LogicObject.GetVariable("LogTrigger");
        Log.Info("The Value is "+ productionRunning.Value);
        logEnabled = productionRunning.Value;
        productionRunning.VariableChange += ProductionRunning_Change;

        //Get log variable
       Log.Info("numbers of variables" + LogicObject.Children.Count.ToString());
      // List<IUAVariable> logVariables = new();
       foreach (IUAVariable child in LogicObject.Children.Cast<IUAVariable>())
        {
           logVariables.Add(child);
       }        
    }
    private void ProductionRunning_Change(object sender, VariableChangeEventArgs e)
    {
        if (e.NewValue == true)
        {
            loggingTask = new LongRunningTask(ProcessCsvFile, LogicObject);
            loggingTask.Start();
            logEnabled = e.NewValue;
            Log.Info("Start logging");
        }
        else
        {
            logEnabled = e.NewValue;       
            loggingTask.Dispose();

        }
    
    }

    public override void Stop()
    {
       loggingTask?.Dispose();
    }

    private void ProcessCsvFile(LongRunningTask task)
    {
        DateTime currentDate = DateTime.Now;

        // Format the current date as a string with format "yyyyMMdd"
        string formattedDate = currentDate.ToString("yyyyMMdd");

        filePath = GetCSVFilePath();
        string filePathWithoutExtension = Path.ChangeExtension(filePath, null);
        filePath = filePathWithoutExtension+formattedDate+".csv";

        Log.Info(filePath);

        // Create a StreamWriter to write to the CSV file
        using StreamWriter writer = File.AppendText(filePath);
        // Write header if the file is empty
        if (new FileInfo(filePath).Length == 0)
        {
            writer.WriteLine(logVariables[0].Value.ToString());
            string columnHeader = "Timestamp";
            for (int i = 3; i < logVariables.Count; i++)
            {
                columnHeader += ',';
                columnHeader += logVariables[i].BrowseName;
            }
            writer.WriteLine(columnHeader);
            Log.Info(columnHeader);
        }

        // Continuously write records
        while (true)
        {   //Log.Info("log enable = "+ logEnabled);
            if (logEnabled)
            {
                // Generate a sample record (timestamp and a random value)
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string value = new Random().Next(1, 100).ToString();

                // Construct the CSV record
                //string csvRecord = $"{timestamp},{value}";
                string csvRecord = $"{timestamp}";
                for (int i = 3; i < logVariables.Count; i++)
                {
                    csvRecord += ",";
                    csvRecord += logVariables[i].Value;
                }
                Log.Info(csvRecord);
                // Write the record to the file
                writer.WriteLine(csvRecord);
                writer.Flush(); // Flush to ensure data is written immediately

                // Wait for some time before writing the next record
                Thread.Sleep(1000); // Wait for 1 second (adjust as needed)
            }
            else
            {
                Log.Info("Logging stopped");
                writer.Flush(); // Flush to ensure data is written immediately
                break;
            }
        }
    }
    private string GetCSVFilePath()
    {
        string csvPath;
        FileInfo fi;
        try
        {
            var csvPathVariable = LogicObject.Get<IUAVariable>("CSVPath");
            csvPath = new ResourceUri(csvPathVariable.Value).Uri;
            fi = new FileInfo(csvPath);
        }
        catch (Exception ex)
        {
            Log.Error("CSVLog." + MethodBase.GetCurrentMethod().Name, "Cannot read CSV path, exception: " + ex.Message);
            return "";
        }
        return fi.FullName;
    }
    private LongRunningTask loggingTask;
    private bool logEnabled;
    private readonly List<IUAVariable> logVariables = new();
    private string filePath;
}
