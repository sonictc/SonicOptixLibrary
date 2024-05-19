#region Using directives
using System;
using System.Collections.Generic;
using System.Linq;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using FTOptix.NativeUI;
using FTOptix.NetLogic;
using FTOptix.Retentivity;
using FTOptix.UI;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
#endregion

public class DateTimePickerLogic : BaseNetLogic
{
    public override void Start()
    {
        SelectedTime = LogicObject.GetVariable("SelectedTime");
        UISelected = LogicObject.GetVariable("UISelectedTime");
       
        DateTime selectedDateTime = SelectedTime.Value;

        string firstHistoryTxt = "2000-01-01";
        DateTime firstHistory = DateTime.Parse(firstHistoryTxt);
        if (selectedDateTime < firstHistory)
        {
            DateTime currentDate = GetCurrentDate();
            UISelected.Value = currentDate.Date;
            repaintCalendar(currentDate);
        }
        else
        {
            UISelected.Value = selectedDateTime.Date;
            UISelected.GetVariable("Hour").Value = selectedDateTime.ToString("HH");
            UISelected.GetVariable("Minute").Value = selectedDateTime.ToString("mm");
            UISelected.GetVariable("Second").Value = selectedDateTime.ToString("ss");
            UISelected.GetVariable("Year").Value = selectedDateTime.ToString("yyyy");
            UISelected.GetVariable("Month").Value = selectedDateTime.ToString("MM");
            UISelected.GetVariable("Day").Value = selectedDateTime.ToString("dd");
            repaintCalendar(selectedDateTime);
        }
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    public void repaintCalendar(DateTime date)
    {
        List<DateTime> monthDates = GetMonthDates(date);
        LogicObject.GetVariable("DisplayMonthYearHeader").Value = date.ToString("MMM, yyyy");
        LogicObject.GetVariable("DisplayMonth").Value = date.ToString("MM");
        LogicObject.GetVariable("DisplayYear").Value = date.ToString("yyyy");
        LogicObject.GetVariable("UISelectedTime/Year").Value = date.Year;
        LogicObject.GetVariable("UISelectedTime/Month").Value = date.Month;
        LogicObject.GetVariable("UISelectedTime/Day").Value = date.Day;

        var panel = Owner.Get("Layout/DayPanel");

        int i = 0;
        foreach (var child in panel.Children.OfType<Panel>())
        {
            child.GetVariable("IsValid").Value = false;
        }

        foreach (var child in panel.Children.OfType<Panel>())
        {
            child.GetVariable("IsValid").Value = true;
            child.GetVariable("Date").Value = monthDates[i];
            child.GetVariable("Day").Value = monthDates[i].ToString("dd");
            if (monthDates[i].ToString("MM") == date.ToString("MM"))
            {
                child.GetVariable("IsThisMonth").Value = true;
            }
            else
            {
                child.GetVariable("IsThisMonth").Value = false;
            }
            i++;
            if (i > monthDates.Count - 1)
            {
                break;
            }
        }
        ;
    }
    public static DateTime GetCurrentDate()
    {
        TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        return TimeZoneInfo.ConvertTime(DateTime.Now, timeZoneInfo);
    }
    public static List<DateTime> GetMonthDates(DateTime date)
    {
        DateTime firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
        DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        DateTime startOfFirstWeek = GetStartOfWeek(firstDayOfMonth);
        DateTime endOfLastWeek = GetEndOfWeek(lastDayOfMonth);

        List<DateTime> monthDates = new List<DateTime>();

        for (
            DateTime current = startOfFirstWeek;
            current <= endOfLastWeek;
            current = current.AddDays(1)
        )
        {
            monthDates.Add(current);
        }

        return monthDates;
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        DayOfWeek firstDayOfWeek = DayOfWeek.Sunday;
        int diff = (7 + (date.DayOfWeek - firstDayOfWeek)) % 7;
        return date.AddDays(-1 * diff).Date;
    }

    private static DateTime GetEndOfWeek(DateTime date)
    {
        DayOfWeek lastDayOfWeek = DayOfWeek.Saturday;
        int diff = (lastDayOfWeek - date.DayOfWeek + 7) % 7;
        return date.AddDays(diff).Date;
    }

    [ExportMethod]
    public void Confirm()
    {
        int hours = UISelected.GetVariable("Hour").Value ;
        int minutes = UISelected.GetVariable("Minute").Value ;
        int seconds = UISelected.GetVariable("Second").Value ;
        DateTime dateTime = UISelected.Value;
        string dateString = dateTime.ToString("yyyy-MM-dd")+" "+hours+":"+minutes+":"+seconds;
        DateTime newDate = DateTime.Parse(dateString);

        SelectedTime.Value = newDate;
        SelectedTime.GetVariable("Year").Value = newDate.Year;
        SelectedTime.GetVariable("Month").Value= newDate.Month;
        SelectedTime.GetVariable("Day").Value= newDate.Day;
        SelectedTime.GetVariable("Hour").Value= newDate.Hour;
        SelectedTime.GetVariable("Minute").Value=newDate.Minute;
        SelectedTime.GetVariable("Second").Value=newDate.Second;

        int dayOfWeek = (int)newDate.DayOfWeek;
         SelectedTime.GetVariable("DayOfWeek").Value=dayOfWeek;
    }

    [ExportMethod]
    public void NextMonth()
    {
        DateTime currentSelect = UISelected.Value;
        UISelected.Value = currentSelect.AddMonths(1); 
        repaintCalendar(UISelected.Value);
    }

    [ExportMethod]
    public void PreviousMonth()
    {
        DateTime currentSelect = UISelected.Value;
        UISelected.Value = currentSelect.AddMonths(-1); 
        repaintCalendar(UISelected.Value);
    }

    [ExportMethod]
    public void BackToday()
    {
        DateTime currentDate = GetCurrentDate();
        UISelected.Value = currentDate.Date;
        repaintCalendar(currentDate);
    }

     [ExportMethod]
    public void EvaluateMonth()
    {
        repaintCalendar(UISelected.Value);
    }   

    [ExportMethod]
    public void EvaluateYear()
    {
        int year = LogicObject.GetVariable("UISelectedTime/Year").Value;
        int month = LogicObject.GetVariable("UISelectedTime/Month").Value;
        int day = LogicObject.GetVariable("UISelectedTime/Day").Value;
        string newMonthStr = year.ToString() + "-" + month.ToString() + "-" + day.ToString();
        DateTime newMonth = DateTime.Parse(newMonthStr);
        UISelected.Value = newMonth;
        repaintCalendar(newMonth);
    }   

    public IUAVariable SelectedTime; // = LogicObject.GetVariable("SelectedTime");
    public IUAVariable UISelected; 
}
