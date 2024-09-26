#region Using directives
using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.DataLogger;
using FTOptix.HMIProject;
using FTOptix.NativeUI;
using FTOptix.NetLogic;
using FTOptix.ODBCStore;
using FTOptix.Retentivity;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.UI;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;

#endregion

public class StoreAndForwardLogic : BaseNetLogic
{
    private ODBCStore ExternalDB;
    private SQLiteStore LocalDB;
    private string LocalTableName;
    private string ExternalTableName;
    private bool DeleteTransferedRecord;
    private int LimitTransfer;
    private PeriodicTask periodicTask;

    // private long lastForwardId;

    public override void Start()
    {
        NodeId externalDBId = LogicObject.GetVariable("ExternalDatabase").Value;
        NodeId localDBId = LogicObject.GetVariable("LocalDatabase").Value;
        ExternalDB = (ODBCStore)InformationModel.Get(externalDBId);
        LocalDB = (SQLiteStore)InformationModel.Get(localDBId);
        LocalTableName = LogicObject.GetVariable("LocalTableName").Value;
        ExternalTableName = LogicObject.GetVariable("ExternalTableName").Value;
        DeleteTransferedRecord = LogicObject.GetVariable("DeleteTransferedRecord").Value;
        LimitTransfer = LogicObject.GetVariable("LimitTransfer").Value;
        Table LocalTable = LocalDB.Tables.Get<Table>(LocalTableName);
        var fw = LocalTable.Columns.Get("Forwarded");
        if(fw!=null){
            Log.Info("Found forwarded column");
        
        }else{
            LocalTable.AddColumn("Forwarded",OpcUa.DataTypes.Boolean);
            Log.Info("forwarded column not found, Created forward column successfully");
        }
        int interval = LogicObject.GetVariable("SendInterval").Value;
        periodicTask = new PeriodicTask(StoreAndForward, interval, LogicObject);
        periodicTask.Start();

        Log.Info(
            "Starting store and forward from"
                + LocalDB.BrowseName
                + " to "
                + ExternalDB.BrowseName
                + " every "
                + interval
                + " ms"
        );
    }

    public override void Stop()
    {
        periodicTask.Dispose();
        periodicTask = null;
    }

    public bool IsExternalAvailable()
    {
        bool status = ExternalDB.GetVariable("Status").Value;
        if (status)
        {
            Log.Info("DatabaseConnectionCheck", "Database connection ready");
            LogicObject.GetVariable("ExternalDatabaseStatus").Value = 1;
        }
        else
        {
            Log.Info("DatabaseConnectionCheck", "Database connection lost");
            LogicObject.GetVariable("ExternalDatabaseStatus").Value = 0;
        }
        return status;
    }

    private void ReadBuffer(
        out object[,] newBuffer,
        out string[] newHeaders,
        out long lastForwardId
    )
    {
        newBuffer = null;
        newHeaders = null;
        int rowCount = 0;
        int columnCount = 0;
        lastForwardId = 0;

        string query =
            "SELECT * FROM "
            + LocalTableName
            + " WHERE Forwarded IS NULL ORDER BY Timestamp ASC LIMIT "
            + LimitTransfer.ToString();
        Log.Info("ReadBuffer", "Query:" + query);

        try
        {
            LocalDB.Query(query, out string[] rawHeaders, out object[,] rawBuffer);
            columnCount = rawBuffer.GetLength(1);
            rowCount = rawBuffer.GetLength(0);
            Log.Info(
                "ReadBuffer",
                "Query succeed with result Col:" + columnCount + " and Row:" + rowCount
            );

            if (rowCount != 0)
            {
                //find id column position
                int IdColPos = 5;
                for (int col = 0; col < columnCount; col++)
                {
                    if (rawHeaders[col] == "Id")
                    {
                        IdColPos = col;
                    }
                    //Log.Info("ReadBuffer", "Loop number :" + col + " " + rawHeaders[col]);
                }

                lastForwardId = (long)rawBuffer[rowCount - 1, IdColPos];
                Log.Info("ReadBuffer", "Last Forwarded ID :" + lastForwardId);
                newBuffer = RemoveNullColumn(rawBuffer, IdColPos);
                // Filter the array to exclude the keyword
                newHeaders = rawHeaders.Where(item => item != "Forwarded").ToArray();
                newHeaders = newHeaders.Where(item => item != "Id").ToArray();
            }
            else
            {
                Log.Warning("ReadBuffer", "Empty buffer");
                LogicObject.GetVariable("ProcessStatus").Value = "Empty buffer";
            }
        }
        catch (Exception ex)
        {
            Log.Error("ReadBuffer", ex.Message + ex.InnerException);
            LogicObject.GetVariable("ProcessStatus").Value = "Error reading buffer";
        }
    }

    static object[,] RemoveNullColumn(object[,] originalArray, int idColumnNumber)
    {
        int rowCount = originalArray.GetLength(0);
        int colCount = originalArray.GetLength(1);
        // Log.Info("RemoveNullColumn","Row Count" + rowCount.ToString() + " Column Count " + colCount.ToString());
        // New array with one less column
        object[,] newArray = new object[rowCount, colCount - 2];

