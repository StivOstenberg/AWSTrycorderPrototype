﻿using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.S3;
using Amazon.S3.Model;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.XPath;
using WinSCP;

namespace AWSMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public DataTable RawEC2Results = GetEC2DetailsTable();
        public DataTable RawUsers = GetUsersDetailsTable();
        public DataTable RawS3 = GetS3DetailsTable();

        //Just defining a default filter to avoid data overload.
        public List<string> defaultusercolumns = new List<string>()
        {
            "AccountID",
            "Username",
            "PwdEnabled ",
            "PwdLastUsed",
            "MFA Active",
            "AccessKey1-LastUsedService",
            "Policy-List",
            "Access-Keys",
            "Groups"
            
        };

        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp, Object value);
        UpdateProgressBarDelegate doupdatePbDelegate;
        double regioncounter = 0;

        //Code required to manipulate Windows.
        [System.Runtime.InteropServices.DllImport("USER32.DLL", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [System.Runtime.InteropServices.DllImport("USER32.DLL", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public string saveas()
        {
            SaveFileDialog _SD = new SaveFileDialog();
            _SD.Filter = "Excel File (*.xls)|*.xls*";
            _SD.FileName = "TrycorderOut" + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + "-" + DateTime.Now.Hour + DateTime.Now.Minute;
            _SD.Title = "Save As";
            if (_SD.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return(_SD.FileName);
            }
            return "";
        }

        public string Filepicker()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "All Files|*.*|Script (*.py, *.sh)|*.py*;*.sh"; 
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                return (ofd.FileName);
            }
            return ("");
        }

        public string Filepicker(string Filter)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = Filter;
            ofd.InitialDirectory = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return (ofd.FileName);
            }
            return ("");
        }
        public MainWindow()
        {
            DataTable MyDataTable = GetEC2DetailsTable();
            
            InitializeComponent();
            ProgressBar1.Visibility = System.Windows.Visibility.Hidden;
            var Profiles = Amazon.Util.ProfileManager.ListProfileNames().OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase);
            this.ShowInTaskbar = true;
            this.Topmost = false;
            

            foreach(string aProfile in Profiles)
            {

                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem();
                mi.IsCheckable = true;
                mi.Header = aProfile;
                
                mi.IsChecked = true;
                mi.StaysOpenOnClick = true;
                mi.Click += ProfileChecked;
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[1];
                Proot.Items.Add(mi);
            }

            foreach (var aUserField in RawUsers.Columns)
            {
                string thisfield = aUserField.ToString();
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem();
                mi.IsCheckable = true;
                mi.Header = aUserField.ToString();
                if(defaultusercolumns.Contains(thisfield))
                { 
                    mi.IsChecked = true; 
                }
                else
                {
                    mi.IsChecked = false;
                }
                mi.StaysOpenOnClick = true;
                mi.Click += UserChecked;
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[4];
                Proot.Items.Add(mi);
            }

            ShowHideUserColumns();

            var Regions = RegionEndpoint.EnumerableAllRegions;
            foreach(var aregion in Regions)  //Build the Region Select Menu
            {
                //Skip Beijing and USGov
                if (aregion == Amazon.RegionEndpoint.USGovCloudWest1) continue;
                if (aregion == Amazon.RegionEndpoint.CNNorth1) continue;
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem();
                mi.IsCheckable = true;
                mi.Header = aregion;
                mi.IsChecked = true;
                mi.Click += ProfileChecked;
                mi.StaysOpenOnClick = true;
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[2];
                Proot.Items.Add(mi);
            }
            ColumnCombo.Items.Add("_ANY_");
            foreach(var acolumn in GetEC2DetailsTable().Columns) //Set the Column Show Hide menu up
            {
                ColumnCombo.Items.Add(acolumn.ToString());
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem();
                mi.IsCheckable = true;
                mi.Header = acolumn.ToString();
                mi.IsChecked = true;
                mi.StaysOpenOnClick = true;
                mi.Click += ColumnsClick;
                mi.Checked += ColumnsClick;
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[3];
                Proot.Items.Add(mi);
            }
            ColumnCombo.SelectedItem = "_ANY_";
        }

        static DataTable GetS3DetailsTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("AccountID", typeof(string));
            table.Columns.Add("Profile", typeof(string));
            table.Columns.Add("Bucket", typeof(string));
            table.Columns.Add("Region", typeof(string));
            table.Columns.Add("CreationDate", typeof(string));
            table.Columns.Add("LastAccess", typeof(string));// This works, but data returned is bogus.
            table.Columns.Add("Owner", typeof(string));
            table.Columns.Add("Grants", typeof(string));


            table.Columns.Add("WebsiteHosting", typeof(string));
            table.Columns.Add("Logging", typeof(string));
            table.Columns.Add("Events", typeof(string));
            table.Columns.Add("Versioning", typeof(string));
            table.Columns.Add("LifeCycle", typeof(string));
            table.Columns.Add("Replication", typeof(string));
            table.Columns.Add("Tags", typeof(string));
            table.Columns.Add("RequesterPays", typeof(string));
            
            return table;
        }

        static DataTable GetEC2DetailsTable()
        {
            // Here we create a DataTable .
            DataTable table = new DataTable();
            table.Columns.Add("AccountID", typeof(string));
            table.Columns.Add("Profile", typeof(string));
            table.Columns.Add("Region", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("InstanceID", typeof(string));
            table.Columns.Add("AMI", typeof(string));
            table.Columns.Add("AMIDescription", typeof(string));
            table.Columns.Add("AvailabilityZone", typeof(string));
            table.Columns.Add("Platform", typeof(string));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("Events", typeof(string));
            table.Columns.Add("EventList", typeof(string));
            table.Columns.Add("Tags", typeof(string));
            table.Columns.Add("Priv_IP", typeof(string));
            table.Columns.Add("Pub_IP", typeof(string));
            table.Columns.Add("Pub_DNS", typeof(string));
            table.Columns.Add("State", typeof(string));
            table.Columns.Add("vType", typeof(string));
            table.Columns.Add("iType", typeof(string));
            table.Columns.Add("SecurityGroups", typeof(string));
            return table;
        }

        static DataTable GetUsersDetailsTable()
        {
            // Here we create a DataTable .
            DataTable table = new DataTable();
            table.Columns.Add("AccountID", typeof(string));
            table.Columns.Add("Profile", typeof(string));
            table.Columns.Add("UserID", typeof(string));
            //Information from Credential Report
            table.Columns.Add("Username", typeof(string));//user
            table.Columns.Add("ARN", typeof(string));//arn
            table.Columns.Add("CreateDate", typeof(string));//user_creation_time
            table.Columns.Add("PwdEnabled", typeof(string));//password_enabled
            table.Columns.Add("PwdLastUsed", typeof(string));//password_last_used
            table.Columns.Add("PwdLastChanged", typeof(string));//password_last_changed
            table.Columns.Add("PwdNxtRotation", typeof(string));//password_next_rotation
            table.Columns.Add("MFA Active", typeof(string));//mfa_active

            table.Columns.Add("AccessKey1-Active", typeof(string));//access_key_1_active
            table.Columns.Add("AccessKey1-Rotated", typeof(string));//access_key_1_last_rotated
            table.Columns.Add("AccessKey1-LastUsedDate", typeof(string));//access_key_1_last_used_date
            table.Columns.Add("AccessKey1-LastUsedRegion", typeof(string));//access_key_1_last_used_region
            table.Columns.Add("AccessKey1-LastUsedService", typeof(string));//access_key_1_last_used_service

            table.Columns.Add("AccessKey2-Active", typeof(string));//access_key_2_active
            table.Columns.Add("AccessKey2-Rotated", typeof(string));//access_key_2_last_rotated
            table.Columns.Add("AccessKey2-LastUsedDate", typeof(string));//access_key_2_last_used_date
            table.Columns.Add("AccessKey2-LastUsedRegion", typeof(string));//access_key_2_last_used_region
            table.Columns.Add("AccessKey2-LastUsedService", typeof(string));//access_key_2_last_used_service

            table.Columns.Add("Cert1-Active", typeof(string));//cert_1_active
            table.Columns.Add("Cert1-Rotated", typeof(string));//cert_1_last_rotated
            table.Columns.Add("Cert2-Active", typeof(string));//cert_2_active
            table.Columns.Add("Cert2-Rotated", typeof(string));//cert_2_last_rotated

            table.Columns.Add("User-Policies", typeof(string));
            table.Columns.Add("Access-Keys", typeof(string));
            table.Columns.Add("Groups", typeof(string));

            
            return table;
        }





        public Dictionary<string, DataTable> ScanProfile(ScanRequest Request)
        {
            Dictionary<string, DataTable> ScanResults = new Dictionary<string, DataTable>();
            DataTable UserDetailsTable = GetUsersDetailsTable();
            DataTable EC2DetailsTable = GetEC2DetailsTable();
            DataTable S3DetailsTable = GetS3DetailsTable();
            string accountid = "";
            Amazon.Runtime.AWSCredentials credential;
            var aprofile = Request.Profile;
            var regions2process = Request.Regions;
            var SubmitResults = Request.ResultQueue;
            try
            {
                credential = new Amazon.Runtime.StoredProfileAWSCredentials(aprofile);
                //Try to get the AccountID ID//


                #region UserDetails
                var iam = new AmazonIdentityManagementServiceClient(credential);

                var myUserList = iam.ListUsers().Users;

                try
                {
                    accountid = myUserList[0].Arn.Split(':')[4];//Get the ARN and extract the AccountID ID
                    accountid = "ID: " + accountid;// Prefix added because Excel exsucks.
                }
                catch
                {
                    accountid = "?";
                }

                try // Send command to AWS to generate a Credential Report
                { var createcredreport = iam.GenerateCredentialReport(); }
                catch (Exception)
                { throw; }

                bool needreport = true;

                Amazon.IdentityManagement.Model.GetCredentialReportResponse credreport = new GetCredentialReportResponse();
                DateTime getreportstart = DateTime.Now;
                DateTime getreportfinish = DateTime.Now;
                while (needreport)
                {
                    try
                    {
                        credreport = iam.GetCredentialReport();
                        needreport = false;
                        getreportfinish = DateTime.Now;
                        var dif = getreportstart - getreportfinish;  //Just a check on how long it takes.


                        //Extract data from CSV Stream into DataTable
                        var streambert = credreport.Content;
                        streambert.Position = 0;
                        StreamReader sr = new StreamReader(streambert);
                        string myStringRow = sr.ReadLine();
                        if (myStringRow != null) myStringRow = sr.ReadLine();//Dump the header line
                        while (myStringRow != null)
                        {
                            var arow = myStringRow.Split(",".ToCharArray()[0]);

                            var newrow = new object[UserDetailsTable.Columns.Count];
                            newrow[0] = accountid;
                            newrow[1] = aprofile;
                            newrow[2] = ""; //UserID not in report. pull it later.
                            newrow[3] = arow[0];
                            newrow[4] = arow[1];
                            newrow[5] = arow[2];
                            newrow[6] = arow[3];
                            newrow[7] = arow[4];
                            newrow[8] = arow[5];
                            newrow[9] = arow[6];
                            newrow[10] = arow[7];
                            newrow[11] = arow[8];
                            newrow[12] = arow[9];
                            newrow[13] = arow[10];
                            newrow[14] = arow[11];
                            newrow[15] = arow[12];
                            newrow[16] = arow[13];
                            newrow[17] = arow[14];
                            newrow[18] = arow[15];
                            newrow[19] = arow[16];
                            newrow[20] = arow[17];
                            newrow[21] = arow[18];
                            newrow[22] = arow[19];
                            newrow[23] = arow[20];
                            newrow[24] = arow[21];
                            RawUsers.Rows.Add(newrow);
                            UserDetailsTable.Rows.Add(newrow);
                            myStringRow = sr.ReadLine();
                        }
                        sr.Close();
                        sr.Dispose();


                    }
                    catch (Exception ex)
                    {
                        string test = "";
                        //Deal with this later if necessary.
                    }
                }



                foreach (var auser in myUserList)//Fill in the userID.  Why?  because it exists.
                {
                    string auserid = auser.UserId;
                    string arn = auser.Arn;
                    string username = auser.UserName;
                    string policylist = "";
                    string aklist = "";
                    string groups = "";

                    ListAccessKeysRequest LAKREQ = new ListAccessKeysRequest();
                    LAKREQ.UserName = username;
                    ListAccessKeysResult LAKRES = iam.ListAccessKeys(LAKREQ);
                    foreach (var blivet in LAKRES.AccessKeyMetadata)
                    {
                        if (aklist.Length > 1) aklist += "\n";
                        aklist += blivet.AccessKeyId + "  :  " + blivet.Status;
                    }



                    ListAttachedUserPoliciesRequest LAUPREQ = new ListAttachedUserPoliciesRequest();
                    LAUPREQ.UserName = username;
                    ListAttachedUserPoliciesResult LAUPRES = iam.ListAttachedUserPolicies(LAUPREQ);
                    foreach (var apol in LAUPRES.AttachedPolicies)
                    {
                        if (policylist.Length > 1) policylist += "\n";
                        policylist += apol.PolicyName;
                    }


                    //Need to get policy and group info outta user
                    var groopsreq = new ListGroupsForUserRequest();
                    groopsreq.UserName = username;
                    ListGroupsForUserResult LG = iam.ListGroupsForUser(groopsreq);
                    foreach (var agroup in LG.Groups)
                    {
                        if (groups.Length > 1) groups += "\n";
                        groups += agroup.GroupName;
                    }

                    foreach (DataRow myrow in UserDetailsTable.Rows)
                    {
                        if (myrow["ARN"].Equals(arn))
                        {
                            myrow["UserID"] = auserid;
                            myrow["User-Policies"] = policylist;
                            myrow["Access-Keys"] = aklist;
                            myrow["Groups"] = groups;
                        }
                    }

                }
                #endregion

                #region S3Details

                try { 

                AmazonS3Client S3Client = new AmazonS3Client(credential,Amazon.RegionEndpoint.USEast1);
                ListBucketsResponse response = S3Client.ListBuckets();
                foreach (S3Bucket abucket in response.Buckets)
                {
                    
                    
                    DataRow abucketrow = GetS3DetailsTable().NewRow();
                    var name = abucket.BucketName;

                    try
                    {
                        GetBucketLocationRequest gbr = new GetBucketLocationRequest();
                        gbr.BucketName = name;
                        GetBucketLocationResponse location = S3Client.GetBucketLocation(gbr);
                        var region = location.Location.Value;
                        if (region.Equals("")) region = "us-east-1";
                        var pointy = RegionEndpoint.GetBySystemName(region);



                        //Build a config that references the buckets region.
                        AmazonS3Config S3C = new AmazonS3Config();
                        S3C.RegionEndpoint = pointy;
                        AmazonS3Client BS3Client = new AmazonS3Client(credential, S3C);

                        var createddate = abucket.CreationDate;
                        string owner = "";
                        string grants = "";
                        string tags = "";
                        string lastaccess = "";
                        string defaultpage = "";
                        string website = "";
                        //Now start pulling der einen data.

                        GetACLRequest GACR = new GetACLRequest();
                        GACR.BucketName = name;
                        var ACL = BS3Client.GetACL(GACR);
                        var grantlist = ACL.AccessControlList;
                        owner = grantlist.Owner.DisplayName;
                        foreach (var agrant in grantlist.Grants)
                        {
                            if (grants.Length > 1) grants += "\n";
                            var gName = agrant.Grantee.DisplayName;
                            var gType = agrant.Grantee.Type.Value;
                            var aMail = agrant.Grantee.EmailAddress;

                            if (gType.Equals("Group"))
                            {
                                grants += gType + " - " + agrant.Grantee.URI + " - " + agrant.Permission + " - " + aMail;
                            }
                            else
                            {
                                grants += gName + " - " + agrant.Permission + " - " + aMail;
                            }
                        }



                        GetObjectMetadataRequest request = new GetObjectMetadataRequest();
                        request.BucketName = name;
                        GetObjectMetadataResponse MDresponse = BS3Client.GetObjectMetadata(request);
                        lastaccess = MDresponse.LastModified.ToString();
                        defaultpage = MDresponse.WebsiteRedirectLocation;
if(defaultpage==null)
                            { defaultpage = ""; }
                            else
                            {
                                string rabbit = "";
                            }




                            GetBucketWebsiteRequest GBWReq = new GetBucketWebsiteRequest();
                        GBWReq.BucketName = name;
                        GetBucketWebsiteResponse GBWRes = BS3Client.GetBucketWebsite(GBWReq);

                        defaultpage = GBWRes.WebsiteConfiguration.IndexDocumentSuffix;


                        if (defaultpage != null)
                        {
                            website = @"http://" + name + @".s3-website-" + region + @".amazonaws.com/" + defaultpage;
                        }


                        //Amazon.S3.Model.req




                        abucketrow["AccountID"] = accountid;
                        abucketrow["Profile"] = aprofile;
                        abucketrow["Bucket"] = name;
                        abucketrow["Region"] = region;
                        abucketrow["CreationDate"] = createddate.ToString();
                        abucketrow["LastAccess"] = lastaccess;
                        abucketrow["Owner"] = owner;
                        abucketrow["Grants"] = grants;

                        abucketrow["WebsiteHosting"] = website;
                        abucketrow["Logging"] = "X";
                        abucketrow["Events"] = "X";
                        abucketrow["Versioning"] = "X";
                        abucketrow["LifeCycle"] = "X";
                        abucketrow["Replication"] = "X";
                        abucketrow["Tags"] = "X";
                        abucketrow["RequesterPays"] = "X";
                        S3DetailsTable.Rows.Add(abucketrow.ItemArray);
                    }
                    catch(Exception ex)
                    {
                        
                        abucketrow["AccountID"] = accountid;
                        abucketrow["Profile"] = aprofile;
                        abucketrow["Bucket"] = name;
                        abucketrow["Region"] = "Error opening bucket";
                        S3DetailsTable.Rows.Add(abucketrow.ItemArray);
                    }
                }

                }
                catch(Exception ex)
                {
                   
                    System.Windows.Forms.MessageBox.Show("S3 Failed!\n"+ex);
                }




                #endregion


                #region GetEC2Region

                //////////////////////////////////////////////////////////

                //Foreach aregion
                foreach (var aregion in regions2process)
                {
                    //Skip GovCloud and Beijing. They require special handling and I dont need em.
                    if (aregion == Amazon.RegionEndpoint.USGovCloudWest1) continue;
                    if (aregion == Amazon.RegionEndpoint.CNNorth1) continue;
                    var region = aregion;

                    regioncounter++;



                    //Try to get scheduled events on my Profile/aregion
                    var ec2 = AWSClientFactory.CreateAmazonEC2Client(credential, region);
                    var request = new DescribeInstanceStatusRequest();
                    request.IncludeAllInstances = true;
                    Dispatcher.Invoke(doupdatePbDelegate,
                       System.Windows.Threading.DispatcherPriority.Background,
                        new object[] { System.Windows.Controls.ProgressBar.ValueProperty, regioncounter });
                    var instatresponse = ec2.DescribeInstanceStatus(request);


                    var indatarequest = new DescribeInstancesRequest();




                    foreach (var instat in instatresponse.InstanceStatuses)
                    {

                        indatarequest.InstanceIds.Add(instat.InstanceId);
                        indatarequest.InstanceIds.Sort();
                    }


                    //DescribeInstancesResult DescResult = ec2.DescribeInstances(indatarequest);
                    DescribeInstancesResult DescResult = ec2.DescribeInstances();

                    int count = instatresponse.InstanceStatuses.Count();
                    int itindex = -1;
                    foreach (var instat in instatresponse.InstanceStatuses)
                    {
                        itindex++;
                        //Collect the datases
                        string instanceid = instat.InstanceId;
                        string instancename = "";
                        ProcessingLabel.Content = "Scanning -> Profile:" + aprofile + "    Region: " + region + "   Instance: " + instanceid;
                        Dispatcher.Invoke(doupdatePbDelegate,
                            System.Windows.Threading.DispatcherPriority.Background,
                            new object[] { System.Windows.Controls.ProgressBar.ValueProperty, regioncounter });



                        var status = instat.Status.Status;
                        string AZ = instat.AvailabilityZone;
                        var istate = instat.InstanceState.Name;

                        string profile = aprofile;
                        string myregion = region.ToString();
                        int eventnumber = instat.Events.Count();

                        string eventlist = "";
                        var reservations = DescResult.Reservations;

                        var myinstance = new Reservation();
                        if (instanceid.Contains("i-a8535657"))//Troubleshooting....
                        {
                            var truebert = false;
                        }
                        List<String> innies = new List<String>();
                        foreach (Reservation arez in DescResult.Reservations)
                        {
                            var checky = arez.Instances[0].InstanceId;
                            innies.Add(checky);
                            if(arez.Instances[0].InstanceId.Equals(instanceid))
                            {
                                myinstance = arez;
                            }
                        }
                        innies.Sort();



                        string tags = ""; // Holds the list of tags to print out.

                        var loadtags = (from t in DescResult.Reservations
                                        where t.Instances[0].InstanceId.Equals(instanceid)
                                        select t.Instances[0].Tags).AsEnumerable();

                        Dictionary<string, string> taglist = new Dictionary<string, string>();
                        foreach (var rekey in loadtags)
                        {
                            foreach (var kvp in rekey)
                            {
                                taglist.Add(kvp.Key, kvp.Value);
                            }
                        }

                        foreach (var atag in taglist)//Set instancename, and add value to combobox.
                        {
                            if (atag.Key.Equals("Name"))
                            {
                                instancename = atag.Value;
                            }
                            if (!TagFilterCombo.Items.Contains(atag.Key))
                            {
                                TagFilterCombo.Items.Add(atag.Key);
                            }
                            else
                            {
                                TagFilterCombo.Items.Add(atag.Key);
                            }
                            if (tags.Length > 1)
                            {
                                tags += "\n" + atag.Key + ":" + atag.Value;
                            }
                            else
                            {
                                tags += atag.Key + ":" + atag.Value;
                            }
                        }

                        if (eventnumber > 0)
                        {
                            foreach (var anevent in instat.Events)
                            {
                                eventlist += anevent.Description + "\n";
                            }
                        }


                        var platform = (from t in reservations
                                        where t.Instances[0].InstanceId.Equals(instanceid)
                                        select t.Instances[0].Platform).FirstOrDefault();
                        if (String.IsNullOrEmpty(platform)) platform = "Linux";



                        var Priv_IP = (from t in DescResult.Reservations
                                       where t.Instances[0].InstanceId.Equals(instanceid)
                                       select t.Instances[0].PrivateIpAddress).FirstOrDefault();

                        var disInstance = (from t in reservations
                                   where t.Instances[0].InstanceId.Equals(instanceid)
                                   select t).FirstOrDefault();

                        if (String.IsNullOrEmpty(Priv_IP))
                        {
                            Priv_IP = "?";
                        }

                        var publicIP = (from t in reservations
                                        where t.Instances[0].InstanceId.Equals(instanceid)
                                        select t.Instances[0].PublicIpAddress).FirstOrDefault();
                        if (String.IsNullOrEmpty(publicIP)) publicIP = "";

                        var publicDNS = (from t in reservations
                                         where t.Instances[0].InstanceId.Equals(instanceid)
                                         select t.Instances[0].PublicDnsName).FirstOrDefault();
                        if (String.IsNullOrEmpty(publicDNS)) publicDNS = "";




                        //Virtualization type (HVM, Paravirtual)
                        var ivirtType = (from t in reservations
                                         where t.Instances[0].InstanceId.Equals(instanceid)
                                         select t.Instances[0].VirtualizationType).FirstOrDefault();
                        if (String.IsNullOrEmpty(ivirtType)) ivirtType = "?";

                        // InstanceType (m3/Large etc)
                        var instancetype = (from t in reservations
                                            where t.Instances[0].InstanceId.Equals(instanceid)
                                            select t.Instances[0].InstanceType).FirstOrDefault();
                        if (String.IsNullOrEmpty(instancetype)) instancetype = "?";


                        //Test section to try to pull out AMI data
                        string AMIDesc = "";
                        var AMI = (from t in reservations
                                   where t.Instances[0].InstanceId.Equals(instanceid)
                                   select t.Instances[0].ImageId).FirstOrDefault();
                        if (string.IsNullOrEmpty(AMI))
                        {
                            AMI = "";
                        }
                        else
                        {
                            DescribeImagesRequest DIR = new DescribeImagesRequest();
                            DIR.ImageIds.Add(AMI);
                            var imresp = ec2.DescribeImages(DIR);
                            var idata = imresp.Images;
                            if (idata.Count > 0)
                            {
                                AMIDesc = idata[0].Description;
                            }
                            if (String.IsNullOrEmpty(AMIDesc)) AMIDesc = "AMI Image not accessible!!";
                        }

                        //
                        var SGs = (from t in reservations
                                   where t.Instances[0].InstanceId.Equals(instanceid)
                                   select t.Instances[0].SecurityGroups);

                        string sglist = "";


                        if (SGs.Count() > 0)
                        {
                            foreach (var ansg in SGs.FirstOrDefault())
                            {
                                if (sglist.Length > 2) { sglist += "\n"; }
                                sglist += ansg.GroupName;
                            }
                        }
                        else
                        {
                            sglist = "_NONE!_";
                        }
                        //Add to table
                        if (String.IsNullOrEmpty(sglist)) sglist = "NullOrEmpty";

                        if (String.IsNullOrEmpty(instancename)) instancename = "";
                        string rabbit = accountid + profile + myregion + instancename + instanceid + AZ + status + eventnumber + eventlist + tags + Priv_IP + publicIP + publicDNS + istate + ivirtType + instancetype + sglist;

                        if(instancename.Contains("p1-job"))
                        {
                            string yup = "y";  
                        }


                            EC2DetailsTable.Rows.Add(accountid, profile, myregion, instancename, instanceid, AMI, AMIDesc, AZ, platform, status, eventnumber, eventlist, tags, Priv_IP, publicIP, publicDNS, istate, ivirtType, instancetype, sglist);


                    }

                }
                #endregion
                ScanResults.Add("EC2", EC2DetailsTable);
                ScanResults.Add("Users", UserDetailsTable);
                ScanResults.Add("S3", S3DetailsTable);

                return ScanResults;
            }
            catch (Exception ex)
            {
                //If we failed to connect with creds.

                string error = new string(ex.ToString().TakeWhile(c => c != '\n').ToArray());
                System.Windows.MessageBox.Show(error, Request.Profile.ToString() + " credentials failed to work.\n");
                //Try to flag the menu item so it no longer selectable, and maybe make she red.
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[1];
                foreach (System.Windows.Controls.MenuItem amenuitem in Proot.Items)
                {
                    if (amenuitem.Header.ToString() == aprofile.ToString())
                    {
                        amenuitem.IsCheckable = false;
                        amenuitem.IsChecked = false;
                        amenuitem.Background = Brushes.Red;
                        amenuitem.ToolTip = Request.Profile.ToString() + " credentials failed to work.\n";
                    }
                }


                ScanResults.Add("EC2", GetEC2DetailsTable());
                ScanResults.Add("Users", GetUsersDetailsTable());
                ScanResults.Add("S3", GetS3DetailsTable());

                return ScanResults;



            }

        }


        public class EC2Instance
        {
            public string AccountID { get; set; }
            public string Profile { get; set; }
            public string Region { get; set; }
            public string Name { get; set; }
            public string InstanceID { get; set; }
            public string AvailabilityZone { get; set; }
            public string Platform { get; set; }
            public string Status { get; set; }
            public string Events { get; set; }
            public string EventList { get; set; }
            public string Tags { get; set; }

            public string Priv_IP { get; set; }

            public string Pub_IP { get; set; }
            public string PubDNS { get; set; }
            public string State { get; set; }

            public string vType { get; set; }

            public string iType { get; set; }

            public string SecurityGroups { get; set; }
        }

        private List<EC2Instance>  LoadEC2Data()
        {
            List<EC2Instance> thedata = new List<EC2Instance>();

            return thedata;
        }

        private void EC2EventScanButton_Click(object sender, RoutedEventArgs e)
        {
            Process();
            DoEC2Filter();
        }

        private void Process()
        {
            RawUsers = GetUsersDetailsTable();
            ProgressBar1.Visibility = System.Windows.Visibility.Visible;
            DataTable MyDataTable = GetEC2DetailsTable();
            TagFilterCombo.Items.Clear();

            //Create a new instance of our ProgressBar Delegate that points
            // to the ProgressBar's SetValue method.
            doupdatePbDelegate =  new UpdateProgressBarDelegate(ProgressBar1.SetValue);

            var prof2process = Amazon.Util.ProfileManager.ListProfileNames().OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase).ToList();
            var regions2process = Amazon.RegionEndpoint.EnumerableAllRegions.ToList();
            regions2process.Clear();
            prof2process.Clear();
            var regionsavailable = Amazon.RegionEndpoint.EnumerableAllRegions.ToList();
            //override complete list with one profile.

            //Build Profile List
            foreach (System.Windows.Controls.MenuItem anitem in ProfilesMI.Items)
            {
                if (anitem.IsChecked) prof2process.Add(anitem.Header.ToString());
            }


            //Build Region List
            foreach (System.Windows.Controls.MenuItem anitem in RegionMI.Items)
            {
                if (anitem.IsChecked)
                {
                    //Lookup the endpoint using the region name
                    foreach (var ar in regionsavailable)
                    {
                        var av = ar.DisplayName;
                        var me = anitem.Header;
                     
                        if (anitem.Header.ToString().Contains(ar.DisplayName.ToString())) regions2process.Add(ar);
                    }
                }
            }




            //Configure the ProgressBar
            ProgressBar1.Minimum = 0;
            //Subtract 2 from the count for Beijing and GovWest
            ProgressBar1.Maximum = prof2process.Count * regions2process.Count;
            ProgressBar1.Value = 1;
            regioncounter = 0;

            // Start the loops.  For each Profile, iterate through all regions.
            //Foreach Profile(credential) set aprofile

            //Trying to parallelize this.
            // Establish QUEUE for threads to report back on
            Queue<Dictionary<string, DataTable>> ProfileResults = new Queue<Dictionary<string, DataTable>>();

            ProgressBar1.Visibility = System.Windows.Visibility.Visible;
             foreach (var aprofile in prof2process)
            {
                //Call the ScanProfile function to fill queue
                var arequest = new ScanRequest();
                arequest.Profile = aprofile;
                arequest.Regions = regions2process;
                arequest.ResultQueue = ProfileResults;

                 //How to parallelize this?  
                 ProfileResults.Enqueue( ScanProfile(arequest));//Currently returns values via the ProfileResults Queue.;
                 
            }

            ProgressBar1.Visibility = System.Windows.Visibility.Hidden;

            var tempec2 = GetEC2DetailsTable();
            var tempuser = GetEC2DetailsTable();
            var tempS3 = GetS3DetailsTable();
            
            while(ProfileResults.Count>0)
            {
                var scanresults = ProfileResults.Dequeue();
                tempec2.Merge(scanresults["EC2"]);
                tempuser.Merge(scanresults["Users"]);
                tempS3.Merge(scanresults["S3"]);
            }
            RawEC2Results = tempec2.Copy();
            RawUsers = tempuser.Copy();
            RawS3 = tempS3.Copy();

            DaGrid.ItemsSource = RawEC2Results.AsDataView();
            UserGrid.ItemsSource = RawUsers.AsDataView();
            S3DataGrid.ItemsSource = RawS3.AsDataView();
            ProgressBar1.Visibility = System.Windows.Visibility.Hidden;
            ProcessingLabel.Content  = "Results Displayed: " + RawEC2Results.Rows.Count;


            
        }

        private void TagFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                FilterTagText.Text = TagFilterCombo.SelectedValue.ToString();
            }
            catch
            { }
        }

        private void DoFilterButton_Click(object sender, RoutedEventArgs e)
        {
            DoEC2Filter();

        }
        private void DoEC2Filter()
        {
            if (RawEC2Results.Rows.Count < 1) return;
            var newbie = RawEC2Results.AsEnumerable();

            string fxp = ""; // The string what will build our query.

            string columntofilter = ColumnCombo.SelectedItem.ToString();
            bool anycolumn=false;
            if (columntofilter.Equals("_ANY_"))
            {
                anycolumn = true;
                columntofilter = "Tags";
            }
            

            if (fxp.Length > 2) {
                fxp += " and ";
            }
            else
            {
                if (anycolumn && !FilterTagText.Text.Equals(""))
                {
                    if (RawEC2Results.Rows.Count < 1)
                    {
                       newbie=RawEC2Results.AsEnumerable() ;

                    }
                    try
                    {
                        newbie = RawEC2Results.AsEnumerable().Where(p => p.Field<string>("AccountID").Contains(FilterTagText.Text) || 
                                                                          p.Field<string>("Profile").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Region").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Name").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("InstanceID").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("AvailabilityZone").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Platform").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Status").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Events").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("EventList").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Tags").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Priv_IP").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Pub_IP").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Pub_DNS").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("State").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("vType").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("iType").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("SecurityGroups").Contains(FilterTagText.Text));
                    }
                    catch
                    {
                        newbie = RawEC2Results.AsEnumerable();
                    }
                }
                else
                {
                    newbie = from record in RawEC2Results.AsEnumerable()
                                 where record.Field<string>(columntofilter).Contains(FilterTagText.Text)
                                 select record;
                }
                


                var newdt = GetEC2DetailsTable();
                int count = newbie.Count();
                foreach (var element in newbie)
                {
                    var row = newdt.NewRow();
                    row = element;
                    string thisprofile = row["Profile"].ToString();
                    string thisregion = row["Region"].ToString();
                    bool isprofilechecked = (from System.Windows.Controls.MenuItem t in ProfilesMI.Items
                                        where t.Header.Equals(thisprofile)
                                        select t.IsChecked).FirstOrDefault();

                    bool isregionchecked = (from System.Windows.Controls.MenuItem t in RegionMI.Items
                                             where t.Header.ToString().Equals(thisregion)
                                             select t.IsChecked).FirstOrDefault();
                    //-------------------------------------------------------------------------------------------


                    
                    var boobert = (from System.Windows.Controls.MenuItem t in RegionMI.Items
                                            
                                            select t.Header);

                    //-------------------------------------------------------------------------------------------
                    string daProfile = (string)row.Table.Columns[0].ToString();



                    if(isprofilechecked & isregionchecked)
                    { 
                    newdt.ImportRow(row);
                    }

                }
                DaGrid.ItemsSource = newdt.AsDataView();
                ProcessingLabel.Content = "Filtered Results Displayed: " + newdt.Rows.Count + " of " + RawEC2Results.Rows.Count;
                
            }
            ShowHideEC2Columns();
        }

        private void DoUserFilter()
        {
            if (RawUsers.Rows.Count < 1) return;
            var usource = RawUsers.Copy();

            ShowHideUserColumns();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            DaGrid.ItemsSource = RawEC2Results.AsDataView();
            FilterTagText.Text = "";
            ProcessingLabel.Content = "Results Displayed: " + DaGrid.Items.Count;
            ShowHideEC2Columns();
        }

        private void DaGrid_Loaded(object sender, RoutedEventArgs e)
        {

            DaGrid.ItemsSource = LoadEC2Data();
            DaGrid.ContextMenu = ECContext;
            System.Windows.Controls.MenuItem SSH = new System.Windows.Controls.MenuItem();
            SSH.Click += new RoutedEventHandler(SSH_Click);
            SSH.Header = "dOpen SSH";

            System.Windows.Controls.MenuItem SCP = new System.Windows.Controls.MenuItem();
            SCP.Click += new RoutedEventHandler(SCP_Click);
            SCP.Header = "dOpen SCP";
            

            ECContext.Items.Add(SSH);
            ECContext.Items.Add(SCP);
        }

        private void UserGrid_Loaded(object sender, RoutedEventArgs e)
        {
            UserGrid.ItemsSource = GetUsersDetailsTable().AsDataView();
            ShowHideUserColumns();
            
        }

        private System.Windows.Controls.ContextMenu ECContext = new System.Windows.Controls.ContextMenu();




