# EC2EventMonitor
A scanner that utilizes an AWS Credential file to build a list of accounts, then scans all regions in each account profile for EC2 instances with events scheduled. Useful if you are managing a large number of AWS accounts.  Supports filtering of instances, and SSH and SCP access via context menus.   File copy allows copying file to all machines shown in filtered view.  Regions, Profiles, and Columns can be included or excluded using the menu bar.


This project should also prove useful for folks who need a starting framework for building an app that will access AWS.  It shows how to load credentials and use those credentials to access AWS API calls.

Written in C# with APIs from the AWS Developer Toolkit.  Requires .NET Framework 4.5.  

Relies on the AWS credential file to build a list of accounts.  
By default , the file should be placed at ~/.aws/credentials, where ~ represents your HOME directory.
This file will be created automatically if you add the credentials in Visual Studio,  or you can create it yourself using instructions from 

           http://docs.aws.amazon.com/aws-sdk-php/guide/latest/credentials.html

Check the section "Using the AWS credentials file and credential profiles".


