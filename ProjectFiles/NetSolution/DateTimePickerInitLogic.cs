#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.Core;
#endregion

public class DateTimePickerInitLogic : BaseNetLogic
{
    public override void Start()
    {
        DateTime currentDate = GetCurrentDate();
        DateTime selectedDateTime = Owner.Get("DateAndTime").GetVariable("Value").Value;
    
        string firstHistoryTxt = "2000-01-01";
        DateTime firstHistory = DateTime.Parse(firstHistoryTxt);
        if (selectedDateTime < firstHistory)
        {
            Owner.Get("DateAndTime").GetVariable("Value").Value = currentDate.Date;
           
        }
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

      public static DateTime GetCurrentDate()
    {
        TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        return TimeZoneInfo.ConvertTime(DateTime.Now, timeZoneInfo);
    }
}
