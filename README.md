About
=====

QvEDXTrigger is a command line tool for QlikView 11 to trigger External Event Tasks known as EDX. The tool is developed to be as generic as possible and work in most situations. For example it features advanced and very customizable logging capabilities and it also takes care of things behind the scene to make it as easy as possible to use with environments with multiple distribution services.

This project aim to both demonstrate and inspire how to work with the QlikView Management Service API (QMS) while at the same time being a useful and fully working example application ready for production use.

Help screen
-----------

	Usage: QvEDXTrigger [options]
	Trigger QlikView EDX Enabled Tasks from command line.

	Options:
	  -t, --task=TaskNameOrID    TaskNameOrID of task to trigger (case-sensitive)
	  -p, --password[=Password]  Password for the task (if required)
	      --variable[=Name]      Name of variable to change
	      --values[=Value(s)]    Value(s) to assign the variable above (semicolon
	                               or comma separated)
	  -s, --service[=address]    Location of QlikView Management Service,
	                               defaults to address in configuration file
	      --sleep[=seconds]      Sleep number of seconds between status polls
	                               (default is 10 seconds)
	      --timeout[=minutes]    Timeout in number of minutes (default is
	                               indefinitely)
	  -v, --verbose              Increases the verbosity level
	  -V, --version              Show version information
	  -?, -h, --help             Show usage information

	Options can be in the form -option, /option or --long-option

Configuration
-------------

Add the user that is going to execute the tool to the "QlikView EDX" or alternatively the "QlikView Management API" Windows group. These groups does not exist by default and must be created. Restart the computer or log out and back in again to let Windows update the groups.

Change the line below in QvEDXTrigger.exe.config file to reflect the server address of your QlikView Management Service. This address is also configurable through the --service parameter during execution.

	<endpoint address="http://localhost:4799/QMS/Service" binding="basicHttpBinding"
           bindingConfiguration="BasicHttpBinding_IQMS" contract="QMSAPI.IQMS"
           name="BasicHttpBinding_IQMS" behaviorConfiguration="ServiceKeyEndpointBehavior" />

Please note that depending on wether the QlikView environment is using certificates or not it will be necessary to change the extensions and behaviors settings in the configuration file. Disable the WITHOUT certificates block and enable the WITH certificates block according to the comments if using certificates.

It's possible to set the default values for Sleep and Timeout in the configuration file. These are the same parameters as --sleep and --timeout during execution. Sleep is specified in seconds and Timeout in minutes. The Sleep value means the duration between status polls and can be increased to avoid "hammering" the service for tasks that take a long time to finish. Specifying -1 as Timeout means indefinitely and the application will wait for the task to finish, which is usually the desired behaviour.

	<appSettings>
	   <add key="Sleep" value="10" />
	   <add key="Timeout" value="-1" />
	</appSettings>


It's recommended to schedule and run the tool from a batch file, see below for examples. 

Logging
-------

All logging is performed using NLog. NLog is a free logging platform for .NET, Silverlight and Windows Phone with rich log routing and management capabilities. It makes it easy to produce and manage high-quality logs for your application regardless of its size or complexity.

NLog can process diagnostic messages emitted from any .NET language (such as C# or Visual Basic), augment them with contextual information (such as date/time, severity, thread, process, environment enviroment), format them according to your preference and send them to one or more targets such as file or database.

Please see the documentation for NLog how to configure and change it's settings as desired.

* NLog <http://nlog-project.org/>
* NLog configuration file <http://nlog-project.org/wiki/Configuration_file>
* NLog Targets <http://nlog-project.org/wiki/Targets>

Using the default configuration the logfiles will be stored in C:\Programdata\QlikTech\External Event Logs\\{DATE}\\{TASKNAMEORID}.txt and can look like the examples below.

The GUID is an execution ID to keep each task execution apart from each other, making it easy to see what belongs to what. If no execution ID is available the field with contain -1 which can be mroe related to some application or configuration mistake before the task is able to be triggered.

Everything worked, using minimal and default verbosity:

	2012-11-16 09:26:07.7453|INFO|49e1e4bb-ce8e-4f14-a9f6-0190d90d51b7|Started
	2012-11-16 09:27:13.2335|INFO|49e1e4bb-ce8e-4f14-a9f6-0190d90d51b7|Completed (Duration: 00:01:03)

An error occured, using a higher verbosity level showing some more details. Also a reference to the distribution service own logfile is given.:

	2012-11-16 14:00:31.5365|INFO|9bc76c09-23e0-4d2b-87e9-68b18169fa24|Name: Reload and distribute this fantastic task, ID: 007c4709-8acb-4402-9e36-46b62b7af095, Enabled: Yes, Sleep: 10 seconds, Timeout: Indefinitely
	2012-11-16 14:00:31.5455|INFO|9bc76c09-23e0-4d2b-87e9-68b18169fa24|Started
	2012-11-16 14:01:01.6425|ERROR|9bc76c09-23e0-4d2b-87e9-68b18169fa24|Failed (Error code: 4)
	2012-11-16 14:01:01.6425|ERROR|9bc76c09-23e0-4d2b-87e9-68b18169fa24|Logfile: C:\ProgramData\QlikTech\DistributionService\1\Log\20121116\140031 - Reload and distribute this fantastic task\TaskLog.txt


Error Codes
-----------

The application will exit with error codes depending on what happend to the task. These are more or less the values from TaskResultCode from the QMS API being forwarded and in reality the application will probably never exit with a "Waiting" status. Most common will be "Failed" or "Warning", if not being "Completed" that is.

If however the Timeout value is specified and occurs before the task is completed the application will exit with a "Running" status as the task itself is not aborted because of the timeout.

	Completed   0   The task has completed successfully. (same as 6)
	Waiting     1   The task is waiting to be executed.  
	Running     2   The task is running  
	Aborting    3   The task is aborting the execution.  
	Failed      4   The task failed.  
	Warning     5   The task completed with a warning.  
	Completed   6   The task has completed successfully.
	Not used    7   Reserved
	Not used    8   Reserved
	Error       9   An unknown error occured
	Exception   10  Catch exception error

Examples
--------

Trigger a task:

	QvEDXTrigger.exe --task="Reload and distribute this fantastic task"

Trigger a task with password:

	QvEDXTrigger.exe --task="Reload and distribute this fantastic task" --password=PASSWORD123

Trigger a task with customized sleep value (60 seconds) and increase verbosity level for logging:

	QvEDXTrigger.exe --task="Reload and distribute this fantastic task" --sleep:60 --verbose

Trigger a task with custom variable name and values:

	QvEDXTrigger.exe --task="Reload and distribute this fantastic task" --variable=Numbers --values=1;2;3;4;5

Trigger a task identified by GUID using a custom service address:

	QvEDXTrigger.exe --task=007c4709-8acb-4402-9e36-46b62b7af095 --service=http://blueberryserver:4799/QMS/Service

License
-------

This software is made available "AS IS" without warranty of any kind under The Mit License (MIT). QlikTech support agreement does not cover support for this software.

Meta
----

* Code: `git clone git://github.com/braathen/qv-edx-trigger.git`
* Home: <https://github.com/braathen/qv-edx-trigger>
* Bugs: <https://github.com/braathen/qv-edx-trigger/issues>
