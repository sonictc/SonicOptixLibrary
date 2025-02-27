#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.DataLogger;
using FTOptix.Store;
using FTOptix.ODBCStore;
using FTOptix.WebUI;
#endregion

public class CreateValueWidget : BaseNetLogic
{
  [ExportMethod]
  public void CreateWidget()
  {
    var valueTemplate = Project.Current.Find("ValueWidget");
    Log.Info(valueTemplate.BrowseName);
    var refObj = Project.Current.Get("CommDrivers").FindVariable("Meter");
    Log.Info(refObj.BrowseName);
    foreach (var meter in refObj.Children)
    {
      var widgetObj = InformationModel.MakeObject(refObj.BrowseName + meter.BrowseName, valueTemplate.NodeId);
      var ref_tag = widgetObj.GetVariable("Meter");
      ref_tag.Value = meter.NodeId;
      Owner.Add(widgetObj);
      Log.Info("Adding " + meter.BrowseName + " widget successfully");
    }
  }
}
