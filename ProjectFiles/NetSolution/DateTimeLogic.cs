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
using System.Collections.Generic;
using System.Linq;
#endregion

public class DateTimeLogic : BaseNetLogic
{
    public override void Start()
    {
        //   TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        // DateTime currentDate = TimeZoneInfo.ConvertTime(DateTime.Now, timeZoneInfo);
        var DateTimeObject = Owner.Get("DateTimeObject1");
        DateTime selectedDateTime = DateTimeObject.GetVariable("SelectedDateTime").Value;
        

           string firstHistoryTxt = "2000-01-01";
        DateTime firstHistory = DateTime.Parse(firstHistoryTxt);
        if (selectedDateTime < firstHistory){
         DateTime currentDate = GetCurrentDate();
         repaintCalendar(currentDate);
        } else {
            DateTimeObject.GetVariable("SelectedHour").Value = selectedDateTime.ToString("HH");
        DateTimeObject.GetVariable("SelectedMinute").Value = selectedDateTime.ToString("mm");
        DateTimeObject.GetVariable("SelectedSecond").Value = selectedDateTime.ToString("ss");
            repaintCalendar(selectedDateTime);

        }
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    public void repaintCalendar(DateTime date){
      List<DateTime> monthDates = GetMonthDates(date);
        LogicObject.GetVariable("DisplayMonthYearHeader").Value = date.ToString("MMM, yyyy");
        LogicObject.GetVariable("DisplayMonth").Value = date.ToString("MM");
        LogicObject.GetVariable("DisplayYear").Value = date.ToString("yyyy");

        // var DateTimeObject = Owner.Get("DateTimeObject1");
        // DateTimeObject.GetVariable("SelectedDateTime").Value = date;

        var panel = Owner.Get("Layout/DayPanel");
        
        int i = 0;
        foreach (var child in panel.Children.OfType<Panel>()){
               child.GetVariable("IsValid").Value = false;
        }

        foreach (var child in panel.Children.OfType<Panel>()){
            child.GetVariable("IsValid").Value = true;
            child.GetVariable("Date").Value = monthDates[i];
            child.GetVariable("Day").Value = monthDates[i].ToString("dd");
            if (monthDates[i].ToString("MM") == date.ToString("MM")){
                child.GetVariable("IsThisMonth").Value = true;
            }
            else{
                child.GetVariable("IsThisMonth").Value = false;
            }
            i++;
            if (i>monthDates.Count-1){
                break;
            }
        };
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

        for (DateTime current = startOfFirstWeek; current <= endOfLastWeek; current = current.AddDays(1))
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
    public void Confirm(DateTime date,int hours,int minutes,int seconds){
          
         var DateTimeObject = Owner.Get("DateTimeObject1");
        DateTimeObject.GetVariable("SelectedMinute").Value = minutes;
         DateTimeObject.GetVariable("SelectedHour").Value = hours;
          DateTimeObject.GetVariable("SelectedSecond").Value = seconds;
             TimeSpan offset = new TimeSpan(hours, minutes, seconds);

        // Add the TimeSpan to the original date
        DateTime newDate = date.Add(offset);
       DateTimeObject.GetVariable("SelectedDateTime").Value = newDate;
    }
   [ExportMethod]
    public void NextMonth(){
          string month =   LogicObject.GetVariable("DisplayMonth").Value;
        string year = LogicObject.GetVariable("DisplayYear").Value ;
        string nextMonth = year+"-"+month  +"-01";
        DateTime dateTime = DateTime.Parse(nextMonth);
        repaintCalendar(dateTime.AddMonths(1));
    }
   [ExportMethod]
    public void PreviousMonth(){
          string month =   LogicObject.GetVariable("DisplayMonth").Value;
        string year = LogicObject.GetVariable("DisplayYear").Value ;
        string nextMonth = year+"-"+month  +"-01";
        DateTime dateTime = DateTime.Parse(nextMonth);
        repaintCalendar(dateTime.AddMonths(-1));
    }

       [ExportMethod]
    public void BackToday(){
        DateTime currentDate = GetCurrentDate();
         repaintCalendar(currentDate);
    }
}
