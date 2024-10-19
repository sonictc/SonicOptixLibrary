#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.SQLiteStore;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.Store;
using FTOptix.RAEtherNetIP;
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using System.Linq;
#endregion

public class HotBackupControllerSwitch : BaseNetLogic
{
    [ExportMethod]
    public void GenerateNodesIntoModel()
    {
        // Get nodes where to search PLC tags
        startingNodeToFetch = InformationModel.Get(LogicObject.GetVariable("StartingNodeToFetch").Value);
        if (startingNodeToFetch == null)
        {
            Log.Error(this.GetType().Name, "Cannot get StartingNodeToFetch");
            return;
        }
        // Delete existing nodes if needed
        deleteExistingTags = LogicObject.GetVariable("DeleteExistingTags").Value;
        // Get the node where we are going to create the Model variables/objects
        targetNode = InformationModel.Get<Folder>(LogicObject.GetVariable("TargetFolder").Value);
        if (targetNode == null)
        {
            Log.Error(this.GetType().Name, "Cannot get TargetNode");
            return;
        }
        // Start procedure
        generateNodesTask = new LongRunningTask(GenerateNodesMethod, LogicObject);
        generateNodesTask.Start();
    }
     [ExportMethod]
    public void GenerateNodesIntoModel2()
    {
        // Get nodes where to search PLC tags
        startingNodeToFetch = InformationModel.Get(LogicObject.GetVariable("StartingNodeToFetch2").Value);
        if (startingNodeToFetch == null)
        {
            Log.Error(this.GetType().Name, "Cannot get StartingNodeToFetch");
            return;
        }
        // Delete existing nodes if needed
        deleteExistingTags = LogicObject.GetVariable("DeleteExistingTags").Value;
        // Get the node where we are going to create the Model variables/objects
        targetNode = InformationModel.Get<Folder>(LogicObject.GetVariable("TargetFolder").Value);
        if (targetNode == null)
        {
            Log.Error(this.GetType().Name, "Cannot get TargetNode");
            return;
        }
        // Start procedure
        generateNodesTask = new LongRunningTask(GenerateNodesMethod, LogicObject);
        generateNodesTask.Start();
    }

    private void GenerateNodesMethod()
    {
        
        GenerateNodes(startingNodeToFetch);
        generateNodesTask?.Dispose();
    }

    private LongRunningTask generateNodesTask;
    private IUANode startingNodeToFetch;
    private IUANode targetNode;
    private bool deleteExistingTags;

