#region Using directives
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.DataLogger;
using FTOptix.HMIProject;
using FTOptix.NativeUI;
using FTOptix.NetLogic;
using FTOptix.OPCUAClient;
using FTOptix.Report;
using FTOptix.Retentivity;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.UI;
using FTOptix.WebUI;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
#endregion

public class LogToCSV : BaseNetLogic
{
    public override void Start()
    {
        var productionRunning = LogicObject.GetVariable("LogTrigger");
        Log.Info("The Value is " + productionRunning.Value);
        logEnabled = productionRunning.Value;
        productionRunning.VariableChange += ProductionRunning_Change;

        //Get log variable
        Log.Info("numbers of variables" + LogicObject.Children.Count.ToString());
        // List<IUAVariable> logVariables = new();
        foreach (IUAVariable child in LogicObject.Children.Cast<IUAVariable>())
        {
            logVariables.Add(child);
        }

        logStatus = LogicObject.GetVariable("LogStatus");
        logStatus.Value = "Datalog Idled";
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
        logEnabled = false;
        loggingTask?.Dispose();
    }

    private void ProcessCsvFile(LongRunningTask task)
    {
        DateTime currentDate = DateTime.Now;

        // Format the current date as a string with format "yyyyMMdd"
        string formattedDate = currentDate.ToString("yyyyMMdd");

        filePath = GetCSVFilePath();

        string directory = Path.GetDirectoryName(filePath);
        string baseFileName = Path.GetFileNameWithoutExtension(filePath);

        string fileName = $"{baseFileName}_{DateTime.Now:yyyyMMdd}.csv";
        filePath = Path.Combine(directory, fileName);

        if (File.Exists(filePath))
        {
            int count = 1;

            string extension = Path.GetExtension(fileName);
            baseFileName = Path.GetFileNameWithoutExtension(filePath);
            while (File.Exists(filePath))
            {
                fileName = $"{baseFileName}_{count}{extension}";
                filePath = Path.Combine(directory, fileName);
                count++;
            }
        }
        else
        {
            logStatus.Value = "Data Logging Status : File Missing or path is invalid ";
        }
        Log.Info(filePath);

        // Create a StreamWriter to write to the CSV file
        using StreamWriter writer = File.AppendText(filePath);

        int firstVarPos = 7;
        // Write header if the file is empty
        if (new FileInfo(filePath).Length == 0)
        {
            string header = logVariables[0].Value;
            string programName = logVariables[4].Value;
            string areaName = logVariables[5].Value;

            Log.Info(logVariables[0].Value.ToString());
            writer.WriteLine(header);
            writer.WriteLine($"Program Name : {programName}");
            writer.WriteLine($"Area Name: {areaName}");
            string columnHeader = "Date,Time";
            for (int i = firstVarPos; i < logVariables.Count; i++)
            {
                columnHeader += ',';
                columnHeader += logVariables[i].BrowseName;
            }
            writer.WriteLine(columnHeader);
            Log.Info(columnHeader);
        }

        // Continuously write records
        while (true)
        { //Log.Info("log enable = "+ logEnabled);
            if (logEnabled & File.Exists(filePath))
            {
                // Generate a sample record (timestamp and a random value)
                string date = DateTime.Now.ToString("yyyy-MM-dd");
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                // Construct the CSV record
                //string csvRecord = $"{timestamp}";
                //revise to split date and time
                string csvRecord = $"{date},{timestamp}";
                for (int i = firstVarPos; i < logVariables.Count; i++)
                {
                    csvRecord += ",";
                    csvRecord += logVariables[i].Value;
                }
                Log.Info(csvRecord);
                try
                {
                    // Write the record to the file
                    writer.WriteLine(csvRecord);
                    writer.Flush(); // Flush to ensure data is written immediately
                    logStatus.Value = "Data Loggin Status : Logging";
                    // Wait for some time before writing the next record
                }
                catch (Exception error)
                {
                    Log.Error("DataLog to CSV error :" + error.Message);
                    logStatus.Value = "DataLog to CSV error :" + error.Message;
                }
                Thread.Sleep(logVariables[3].Value);
            }
            else
            {
                if (!File.Exists(filePath))
                {
                    logStatus.Value = "Data Logging Status : File Missing or path is invalid ";
                }
                else
                {
                    Log.Info("Logging stopped");
                    logStatus.Value = "Data Logging Status : Idle ";
                    writer.Flush(); // Flush to ensure data is written immediately
                    break;
                }
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
            Log.Error(
                "CSVLog." + MethodBase.GetCurrentMethod().Name,
                "Cannot read CSV path, exception: " + ex.Message
            );
            return "";
        }
        return fi.FullName;
    }

    private LongRunningTask loggingTask;
    private bool logEnabled;
    private readonly List<IUAVariable> logVariables = new();
    private string filePath;
    private IUAVariable logStatus;
}
