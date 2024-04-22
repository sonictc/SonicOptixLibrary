#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.WebUI;
using FTOptix.NativeUI;
using FTOptix.Alarm;
using FTOptix.UI;
using FTOptix.Recipe;
using FTOptix.DataLogger;
using FTOptix.EventLogger;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.Report;
using FTOptix.OPCUAServer;
using FTOptix.System;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
//using FTOptix.UI;
using FTOptix.Core;
#endregion

public class SetStyleSheetToPresentationEngine : BaseNetLogic
{
    public override void Start()
    {
        
    }

    public override void Stop()
    {
  
    }

    [ExportMethod]
    public void SetStyleSheet(NodeId newStyleSheet)
    {
        // Get the new StyleSheet
        var styleSheetNode = InformationModel.Get<StyleSheet>(newStyleSheet);
        if (styleSheetNode == null) {
            Log.Error("SetStyleSheetLogic.SeStyleSheet", "Cannot find new StyleSheet!");
            return;
        }
        // Get the current presentation engine
        var currentPresentationEngine = getPresentationEngine((IUANode)LogicObject);
        if (currentPresentationEngine == null) {
            Log.Error("SetStyleSheetLogic.SeStyleSheet", "Cannot find any PresentationEngine!");
            return;
        }
        ((PresentationEngine)currentPresentationEngine).StyleSheet = newStyleSheet;
    }

    private IUANode getPresentationEngine(IUANode startingPoint)
    {

        //check if the input arg is not the project node
        if (startingPoint.NodeId == Project.Current.NodeId)
        {
            return null;
        }

        //check if the input arg is presentationEngine node if yes , return that node.
        if (startingPoint is NativeUIPresentationEngine || startingPoint is WebUIPresentationEngine)
        {
            return startingPoint;
        }
        else
        {
            //else if the input arg is not presentation engine node. run this loop again using its owner.
            //this will create the loop check until the presentation engine was found.
            return getPresentationEngine(startingPoint.Owner);
        }
    }
}
