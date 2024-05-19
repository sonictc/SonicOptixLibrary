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
        //var SelectedTime = Owner.Get("SelectedTime1");
        // DateTime selectedDateTime = SelectedTime.GetVariable("SelectedDateTime").Value;
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
            // SelectedTime.GetVariable("SelectedHour").Value = selectedDateTime.ToString("HH");
            // SelectedTime.GetVariable("SelectedMinute").Value = selectedDateTime.ToString("mm");
            // SelectedTime.GetVariable("SelectedSecond").Value = selectedDateTime.ToString("ss");
            // repaintCalendar(selectedDateTime);
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

        // var SelectedTime = Owner.Get("SelectedTime1");
        // SelectedTime.GetVariable("SelectedDateTime").Value = date;

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

    public int HexColorToDecimal(string hexColor)
    {
        // Remove the '#' character if present
        if (hexColor.StartsWith("#"))
        {
            hexColor = hexColor.Substring(1);
        }

        // Convert the hexadecimal string to a decimal integer
        int decimalColor = Convert.ToInt32(hexColor, 16);
        return decimalColor;
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
        //var SelectedTime = Owner.Get("SelectedTime1");
        // SelectedTime.GetVariable("SelectedMinute").Value = minutes;
        // SelectedTime.GetVariable("SelectedHour").Value = hours;
        // SelectedTime.GetVariable("SelectedSecond").Value = seconds;
        int hours = UISelected.GetVariable("Hour").Value ;
        int minutes = UISelected.GetVariable("Minute").Value ;
        int seconds = UISelected.GetVariable("Second").Value ;
        DateTime dateTime = UISelected.Value;
        string dateString = dateTime.ToString("yyyy-MM-dd")+" "+hours+":"+minutes+":"+seconds;
        // UISelected.GetVariable("Year").Value ;
        // UISelected.GetVariable("Month").Value ;
        // UISelected.GetVariable("Day").Value ;
       

        // Add the TimeSpan to the original date
        DateTime newDate = DateTime.Parse(dateString);
        //SelectedTime.GetVariable("SelectedDateTime").Value = newDate;
        //SelectedTime = UISelected;
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
        string month = LogicObject.GetVariable("DisplayMonth").Value;
        string year = LogicObject.GetVariable("DisplayYear").Value;
        string nextMonth = year + "-" + month + "-01";
        DateTime dateTime = DateTime.Parse(nextMonth);
        repaintCalendar(dateTime.AddMonths(1));
    }

    [ExportMethod]
    public void PreviousMonth()
    {
        string month = LogicObject.GetVariable("DisplayMonth").Value;
        string year = LogicObject.GetVariable("DisplayYear").Value;
        string nextMonth = year + "-" + month + "-01";
        DateTime dateTime = DateTime.Parse(nextMonth);
        repaintCalendar(dateTime.AddMonths(-1));
    }

    [ExportMethod]
    public void BackToday()
    {
        DateTime currentDate = GetCurrentDate();
        repaintCalendar(currentDate);
    }

    public IUAVariable SelectedTime; // = LogicObject.GetVariable("SelectedTime");
    public IUAVariable UISelected; 
}
