#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.Alarm;
using FTOptix.UI;
using FTOptix.RAEtherNetIP;
using FTOptix.NativeUI;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using System.Linq;
#endregion

public class YearPanelLogic : BaseNetLogic
{
    public override void Start()
    {
        var selectedYearObject = LogicObject.GetVariable("SelectedYear");
        LogicObject.GetVariable("UISelectedYear").Value = selectedYearObject.Value;
        RepaintYearPanel(selectedYearObject.Value);// Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void RepaintYearPanel(int year){

        int startYear = year-4;
        int endYear = year+4;
        var panel = Owner.Get("Layout/YearPanel");

        int i = startYear;
        foreach (var child in panel.Children.OfType<Panel>())
        {
            child.GetVariable("Year").Value = i;
            i++;
            if (i > endYear)
            {
                break;
            }
        }

    }
    [ExportMethod]
    public void Up(){
        int selectedYear = LogicObject.GetVariable("UISelectedYear").Value;
        selectedYear += 9;
        RepaintYearPanel(selectedYear);
        LogicObject.GetVariable("UISelectedYear").Value = selectedYear;
    }

    [ExportMethod]
    public void Down(){
        int selectedYear = LogicObject.GetVariable("UISelectedYear").Value;
        selectedYear -= 9;
        RepaintYearPanel(selectedYear);
        LogicObject.GetVariable("UISelectedYear").Value = selectedYear;

    }

    [ExportMethod]
    public void ResetYear(){
        int selectedYear = LogicObject.GetVariable("SelectedYear").Value;
        RepaintYearPanel(selectedYear);
        LogicObject.GetVariable("UISelectedYear").Value = selectedYear;

    }
    
}
