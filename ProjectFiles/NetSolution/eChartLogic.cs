#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.DataLogger;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.WebUI;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Core;
using System.IO;
using System.Threading;
#endregion

public class eChartLogic : BaseNetLogic
{
    public override void Start()
    {
       RefreshGraph(); // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }
    [ExportMethod]
    
    public void RefreshGraph()
    {
        Owner.Get<WebBrowser>("WebBrowser").Visible = false;
        Log.Info("eCharts", "Starting");
        String projectPath = (ResourceUri.FromProjectRelativePath("").Uri);
        String folderSeparator = Path.DirectorySeparatorChar.ToString();
        string[] yAxisArray = {"100","200","300","200","100"};
        string[] xAxisArray = {"1","2","3","4","5"};
        
        // // Get template name and create destination path
        string templatePath = projectPath + folderSeparator + "eCharts" + folderSeparator + "Template-BasicLineChart.html";
        string filePath = projectPath + folderSeparator + "eCharts" + folderSeparator + "BasicLineChart.html";

        // // Read template page content
        string text = File.ReadAllText(templatePath);
      
        // Insert values
        // for (int i = 1; i < 4; i++)
        // {
        //      (Project.Current.GetVariable("Model/Dashboard/Variable" + i).Value * 1000).ToString();
        // }
        text = text.Replace("$yAxis",string.Join(",", yAxisArray));
        text = text.Replace("$xAxis",string.Join(",", xAxisArray));
        // // Write to file
         File.WriteAllText(filePath, text);

        // Refresh WebBrowser page
        Owner.Get<WebBrowser>("WebBrowser").Refresh();
        Log.Debug("eCharts", "Finished");
        Thread.Sleep(500);
        Owner.Get<WebBrowser>("WebBrowser").Visible = true;
    }
}
