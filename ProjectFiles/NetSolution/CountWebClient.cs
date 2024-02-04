#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.NetLogic;
using FTOptix.WebUI;
using System.Collections.Generic;
using System.Linq;
#endregion

public class CountWebClient : BaseNetLogic
{
    private PresentationEngine varwpe;

    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }
        [ExportMethod]
public void GetActiveWebSessionsNumber(NodeId webPresentatonEngine, out int activeWebSessionNumber){
    try
    {
        varwpe=InformationModel.Get<PresentationEngine>(webPresentatonEngine);
        activeWebSessionNumber=GetWebpresentationengingSessions(varwpe).Count();
        Log.Info(activeWebSessionNumber.ToString());
    }
    catch(System.Exception)
    {
       // Log.Error(e.Message);
        activeWebSessionNumber=-1;
    }
}
    private IEnumerable<UISession> GetWebpresentationengingSessions(PresentationEngine webPresentationEngine) => webPresentationEngine.Sessions.Where(s=>s.User!=null);

    
}