/// <summary>
/// SFTPCopy
/// </summary>
/// <param name="hostname">Name of host to connect to</param>
/// <param name="username">Username</param>
/// <param name="lfile">Local file to be copied</param>
/// <param name="ec2dir">Remote Directory to copy file to</param>
/// <returns></returns>
        public string  SFTPFileCopy(string hostname, string username, string lfile, string ec2dir)
        {
            string toreturn = "";
            try
            {

                // Setup session options
                SessionOptions sessionOptions = new SessionOptions
                {
                    Protocol = WinSCP.Protocol.Sftp,
                    HostName = hostname,
                    UserName = username,
 //                   Password = password,
  //                  SshHostKeyFingerprint = "ssh-rsa 2048 xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx"
                };

                sessionOptions.GiveUpSecurityAndAcceptAnySshHostKey = true;// Since we are pulling these names from AWS, assume they are OK. Avoid prompt.

                using (Session session = new Session())
                {
                    // Connect
                    session.Open(sessionOptions);

                    // Upload files
                    TransferOptions transferOptions = new TransferOptions();
                    transferOptions.TransferMode = TransferMode.Binary;

                    TransferOperationResult transferResult;
                    transferResult = session.PutFiles(lfile, ec2dir, false, transferOptions);

                    // Throw on any error
                    transferResult.Check();

                    // Print results
                    foreach (TransferEventArgs transfer in transferResult.Transfers)
                    {
                        toreturn += "\n Upload to " + hostname + " succeeded"; 

                    }
                }

                return toreturn;
            }
            catch 
            {
                toreturn += "/nFailed copy to: " + hostname + ". Is key loaded in Pageant?";
                return toreturn;
            }
        }

        public void PayPalDonate(string youremail, string description, string country, string currency)
        {
            string PayPalURL = "";
            PayPalURL += "https://www.paypal.com/cgi-bin/webscr" +
                "?cmd=" + "_donations" +
                "&business=" + youremail +
                "&lc=" + country +
                "&item_name=" + description +
                "&currency_code=" + currency +
                "&bn=" + "PP%2dDonationsBF";
            System.Diagnostics.Process.Start(PayPalURL);
        }


        #region Event Handlers
        private void RDP_Click(object sender, EventArgs e)
        {
            string keydir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string puttyexe = @"C:\Program Files (x86)\PuTTY\putty.exe";
            var selecteditem = DaGrid.SelectedItem;// Get the datarowview
            DataRowView drv = (DataRowView)selecteditem;
            var myrow = drv.Row;
            var TargetIP = myrow["Pub_IP"];
            if (TargetIP.Equals("")) TargetIP = myrow["Priv_IP"];


            return;  //Not executing below code until I figure out how we shall manage the password going forward.


            Process rdcProcess = new Process();
            
            //Try to save credentials
            rdcProcess.StartInfo.FileName = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32\cmdkey.exe");
            //rdcProcess.StartInfo.Arguments = "/generic:TERMSRV/192.168.0.217 /user:" + "username" + " /pass:" + "password";
            rdcProcess.StartInfo.Arguments = "/generic:TERMSRV/" + TargetIP + " /user:" + "administrator" + " /pass:" + "password";
            rdcProcess.Start();
            
            //then connect
            rdcProcess.StartInfo.FileName = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32\mstsc.exe");
            rdcProcess.StartInfo.Arguments = "/v " + TargetIP; // ip or name of computer to connect
            rdcProcess.Start();




        }

        private void SSH_Click(object sender, EventArgs e)
        {
            string keydir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string puttyexe = @"C:\Program Files (x86)\PuTTY\putty.exe";
            var selecteditem = DaGrid.SelectedItem;// Get the datarowview
            DataRowView drv = (DataRowView)selecteditem;
            var myrow = drv.Row;
            var TargetIP = myrow["Pub_IP"];
            if (TargetIP.Equals("")) TargetIP = myrow["Priv_IP"];

            if (File.Exists(puttyexe)) //No point if not installed.
            {
                var PPKs = Directory.GetFiles(keydir, "*.ppk");
                //Going to try each .ppk file in MyDocuments
                foreach (var akeyfile in PPKs)
                {
                    try
                    {
                        string puttyargs = "-ssh -X -i " + akeyfile + " ec2-user@" + TargetIP + " 22";
                        var result = System.Diagnostics.Process.Start(puttyexe, puttyargs);
                        System.Threading.Thread.Sleep(2000);
                        //Look for a Putty Security Alert Window and hit the Y key.  Hacky, but it works.
                        IntPtr puttywin = FindWindow(null, "PuTTY Security Alert");
                        if (puttywin == IntPtr.Zero) ;
                        else
                        {
                            SetForegroundWindow(puttywin);
                            SendKeys.SendWait("y");

                        }

                        if (result.MainWindowTitle.Contains("ec2-user"))//Ugly, but we have to check if connected. Fails if we dont accept key in time.
                        {
                            break;
                        }
                        else
                        {
                            result.Kill();
                        }
                    }
                    catch
                    {

                    }
                }

            }
            else //Need to allow find at some point.  That means config file.  Sigh.
            {
                System.Windows.MessageBox.Show(@"C:\Program Files (x86)\PuTTY\putty.exe not found");
            }

        }

        private void SCP_Click(object sender, RoutedEventArgs e)
        {
            string keydir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var rabbit = DaGrid.SelectedItem;// Get the datarowview
            DataRowView bunny = (DataRowView)rabbit;
            var hare = bunny.Row;
            var TargetIP = hare["Pub IP"];
            if (File.Exists("winscp.exe"))//Should be included in directory.
            {

                var PPKs = Directory.GetFiles(keydir, "*.ppk");
                //Going to try each .ppk file in MyDocuments
                foreach (var akeyfile in PPKs)
                {
                    try
                    {
                        //string puttyargs = "-ssh -i " + akeyfile + " ec2-user@" + TargetIP + " 22";
                        string winscpargs = "scp://ec2-user@" + TargetIP + ":22 /privatekey=" + akeyfile;
                        var result = System.Diagnostics.Process.Start("winscp.exe", winscpargs);
                        System.Threading.Thread.Sleep(3000);


                        IntPtr winscperrorwin = FindWindow(null, "Warning");
                        if (winscperrorwin == IntPtr.Zero) ;
                        else
                        {
                            SetForegroundWindow(winscperrorwin);
                            SendKeys.SendWait("Y");
                            result.Kill();
                        }


                        //Look for a Winscp error  Window and hit the Enter key.  Hacky, but it works.
                        winscperrorwin = FindWindow(null, "Error - WinSCP");
                        if (winscperrorwin == IntPtr.Zero) ;
                        else
                        {
                            SetForegroundWindow(winscperrorwin);
                            SendKeys.SendWait("{ENTER}");
                            result.Kill();
                        }


                        if (result.MainWindowTitle.Contains("ec2-user"))//Ugly, but we have to check if connected. Fails if we dont accept key in time.
                        {
                            break;
                        }
                        else
                        {
                            //result.Kill();
                        }
                    }
                    catch
                    {
                        //Nothing to do
                    }
                }

            }
            else
            {
                System.Windows.MessageBox.Show(@"WinSCP not found. Should be in same directory as this program.");
            }


        }

        private void FilepickerButton_Click(object sender, RoutedEventArgs e)
        {
            LocalFileTextbox.Text = Filepicker();
        }

        private void FileCopyButton_Click(object sender, RoutedEventArgs e)
        {
            var finalresult = "";
            foreach (DataRowView belch in DaGrid.ItemsSource)
            {
                var rabbit = belch.Row.Field<string>("Pub DNS");
                try
                {
                    var result = SFTPFileCopy(rabbit, "ec2-user", LocalFileTextbox.Text, EC2dirtoCopytoTextbox.Text);
                    finalresult += "\n" + result;
                }
                catch
                {
                    finalresult += "\n Failed copy to " + rabbit;
                }

            }
            System.Windows.Forms.MessageBox.Show(finalresult);
        }


        private void CKAllPMI_Click(object sender, RoutedEventArgs e)
        {
            //Checks all Profilemenu items
            foreach (System.Windows.Controls.MenuItem anitem in ProfilesMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
            if (DaGrid.Items.Count > 0) DoEC2Filter();
        }

        private void UCKAllPMI_Click(object sender, RoutedEventArgs e)
        {
            //Checks all Profilemenu items
            foreach (System.Windows.Controls.MenuItem anitem in ProfilesMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
            if (DaGrid.Items.Count > 0) DoEC2Filter();
        }

        private void CkAllRMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in RegionMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
            if (DaGrid.Items.Count > 0) DoEC2Filter();
        }

        private void UCkAllRMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in RegionMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
            if (DaGrid.Items.Count > 0) DoEC2Filter();
        }

        private void CkAllCMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in Ec2ColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
            if (DaGrid.Items.Count > 0) DoEC2Filter();
        }

        private void UCkAllCMI_Checked(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in Ec2ColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
            if (DaGrid.Items.Count > 0) DoEC2Filter();
        }


        //------------------------------------------------------------------------------------------
        private void CkAllUserCMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in UserColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
            if (UserGrid.Items.Count > 0) DoUserFilter();
        }

        private void UCkAllUserCMI_Checked(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in UserColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
            if (UserGrid.Items.Count > 0) DoUserFilter();
        }

        private void DaGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cecil = DaGrid.SelectedItems.Count;
            ECContext.Items.Clear();
            if (cecil > 1)
            {
                System.Windows.Controls.MenuItem MS = new System.Windows.Controls.MenuItem();
                MS.Header = "Multiselected Options";
                ECContext.Items.Add(MS);
            }
            else if(DaGrid.SelectedItems.Count.Equals(0) | DaGrid.SelectedItems.Count.Equals(1) )// Select the one Context row.
            {
                //===============================================================================================
                DependencyObject dep = (DependencyObject)e.OriginalSource;
                while ((dep != null) && !(dep is System.Windows.Controls.DataGridCell))
                {
                    dep = VisualTreeHelper.GetParent(dep);
                }
                if (dep == null) return;

                if (dep is System.Windows.Controls.DataGridCell)
                {
                    System.Windows.Controls.DataGridCell cell = dep as System.Windows.Controls.DataGridCell;
                    cell.Focus();

                    while ((dep != null) && !(dep is System.Windows.Controls.DataGridRow))
                    {
                        dep = VisualTreeHelper.GetParent(dep);
                    }
                    System.Windows.Controls.DataGridRow row = dep as System.Windows.Controls.DataGridRow;
                    DaGrid.SelectedItem = row.DataContext;


                    var rabbit = DaGrid.SelectedItem;// Get the datarowview
                    DataRowView bunny = (DataRowView)rabbit;
                    var hare = bunny.Row;
                    var mypubip = hare["Pub_IP"];
                    if (mypubip.Equals(""))  mypubip=hare["Priv_IP"];
                    var hassenpfeffer = hare["Platform"];
                    //Build context Menus

                    if (hassenpfeffer.Equals("Linux"))
                    {
                        System.Windows.Controls.MenuItem SSH = new System.Windows.Controls.MenuItem();
                        SSH.Click += new RoutedEventHandler(SSH_Click);
                        SSH.Header = "Open SSH to " + mypubip;
                        SSH.Tag = mypubip;

                        System.Windows.Controls.MenuItem SCP = new System.Windows.Controls.MenuItem();
                        SCP.Click += new RoutedEventHandler(SCP_Click);
                        SCP.Header = "Open SCP to " + mypubip;
                        SCP.Tag = mypubip;

                        ECContext.Items.Add(SSH);
                        ECContext.Items.Add(SCP);
                    }
                    else if(hassenpfeffer.Equals("Windows"))
                    {
                        System.Windows.Controls.MenuItem RDP = new System.Windows.Controls.MenuItem();
                        RDP.Click += new RoutedEventHandler(RDP_Click);
                        RDP.Header = "Open RDP to " + mypubip;
                        RDP.Tag = mypubip;
                        ECContext.Items.Add(RDP);
                    }
                }


                     }//==================================================================================================

            else
            {
                System.Windows.Controls.MenuItem NS = new System.Windows.Controls.MenuItem();
                NS.Header = "No selected arow";
                ECContext.Items.Add(NS);
            }
        }

        private void EC2Event_Monitor_Closing(object sender, CancelEventArgs e)
        {
            System.Windows.Forms.Application.Exit();
        }

        private void ColumnsClick(object sender, System.EventArgs e)
        {
            ShowHideEC2Columns();
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            var outputfile = saveas();
            Dictionary<string, DataTable> mydata = new Dictionary<string, DataTable>();
            mydata.Add("S3", RawS3);
            mydata.Add("Users", RawUsers);
            mydata.Add("EC2 Instances", RawEC2Results);
            ExportToExcel(mydata, outputfile);
        }

        private void FilterTagText_TextChanged(object sender, TextChangedEventArgs e)
        {
            DoEC2Filter();
        }
        private void LoadCred_Click(object sender, RoutedEventArgs e)
        {
            //Loading a credential file.
            string results = "";
            //Select file
            string credfile = Filepicker("All Files|*.*");
            //Import creds
            if (credfile.Equals("")) return;
            var txt = File.ReadAllText(credfile);
            Dictionary<string, Dictionary<string, string>> ini = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);

            Dictionary<string, string> currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            ini[""] = currentSection;

            foreach (var line in txt.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                       .Where(t => !string.IsNullOrWhiteSpace(t))
                       .Select(t => t.Trim()))
            {
                if (line.StartsWith(";"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    ini[line.Substring(1, line.LastIndexOf("]") - 1)] = currentSection;
                    continue;
                }

                var idx = line.IndexOf("=");
                if (idx == -1)
                    currentSection[line] = "";
                else
                    currentSection[line.Substring(0, idx)] = line.Substring(idx + 1);
            }


            //Amazon.Util.ProfileManager.RegisterProfile(newprofileName, newaccessKey, newsecretKey);

            //Build a list of current keys to use to avoid dupes due to changed "profile" names.
            Dictionary<string, string> currentaccesskeys = new Dictionary<string, string>();

            foreach (var aprofilename in Amazon.Util.ProfileManager.ListProfileNames())
            {
                var acred = Amazon.Util.ProfileManager.GetAWSCredentials(aprofilename).GetCredentials();

                currentaccesskeys.Add(aprofilename, acred.AccessKey);
            }

            foreach (KeyValuePair<string, Dictionary<string, string>> kvp in ini)
            {
                string newprofileName = "";
                string newaccessKey = "";
                string newsecretKey = "";
                if (kvp.Key == "") continue;

                newprofileName = kvp.Key.ToString();
                newaccessKey = kvp.Value["aws_access_key_id"].ToString();
                newsecretKey = kvp.Value["aws_secret_access_key"].ToString();


                if (Amazon.Util.ProfileManager.ListProfileNames().Contains(newprofileName))
                {
                    var daP = Amazon.Util.ProfileManager.GetAWSCredentials(newprofileName).GetCredentials();
                    if (daP.AccessKey == newaccessKey & daP.SecretKey == newsecretKey)
                    {
                        //dey da same
                    }
                    else
                    {
                        results += newprofileName + " keys do not match existing profile!\n";
                    }

                }
                else //Profile does not exist by this name.  
                {
                    if (currentaccesskeys.Values.Contains(newaccessKey))//Do we already have that key?
                    {
                        //We are trying to enter a duplicate profile name for the same key. 
                        string existingprofile = "";
                        foreach (KeyValuePair<string, string> minikvp in currentaccesskeys)
                        {
                            if (minikvp.Value == newaccessKey)
                            {
                                existingprofile = minikvp.Key.ToString();
                            }
                        }

                        results += newprofileName + " already exists as " + existingprofile + "\n";
                    }
                    else
                    {
                        if (newaccessKey.Length.Equals(20) & newsecretKey.Length.Equals(40))
                        {
                            results += newprofileName + " added to credential store!\n";
                            //Amazon.Util.ProfileManager.RegisterProfile(newprofileName, newaccessKey, newsecretKey);
                        }
                        else
                        {
                            results += newprofileName + "'s keys are not the correct length!\n";
                        }
                    }
                }

            }
            if (results.Equals(""))
            {
                string message = ini.Count.ToString() + " profiles in " + credfile + " already in credential store.";
                System.Windows.MessageBox.Show(message, "Results");
            }
            else
            {
                System.Windows.MessageBox.Show(results, "Results");
            }

        }

        private void ProfileChecked(object sender, RoutedEventArgs e)
        {
            DoEC2Filter();
        }

        private void UserChecked(object sender, RoutedEventArgs e)
        {
            ShowHideUserColumns(); 
        }

        #endregion Eventhandlers


        private void ShowHideEC2Columns()
        {
            foreach (var anitem in DaGrid.Columns)
            {
                string myheader = (string)anitem.Header;
                //Check status in Column Menu
                bool getcheckedstatus = (from System.Windows.Controls.MenuItem t in Ec2ColumnsMI.Items
                                        where t.Header.Equals(myheader)
                                        select t.IsChecked).FirstOrDefault();

                if (getcheckedstatus)
                {
                    anitem.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    anitem.Visibility = System.Windows.Visibility.Hidden;
                }
            }     
        }

        private void ShowHideUserColumns()
        {
            foreach (var anitem in UserGrid.Columns)
            {
                string myheader = (string)anitem.Header;
                //Check status in Column Menu
                bool getcheckedstatus = (from System.Windows.Controls.MenuItem t in UserColumnsMI.Items
                                         where t.Header.Equals(myheader)
                                         select t.IsChecked).FirstOrDefault();

                if (getcheckedstatus) anitem.Visibility = System.Windows.Visibility.Visible;
                else anitem.Visibility = System.Windows.Visibility.Hidden;
            }


        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {


        }

        private void ClearFilters_Copy_Click(object sender, RoutedEventArgs e)
        {

            UserGrid.ItemsSource = RawUsers.AsDataView();
            DoUserFilter();

        }




        //endlc
        

        private void ExportToExcel(Dictionary<string,DataTable> DataTables, string ExcelFilePath)
        {

            try
            {
                Microsoft.Office.Interop.Excel.Application Excel = new Microsoft.Office.Interop.Excel.Application();
                // load excel, and create a new workbook
                var wb = Excel.Workbooks.Add();


                foreach (string DT2WS in DataTables.Keys)
                {
                    wb.Sheets.Add();
                    int ColumnsCount;
                    var aDT = DataTables[DT2WS];

                    if (aDT == null || (ColumnsCount = aDT.Columns.Count) == 0)
                        throw new Exception("ExportToExcel: Null or empty input table!\n");

                    // single worksheet
                    Microsoft.Office.Interop.Excel._Worksheet Worksheet = Excel.ActiveSheet;
                    Worksheet.Name = DT2WS;

                    object[] Header = new object[ColumnsCount];

                    // column headings               
                    for (int i = 0; i < ColumnsCount; i++)
                    { 
                        Header[i] = aDT.Columns[i].ColumnName;
                     }

                    Microsoft.Office.Interop.Excel.Range HeaderRange = Worksheet.get_Range((Microsoft.Office.Interop.Excel.Range)(Worksheet.Cells[1, 1]), (Microsoft.Office.Interop.Excel.Range)(Worksheet.Cells[1, ColumnsCount]));
                    HeaderRange.Value = Header;
                    //HeaderRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                    HeaderRange.Font.Bold = true;

                    // DataCells
                    int RowsCount = aDT.Rows.Count;
                    object[,] Cells = new object[RowsCount, ColumnsCount];

                    for (int j = 0; j < RowsCount; j++)
                        for (int i = 0; i < ColumnsCount; i++)
                            Cells[j, i] = aDT.Rows[j][i];

                    Worksheet.get_Range((Microsoft.Office.Interop.Excel.Range)(Worksheet.Cells[2, 1]), (Microsoft.Office.Interop.Excel.Range)(Worksheet.Cells[RowsCount + 1, ColumnsCount])).Value = Cells;
                    Worksheet.get_Range((Microsoft.Office.Interop.Excel.Range)(Worksheet.Cells[2, 1]), (Microsoft.Office.Interop.Excel.Range)(Worksheet.Cells[RowsCount + 1, ColumnsCount])).NumberFormat = "@";

                }



                //Ok, now to output the file...



                // check filepath
                if (ExcelFilePath != null && ExcelFilePath != "")
                {
                    try
                    {
                        wb.SaveAs(ExcelFilePath);
                        //Worksheet.SaveAs(ExcelFilePath);
                        Excel.Quit();
                        System.Windows.MessageBox.Show("Excel file saved!");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("ExportToExcel: Excel file could not be saved! Check filepath.\n"
                            + ex.Message);
                    }
                }
                else    // no filepath is given
                {
                    Excel.Visible = true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ExportToExcel: \n" + ex.Message);
            }
        }

        private void FileMenu_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public class ScanRequest
    {
        public string Profile { get; set; }
        public List<Amazon.RegionEndpoint> Regions { get; set; }
        public Queue< Dictionary<string,DataTable>> ResultQueue { get; set; }
    }
}