        for (int i = 0; i < rowCount; i++)
        {
            for (int j = 0, newJ = 0; j < colCount; j++)
            {
                try
                {
                    if (originalArray[i, j] != null && j != idColumnNumber)
                    {
                        newArray[i, newJ] = originalArray[i, j];
                        //Log.Info(newArray[i, newJ].ToString());
                        newJ++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "RemoveNullColumn",
                        ex.Message + "---" + i + "---" + j + "---" + newJ
                    );
                }
            }
        }

        return newArray;
    }

    private void SendBuffer(string[] headers, object[,] buffer,out bool succeed)
    {
        //Get external database table
        Table myTable = ExternalDB.Tables.Get<Table>(ExternalTableName);

        if (myTable != null)
        {
            try
            {
                myTable.Insert(headers, buffer);
                succeed = true;
                Log.Info("SendBuffer", "Complete insert buffer to external database");
            }
            catch (Exception ex)
            {
                succeed = false;
                Log.Error("SendBuffer", ex.Message);
                LogicObject.GetVariable("ProcessStatus").Value = "Error send buffer";
            }
        }
        else {
            Log.Warning("SendBuffer", "No external table found in config, please check your ODBC table name and config");
            succeed = false;
            LogicObject.GetVariable("ProcessStatus").Value = "No external table found in configuration";
        }
    }

    private void MarkSent(long lastForwardId)
    {
        string query =
            "UPDATE "
            + LocalTableName
            + " SET Forwarded = true WHERE Id <= '"
            + lastForwardId.ToString()
            + "'";
        try
        {
            LocalDB.Query(query, out string[] headers, out object[,] buffer);
            Log.Info("MarkSent", "Completed mark buffer as sent");
        }
        catch (Exception ex)
        {
            Log.Error("MarkSent", ex.Message);
            LogicObject.GetVariable("ProcessStatus").Value = "Error mark buffer as sent";
        }
    }

    private void DeleteLocalRecord(long lastForwardId)
    {
        var DeleteTransfer = LogicObject.GetVariable("DeleteTransferedRecord");
        int[] keepTime = { 0, 0, 0, 0, 0, 0 };
        int i = 0;
        foreach (IUAVariable item in DeleteTransfer.Children.Cast<IUAVariable>())
        {
            keepTime[i] = item.Value;
            // Log.Info("DeleteLocalRecord",keepTime[i].ToString());
            i++;
        }
        DateTime currentTime = GetCurrentDate();
        DateTime lastKeepTime = currentTime
            .AddDays(-keepTime[0])
            .AddMonths(-keepTime[1])
            .AddMonths(-keepTime[2])
            .AddHours(-keepTime[3])
            .AddMinutes(-keepTime[4])
            .AddSeconds(-keepTime[5]);
        Log.Info("DeleteLocalRecord", "Last keep time : " + lastKeepTime.ToString());

        string query =
            "DELETE FROM "
            + LocalTableName
            + " WHERE Id <= "
            + lastForwardId.ToString()
            + " AND LocalTimestamp < '"
            + lastKeepTime.ToString("o", CultureInfo.InvariantCulture)
            + "'";
        try
        {
            LocalDB.Query(query, out string[] Header, out object[,] ResultSet);
            Log.Info(
                "DeleteLocalRecord",
                "Completed delete forwarded record before "
                    + lastKeepTime.ToString("yyyy-MM-dd HH:mm:ss")
            );
          
        }
        catch (Exception ex)
        {
            Log.Error("DeleteLocalRecord", ex.Message);
            LogicObject.GetVariable("ProcessStatus").Value = "Error deleting local record";
        }
    }

    public static DateTime GetCurrentDate()
    {
        TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        return TimeZoneInfo.ConvertTime(DateTime.Now, timeZoneInfo);
    }

    private void CheckRemainingBuffer(out bool bufferRemain)
    {
        try
        {
            LocalDB.Query(
                "SELECT * FROM "
                    + LocalTableName
                    + " WHERE Forwarded IS NULL ORDER BY Timestamp ASC LIMIT "
                    + LimitTransfer.ToString(),
                out string[] Header,
                out object[,] ResultSet
            );
            if (ResultSet.GetLength(0) == 0)
            {
                bufferRemain = false;
            }
            else
                bufferRemain = true;
        }
        catch (Exception ex)
        {
            bufferRemain = false;
            Log.Error("CheckRemainingBuffer", ex.Message);
        }
    }

    [ExportMethod]
    public void StoreAndForward()
    {
        bool dbConnection = IsExternalAvailable();
        long lastForwardId = 0;
        if (dbConnection)
        {
            CheckRemainingBuffer(out bool bufferRemain);
            if (bufferRemain)
            {
                ReadBuffer(out object[,] buffer, out string[] headers, out lastForwardId);
                SendBuffer(headers, buffer ,out bool succeed);
                if (succeed)
                {
                MarkSent(lastForwardId);
                DeleteLocalRecord(lastForwardId);
                LogicObject.GetVariable("ProcessStatus").Value = "Normal";
                }
                
            }
            else
            {
                LogicObject.GetVariable("ProcessStatus").Value = "All buffer sent, no buffer remain";
                Log.Info("StoreAndForward", "No buffer remaining");
                DeleteLocalRecord(lastForwardId);
            }
        }
    }
}
