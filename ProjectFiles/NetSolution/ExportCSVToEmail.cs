#region Using directives
using System;
using System.IO;
using System.Text;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.EventLogger;
using FTOptix.OPCUAServer;
using FTOptix.UI;
using FTOptix.Store;
using FTOptix.SQLiteStore;
using FTOptix.Core;
using System.Net.Mail;
using System.Collections.Generic;
using System.Net;
using FTOptix.CommunicationDriver;
using FTOptix.RAEtherNetIP;
using FTOptix.DataLogger;
#endregion

public class ExportCSVToEmail : BaseNetLogic
{
    public override void Start()
    {
        
        ValidateCertificate();
        emailStatus = GetVariableValue("EmailSendingStatus");
        maxDelay = GetVariableValue("DelayBeforeRetry");
        maxDelay.VariableChange += RestartPeriodicTask;
    }

    private void RestartPeriodicTask(object sender, VariableChangeEventArgs e)
    {
        if (e.NewValue < 10000 || e.NewValue == null)
        {
            Log.Warning("EmailSenderLogic", "Minimum delay before retrying should be 10 seconds");
            return;
        }

        retryPeriodicTask?.Cancel();
        retryPeriodicTask = new PeriodicTask(SendQueuedMessage, e.NewValue, LogicObject);
        retryPeriodicTask.Start();
    }

    
    public void SendEmail(string mailToAddress, string mailSubject, string mailBody)
    {
        if (!InitializeAndValidateSMTPParameters())
            return;

        if (!ValidateEmail(mailToAddress, mailSubject, mailBody))
            return;

        var fromAddress = new MailAddress(senderAddress, "From");
        var toAddress = new MailAddress(mailToAddress, "To");

        if (retryPeriodicTask == null)
        {
            var delayBeforeRetry = GetVariableValue("DelayBeforeRetry").Value;
            if (delayBeforeRetry >= 10000)
            {
                retryPeriodicTask = new PeriodicTask(SendQueuedMessage, delayBeforeRetry, LogicObject);
                retryPeriodicTask.Start();
            }
        }

        smtpClient = new SmtpClient
        {
            Host = smtpHostname,
            Port = smtpPort,
            EnableSsl = enableSSL,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(fromAddress.Address, senderPassword)
        };
        var message = CreateEmailMessage(fromAddress, toAddress, mailBody, mailSubject);
        TrySendEmail(message);
    }

    private MailMessageWithRetries CreateEmailMessage(MailAddress fromAddress, MailAddress toAddress, string mailBody, string mailSubject)
    {
        var mailMessage = new MailMessageWithRetries(fromAddress, toAddress)
        {
            Body = mailBody,
            Subject = mailSubject,
            BodyEncoding = System.Text.Encoding.UTF8,
        };

        var attachment = GetVariableValue("Attachment").Value;
        if (!string.IsNullOrEmpty(attachment))
        {
            var attachmentUri = new ResourceUri(attachment);
            mailMessage.Attachments.Add(new Attachment(attachmentUri.Uri));
        }

        mailMessage.ReplyToList.Add(toAddress);
        return mailMessage;
    }

    private void TrySendEmail(MailMessageWithRetries message)
    {
        if (!CanRetrySendingMessage(message))
            return;

        using (message)
        {
            try
            {
                message.AttemptNumber++;
                Log.Info("EmailSender", $"Sending Email... ");
                smtpClient.Send(message);

                emailStatus.Value = true;
                Log.Info("EmailSenderLogic", "Email sent successfully");
            }
            catch (SmtpException e)
            {
                emailStatus.Value = false;
                Log.Error("EmailSenderLogic", $"Email failed to send: {e.StatusCode} {e.Message}");

                if (CanRetrySendingMessage(message))
                    EnqueueFailedMessage(message);
            }
        }
    }

    private void SendQueuedMessage(PeriodicTask task)
    {
        if (failedMessagesQueue.Count == 0 || task.IsCancellationRequested)
            return;

        var message = failedMessagesQueue.Pop();

        if (CanRetrySendingMessage(message))
        {
            var retries = GetVariableValue("MaxRetriesOnFailure").Value;
            Log.Info($"Retry Sending email attempt {message.AttemptNumber} of {retries}");
            TrySendEmail(message);
        }
    }