    /// <summary>
    /// Generates a set of objects and variables in model in order to have a "copy" of a set of imported tags, retrieved from a starting node
    /// </summary>
    public void GenerateNodes(IUANode startingNode)
    {
        var modelFolder = InformationModel.Get<Folder>(LogicObject.GetVariable("TargetFolder").Value);

        if (modelFolder == null)
        {
            Log.Error($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", "Cannot get to target folder");
            return;
        }

        CreateModelTag(startingNode, modelFolder);
        CheckDynamicLinks();
    }

    private void CreateModelTag(IUANode fieldNode, IUANode parentNode, string browseNamePrefix = "")
    {
        switch (fieldNode)
        {
            case TagStructure:
                if (!IsTagStructureArray(fieldNode))
                    CreateOrUpdateObject(fieldNode, parentNode, browseNamePrefix);
                else
                    CreateOrUpdateObjectArray(fieldNode, parentNode);
                break;
            case FTOptix.Core.Folder:
                IUANode newFolder = null;
                if (fieldNode.NodeId != startingNodeToFetch.NodeId)
                    newFolder = CreateFolder(fieldNode, parentNode);
                else
                    newFolder = parentNode;

                foreach (var children in fieldNode.Children)
                    CreateModelTag(children, newFolder, browseNamePrefix);
                break;
            default:
                CreateOrUpdateVariable(fieldNode, parentNode, browseNamePrefix);
                break;
        }
    }

    private static bool IsTagStructureArray(IUANode fieldNode) => ((TagStructure)fieldNode).ArrayDimensions.Length != 0;

    private IUANode CreateFolder(IUANode fieldNode, IUANode parentNode)
    {
        if (parentNode.Get<FTOptix.Core.Folder>(fieldNode.BrowseName) == null)
        {
            var newFolder = InformationModel.Make<FTOptix.Core.Folder>(fieldNode.BrowseName);
            parentNode.Add(newFolder);
            Log.Info($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"Creating \"{Log.Node(newFolder)}\"");
            return newFolder;
        }
        else
        {
            if (deleteExistingTags)
            {
                Log.Info($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"Deleting \"{Log.Node(fieldNode)}\" (DeleteExistingTags is set to True)");
                parentNode.Get<FTOptix.Core.Folder>(fieldNode.BrowseName).Children.Clear();
            }
            else
                Log.Info($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"\"{Log.Node(fieldNode)}\" already exists, skipping creation or children deletion (DeleteExistingTags is set to False)");
            return parentNode.Get<FTOptix.Core.Folder>(fieldNode.BrowseName);
        }
    }

    private void CreateOrUpdateObjectArray(IUANode fieldNode, IUANode parentNode)
    {
        var tagStructureArrayTemp = (TagStructure)fieldNode;

        foreach (var c in tagStructureArrayTemp.Children.Where(c => !IsArrayDimensionsVariable(c)))
            CreateModelTag(c, parentNode, fieldNode.BrowseName + "_");
    }

    private void CreateOrUpdateObject(IUANode fieldNode, IUANode parentNode, string browseNamePrefix = "")
    {
        var existingNode = GetChild(fieldNode, parentNode, browseNamePrefix);
        // Replacing "/" with "_". Nodes with BrowseName "/" are not allowed
        var filedNodeBrowseName = fieldNode.BrowseName.Replace("/", "_");

        if (existingNode == null)
        {
            existingNode = InformationModel.MakeObject(browseNamePrefix + filedNodeBrowseName);
            parentNode.Add(existingNode);
            Log.Info($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"Creating \"{Log.Node(existingNode)}\" object");
        }
        else
            Log.Info($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"Updating \"{Log.Node(existingNode)}\" object");

        foreach (var t in fieldNode.Children.Where(c => !IsArrayDimensionsVariable(c)))
            CreateModelTag(t, existingNode);
    }

    private void CreateOrUpdateVariable(IUANode fieldNode, IUANode parentNode, string browseNamePrefix = "")
    {
        if (IsArrayDimensionsVariable(fieldNode))
            return;

        var existingNode = GetChild(fieldNode, parentNode, browseNamePrefix);

        if (existingNode == null)
        {
            var mTag = (IUAVariable)fieldNode;
            // Replacing "/" with "_". Nodes with BrowseName "/" are not allowed
            var tagBrowseName = mTag.BrowseName.Replace("/", "_");
            existingNode = InformationModel.MakeVariable(tagBrowseName, mTag.DataType, mTag.ArrayDimensions);
            parentNode.Add(existingNode);
            Log.Info($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"Creating \"{Log.Node(existingNode)}\" variable");
        }
        else
            Log.Info($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"Updating \"{Log.Node(existingNode)}\" object");

        ((IUAVariable)existingNode).SetDynamicLink((UAVariable)fieldNode, FTOptix.CoreBase.DynamicLinkMode.ReadWrite);
    }

    private bool IsArrayDimensionsVariable(IUANode n) => n.BrowseName.ToLower().Contains("arraydimen");

    private IUANode GetChild(IUANode child, IUANode parent, string browseNamePrefix = "") => parent.Children[browseNamePrefix + child.BrowseName];

    private void CheckDynamicLinks()
    {
        var dataBinds = targetNode.FindNodesByType<FTOptix.CoreBase.DynamicLink>();
        foreach (var dataBind in dataBinds)
        {
            if (LogicObject.Context.ResolvePath(dataBind.Owner, dataBind.Value).ResolvedNode == null)
                Log.Warning($"{this.GetType().Name}.{System.Reflection.MethodBase.GetCurrentMethod().Name}", $"\"{Log.Node(dataBind.Owner)}\" has unresolved databind, you may need to either: manually reimport the missing PLC tag(s), manually delete the unresolved Model variable(s) or set DeleteExistingTags to True (which may lead to unresolved DynamicLinks somewhere else)");
        }
    }
}
