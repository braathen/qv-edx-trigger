/*
The MIT License (MIT)

Copyright (c) 2012 Rikard Braathen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Threading;
using NDesk.Options;
using qv_edx_trigger.QMSAPI;
using Exception = System.Exception;
using LogLevel = NLog.LogLevel;

namespace qv_edx_trigger
{
    class Program
    {
        static void Main(string[] args)
        {

            bool help = false;
            bool version = false;

            EDXTask t = new EDXTask();

            try
            {
                t.Sleep = ConfigurationManager.AppSettings["Sleep"] != null ? Int32.Parse(ConfigurationManager.AppSettings["Sleep"]) : 10;
                t.TimeOut = ConfigurationManager.AppSettings["Timeout"] != null ? Int32.Parse(ConfigurationManager.AppSettings["Timeout"]) : -1;
                t.Wait = ConfigurationManager.AppSettings["Wait"] != null ? Int32.Parse(ConfigurationManager.AppSettings["Wait"]) : 0;

                var p = new OptionSet()
                            {
                                {"t|task=", "{TaskNameOrID} of task to trigger (case-sensitive)", v => t.TaskNameOrId = v},
                                {"p|password:", "{Password} for the task (if required)", v => t.Password = v},
                                {"variable:", "{Name} of variable to change", v => t.VariableName = v},
                                {"values:", "{Value(s)} to assign the variable above (semicolon or comma separated)", v =>t.VariableValues = new List<string>(v.Split(new[] {';', ','}, StringSplitOptions.RemoveEmptyEntries))},
                                {"s|service:", "Location of QlikView Management Service, defaults to {address} in configuration file", v => t.ServiceAddress = v },
                                {"sleep:", "Sleep number of {seconds} between status polls (default is " + t.Sleep / 1000 + " seconds)", v => t.Sleep = Int32.Parse(v)},
                                {"timeout:", "Timeout in number of {minutes} (default is " + (t.TimeOut < 0 ? "indefinitely" : Convert.ToString(t.TimeOut / 1000 / 60)) + ")",v => t.TimeOut = Int32.Parse(v)},
                                {"wait:", "Wait number of {seconds} (default is " + t.Wait / 1000 + ") before executing task",v => t.Wait = Int32.Parse(v)},
                                {"v|verbose", "Increases the verbosity level", v => { if (v != null) ++t.Verbosity; }},
                                {"V|version", "Show version information", v => version = v != null},
                                {"?|h|help", "Show usage information", v => help = v != null},
                            };

                p.Parse(args);

                if (help || args.Length == 0)
                {
                    ShowHelp(p);
                    return;
                }

            }
            catch (Exception ex)
            {
                LogHelper.Log(LogLevel.Error, ex.Message.Replace(Environment.NewLine, " "), new LogProperties { TaskNameOrId = t.TaskNameOrId, ExecId = "-1" });
                Environment.ExitCode = 9;
                return;
            }

            if (version)
            {
                Console.WriteLine("QvEDXTrigger version 20150306\n");
                Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY.");
                Console.WriteLine("This is free software, and you are welcome to redistribute it");
                Console.WriteLine("under certain conditions.\n");
                Console.WriteLine("Code: git clone git://github.com/braathen/qv-edx-trigger.git");
                Console.WriteLine("Home: <https://github.com/braathen/qv-edx-trigger>");
                Console.WriteLine("Bugs: <https://github.com/braathen/qv-edx-trigger/issues>\n");
                return;
            }

            if (t.Wait > 0)
            {
                Console.WriteLine("Waiting for " + t.Wait /1000 + " seconds...");
                Thread.Sleep(t.Wait);
            }

            Environment.ExitCode = TriggerTask(t);
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: QvEDXTrigger [options]");
            Console.WriteLine("Trigger QlikView EDX Enabled Tasks from command line.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
            Console.WriteLine("Options can be in the form -option, /option or --long-option");
        }

        static int TriggerTask(EDXTask t)
        {

            /*
            Completed   0   The task has completed successfully. (same as 6)
            Waiting     1   The task is waiting to be executed.  
            Running     2   The task is running  
            Aborting    3   The task is aborting the execution.  
            Failed      4   The task failed.  
            Warning     5   The task completed with a warning.  
            Completed   6   The task has completed successfully.
            Error       9   An unknown error occured
            Exception   10  Catch exception error
            */

            var exitCode = 0;

            LogProperties logProperties = new LogProperties {TaskNameOrId = t.TaskNameOrId, ExecId = "-1"};

            try
            {
                // Create a QMS API client
                IQMS apiClient = String.IsNullOrEmpty(t.ServiceAddress) ? new QMSClient() : new QMSClient("BasicHttpBinding_IQMS", t.ServiceAddress);

                // Retrieve a time limited service key
                ServiceKeyClientMessageInspector.ServiceKey = apiClient.GetTimeLimitedServiceKey();

                TaskInfo taskInfo = new TaskInfo();

                if (!IsGuid(t.TaskNameOrId))
                {
                    List<TaskInfo> taskList = apiClient.FindEDX(t.TaskNameOrId);

                    // Find correct task with support for multiple qds
                    if (taskList.Count > 0)
                    {
                        int i = 0;

                        for (i = 0; i < taskList.Count; i++)
                        {
                            if (taskList[i].Name == t.TaskNameOrId)
                                break;
                        }

                        taskInfo = new TaskInfo
                                       {
                                           Name = taskList[i].Name,
                                           ID = taskList[i].ID,
                                           QDSID = taskList[i].QDSID,
                                           Enabled = taskList[i].Enabled
                                       };
                    }
                }
                else
                {
                    taskInfo = apiClient.GetTask(Guid.Parse(t.TaskNameOrId));
                }

                if (taskInfo.Name != null)
                {
                    // Trigger the task
                    TriggerEDXTaskResult result = apiClient.TriggerEDXTask(Guid.Empty, taskInfo.Name, t.Password, t.VariableName, t.VariableValues);

                    if (result.EDXTaskStartResult == EDXTaskStartResult.Success)
                    {
                        logProperties.ExecId = result.ExecId.ToString();

                        if (t.Verbosity > 0)
                        {
                            LogHelper.Log(LogLevel.Info, String.Format("Name: {0}, ID: {1}, Enabled: {2}, Sleep: {3} seconds, Timeout: {4}", taskInfo.Name, taskInfo.ID, taskInfo.Enabled ? "Yes" : "No", t.Sleep / 1000, t.TimeOut == -1 ? "Indefinitely" : t.TimeOut / 60000 + " minutes"), logProperties);
                        }

                        LogHelper.Log(LogLevel.Info, "Started", logProperties);

                        EDXStatus executionStatus = null;

                        if(t.TimeOut != 0)
                        {
                            // Wait until the task is completed or TIMEOUT has passed.
                            SpinWait.SpinUntil(() =>
                            {
                                Thread.Sleep(t.Sleep);

                                // Retrieve a new service key if sleep time is above 18 minutes to be safe (timeout is 20 minutes in QV11)
                                if (t.Sleep > 18 * 60 * 1000)
                                {
                                    if (t.Verbosity > 1)
                                        LogHelper.Log(LogLevel.Info, "GetTimeLimitedServiceKey()", logProperties);

                                    ServiceKeyClientMessageInspector.ServiceKey = apiClient.GetTimeLimitedServiceKey();
                                }

                                // Get the current state of the task.
                                try
                                {
                                    executionStatus = apiClient.GetEDXTaskStatus(Guid.Empty, result.ExecId);
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.Log(LogLevel.Warn, String.Format("{0}", ex.Message.Replace(Environment.NewLine, " ")), logProperties);
                                }

                                if (executionStatus != null && t.Verbosity > 1 && executionStatus.TaskStatus != TaskStatusValue.Running)
                                    LogHelper.Log(LogLevel.Info, executionStatus.TaskStatus.ToString(), logProperties);

                                // Return true if the task has completed.
                                return executionStatus != null && (executionStatus.TaskStatus != TaskStatusValue.Running && executionStatus.TaskStatus != TaskStatusValue.Waiting);
                            }, t.TimeOut);

                            // Write the result
                            if (executionStatus != null)
                            {
                                if (executionStatus.TaskStatus == TaskStatusValue.Completed)
                                {
                                    // datetime parsing needs culture formatting, catch it for now and avoid...
                                    try
                                    {
                                        TimeSpan span = DateTime.Parse(executionStatus.FinishTime).Subtract(DateTime.Parse(executionStatus.StartTime));
                                        LogHelper.Log(LogLevel.Info, String.Format("{0} (Duration: {1})", executionStatus.TaskStatus, span), logProperties);

                                    }
                                    catch (Exception ex)
                                    {
                                        LogHelper.Log(LogLevel.Info, String.Format("{0}", executionStatus.TaskStatus), logProperties);
                                    }
                                }
                                else
                                {
                                    // If something went wrong, point to the logfile for the task execution
                                    exitCode = (Int32)executionStatus.TaskStatus;
                                    LogHelper.Log(LogLevel.Error, String.Format("{0} (Error code: {1})", executionStatus.TaskStatus, exitCode), logProperties);
                                    LogHelper.Log(LogLevel.Error, "Logfile: " + executionStatus.LogFileFullPath, logProperties);
                                }
                            }
                            else
                            {
                                exitCode = 9;
                                LogHelper.Log(LogLevel.Error, String.Format("Failed to get execution status (Error code: {0})", exitCode), logProperties);
                            }
                        }
                    }
                    else
                    {
                        exitCode = 9;
                        LogHelper.Log(LogLevel.Error, String.Format("{0} (Error code: {1})", result.EDXTaskStartResult, exitCode), logProperties);
                    }
                }
                else
                {
                    exitCode = 9;
                    LogHelper.Log(LogLevel.Error, "TaskNotFound (Error code: 9)", logProperties);
                }
            }
            catch (Exception ex)
            {
                exitCode = 10;
                LogHelper.Log(LogLevel.Error, String.Format("{0} (Error code: {1})", ex.Message.Replace(Environment.NewLine, " "), exitCode), logProperties);
            }

            return exitCode;
        }

        public static bool IsGuid(string expression)
        {
            if (expression != null)
            {
                Regex guidRegEx = new Regex(@"^(\{{0,1}([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}{0,1})$");

                return guidRegEx.IsMatch(expression);
            }

            return false;
        }
    }
}