    private void EnqueueFailedMessage(MailMessageWithRetries message)
    {
        failedMessagesQueue.Push(message);
    }

    private bool InitializeAndValidateSMTPParameters()
    {
        senderAddress = (string)GetVariableValue("SenderEmailAddress").Value;
        if (string.IsNullOrEmpty(senderAddress))
        {
            Log.Error("EmailSenderLogic", "Invalid Sender Email address");
            return false;
        }

        senderPassword = (string)GetVariableValue("SenderEmailPassword").Value;
        if (string.IsNullOrEmpty(senderPassword))
        {
            Log.Error("EmailSenderLogic", "Invalid sender password");
            return false;
        }

        smtpHostname = (string)GetVariableValue("SMTPHostname").Value;
        if (string.IsNullOrEmpty(smtpHostname))
        {
            Log.Error("EmailSenderLogic", "Invalid SMTP hostname");
            return false;
        }

        smtpPort = (int)GetVariableValue("SMTPPort").Value;
        enableSSL = (bool)GetVariableValue("EnableSSL").Value;

        return true;
    }

    private bool CanRetrySendingMessage(MailMessageWithRetries message)
    {
        var maxRetries = GetVariableValue("MaxRetriesOnFailure").Value;
        return maxRetries >= 0 && message.AttemptNumber <= maxRetries;
    }

    private class MailMessageWithRetries : MailMessage
    {
        public MailMessageWithRetries(MailAddress fromAddress, MailAddress toAddress)
            : base(fromAddress, toAddress)
        {

        }

        public int AttemptNumber { get; set; } = 0;
    }

    private IUAVariable GetVariableValue(string variableName)
    {
        var variable = LogicObject.GetVariable(variableName);
        if (variable == null)
        {
            Log.Error($"{variableName} not found");
            return null;
        }
        return variable;
    }

    private bool ValidateEmail(string recieverEmail, string emailSubject, string emailBody)
    {
        if (string.IsNullOrEmpty(emailSubject))
        {
            Log.Error("EmailSenderLogic", "Email subject is empty or malformed");
            return false;
        }

        if (string.IsNullOrEmpty(emailBody))
        {
            Log.Error("EmailSenderLogic", "Email body is empty or malformed");
            return false;
        }

        if (string.IsNullOrEmpty(recieverEmail))
        {
            Log.Error("EmailSenderLogic", "RecieverEmail is empty or null");
            return false;
        }
        return true;
    }

