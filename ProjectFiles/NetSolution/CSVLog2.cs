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
using FTOptix.WebUI;
using FTOptix.OPCUAClient;
using FTOptix.Store;
using FTOptix.SQLiteStore;
using FTOptix.DataLogger;
using FTOptix.Report;
#endregion

public class CSVLog2 : BaseNetLogic
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
        else{

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
            string columnHeader = "Timestamp";
            for (int i = firstVarPos; i < logVariables.Count; i++)
            {
                columnHeader += ',';
                columnHeader += logVariables[i].BrowseName;
            }
            writer.WriteLine(columnHeader);
            Log.Info(columnHeader);
        }

        // Continuously write records
        while (true )
        {   //Log.Info("log enable = "+ logEnabled);
            if (logEnabled & File.Exists(filePath))
            {
                // Generate a sample record (timestamp and a random value)
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            
                // Construct the CSV record
                //string csvRecord = $"{timestamp},{value}";
                string csvRecord = $"{timestamp}";
                for (int i = firstVarPos; i < logVariables.Count; i++)
                {
                    csvRecord += ",";
                    csvRecord += logVariables[i].Value;
                }
                Log.Info(csvRecord);
                try {
                // Write the record to the file
                writer.WriteLine(csvRecord);
                writer.Flush(); // Flush to ensure data is written immediately
                logStatus.Value = "Data Loggin Status : Logging";
                // Wait for some time before writing the next record
                


                }
                catch(Exception error)
                {
                    Log.Error("DataLog to CSV error :"+ error.Message);
                    logStatus.Value = "DataLog to CSV error :"+ error.Message;
                 }
                 Thread.Sleep(logVariables[3].Value); 
            }
            else
            {
                if(!File.Exists(filePath)) {
                    logStatus.Value = "Data Logging Status : File Missing or path is invalid ";}
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
            Log.Error("CSVLog." + MethodBase.GetCurrentMethod().Name, "Cannot read CSV path, exception: " + ex.Message);
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