    private void ValidateCertificate()
    {
        if (System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            ServicePointManager.ServerCertificateValidationCallback = (_, __, ___, ____) => { return true; };
    }

    private string senderAddress;
    private string senderPassword;
    private string smtpHostname;
    private int smtpPort;
    private bool enableSSL;

    private SmtpClient smtpClient;
    private PeriodicTask retryPeriodicTask;
    private IUAVariable maxDelay;
    private IUAVariable emailStatus;
    private readonly Stack<MailMessageWithRetries> failedMessagesQueue = new Stack<MailMessageWithRetries>();

    [ExportMethod]
    public void Export()
    {
        try
        {
            var csvPath = GetCSVFilePath();
            if (string.IsNullOrEmpty(csvPath))
                throw new Exception("No CSV file chosen, please fill the CSVPath variable");

            char? fieldDelimiter = GetFieldDelimiter();
            bool wrapFields = GetWrapFields();
            var tableObject = GetTable();
            var storeObject = GetStoreObject(tableObject);
            var selectQuery = GetQuery();

            storeObject.Query(selectQuery, out string[] header, out object[,] resultSet);

            if (header == null || resultSet == null)
                throw new Exception("Unable to execute SQL query, malformed result");

            var rowCount = resultSet.GetLength(0);
            var columnCount = resultSet.GetLength(1);

            using (var csvWriter = new CSVFileWriter(csvPath) { FieldDelimiter = fieldDelimiter.Value, WrapFields = wrapFields })
            {
                csvWriter.WriteLine(header);
                WriteTableContent(resultSet, rowCount, columnCount, csvWriter);
            }

            Log.Info("GenericTableExporter", "The table " + tableObject.BrowseName + " has been succesfully exported to " + csvPath);

            SendEmail("phynaro@gmail.com","LogReport","Hello this is your log report");
        }
        catch (Exception ex)
        {
            Log.Error("GenericTableExporter", "Unable to export table: " + ex.Message);
        }
    }

    private void WriteTableContent(object[,] resultSet, int rowCount, int columnCount, CSVFileWriter csvWriter)
    {
        for (var r = 0; r < rowCount; ++r)
        {
            var currentRow = new string[columnCount];

            for (var c = 0; c < columnCount; ++c)
                currentRow[c] = resultSet[r, c]?.ToString() ?? "NULL";

            csvWriter.WriteLine(currentRow);
        }
    }

    private Table GetTable()
    {
        var alarmEventLoggerVariable = LogicObject.GetVariable("Table");

        if (alarmEventLoggerVariable == null)
            throw new Exception("Table variable not found");

        NodeId tableNodeId = alarmEventLoggerVariable.Value;
        if (tableNodeId == null || tableNodeId == NodeId.Empty)
            throw new Exception("Table variable is empty");

        var tableNode = InformationModel.Get(tableNodeId) as Table;

        if (tableNode == null)
            throw new Exception("The specified table node is not an instance of Store Table type");

        return tableNode;
    }

    private Store GetStoreObject(Table tableNode)
    {
        return tableNode.Owner.Owner as Store;
    }

    private string GetCSVFilePath()
    {
        var csvPathVariable = LogicObject.GetVariable("CSVPath");
        if (csvPathVariable == null)
            throw new Exception("CSVPath variable not found");

        return new ResourceUri(csvPathVariable.Value).Uri;
    }

    private char GetFieldDelimiter()
    {
        var separatorVariable = LogicObject.GetVariable("FieldDelimiter");
        if (separatorVariable == null)
            throw new Exception("FieldDelimiter variable not found");

        string separator = separatorVariable.Value;

        if (separator == String.Empty)
            throw new Exception("FieldDelimiter variable is empty");

        if (separator.Length != 1)
            throw new Exception("Wrong FieldDelimiter configuration. Please insert a single character");

        if (!char.TryParse(separator, out char result))
            throw new Exception("Wrong FieldDelimiter configuration. Please insert a char");

        return result;
    }

    private bool GetWrapFields()
    {
        var wrapFieldsVariable = LogicObject.GetVariable("WrapFields");
        if (wrapFieldsVariable == null)
            throw new Exception("WrapFields variable not found");

        return wrapFieldsVariable.Value;
    }

    private string GetQuery()
    {
        var queryVariable = LogicObject.GetVariable("Query");
        if (queryVariable == null)
            throw new Exception("Query variable not found");

        string query = queryVariable.Value;

        if (String.IsNullOrEmpty(query))
            throw new Exception("Query variable is empty or not valid");

        return query;
    }

    #region CSVFileWriter
    private class CSVFileWriter : IDisposable
    {
        public char FieldDelimiter { get; set; } = ',';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public CSVFileWriter(string filePath)
        {
            streamWriter = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        }

        public CSVFileWriter(string filePath, System.Text.Encoding encoding)
        {
            streamWriter = new StreamWriter(filePath, false, encoding);
        }

        public CSVFileWriter(StreamWriter streamWriter)
        {
            this.streamWriter = streamWriter;
        }

        public void WriteLine(string[] fields)
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < fields.Length; ++i)
            {
                if (WrapFields)
                    stringBuilder.AppendFormat("{0}{1}{0}", QuoteChar, EscapeField(fields[i]));
                else
                    stringBuilder.AppendFormat("{0}", fields[i]);

                if (i != fields.Length - 1)
                    stringBuilder.Append(FieldDelimiter);
            }

            streamWriter.WriteLine(stringBuilder.ToString());
            streamWriter.Flush();
        }

        private string EscapeField(string field)
        {
            var quoteCharString = QuoteChar.ToString();
            return field.Replace(quoteCharString, quoteCharString + quoteCharString);
        }

        private StreamWriter streamWriter;

        #region IDisposable Support
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
                streamWriter.Dispose();

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
    #endregion
}
