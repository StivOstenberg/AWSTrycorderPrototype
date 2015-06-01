using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
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
        public DataTable RawResults = GetEC2StatusTable();

        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp, Object value);
        UpdateProgressBarDelegate doupdatePbDelegate;
        double regioncounter = 0;

        //Code required to manipulate Windows.
        [System.Runtime.InteropServices.DllImport("USER32.DLL", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [System.Runtime.InteropServices.DllImport("USER32.DLL", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern bool SetForegroundWindow(IntPtr hWnd);



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
            DataTable MyDataTable = GetEC2StatusTable();
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
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[1];
                Proot.Items.Add(mi);
            }

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
                mi.StaysOpenOnClick = true;
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[2];
                Proot.Items.Add(mi);
            }
            foreach(var acolumn in GetEC2StatusTable().Columns) //Set the Column Show Hide menu up
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
            ColumnCombo.SelectedItem = "Tags";
        }



        static DataTable GetEC2StatusTable()
        {
            // Here we create a DataTable .
            DataTable table = new DataTable();
            table.Columns.Add("Profile", typeof(string));
            table.Columns.Add("Region", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("InstanceID", typeof(string));
            table.Columns.Add("AvailabilityZone", typeof(string));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("Events", typeof(string));
            table.Columns.Add("EventList", typeof(string));
            table.Columns.Add("Tags", typeof(string));
            table.Columns.Add("Priv IP", typeof(string));
            table.Columns.Add("Pub IP", typeof(string));
            table.Columns.Add("Pub DNS", typeof(string));
            table.Columns.Add("State", typeof(string));
            table.Columns.Add("vType", typeof(string));
            table.Columns.Add("iType", typeof(string));
            table.Columns.Add("SecurityGroups", typeof(string));
            return table;
        }



        public class EC2Instance
        {
            public string Profile { get; set; }
            public string Region { get; set; }
            public string Name { get; set; }
            public string InstanceID { get; set; }
            public string AvailabilityZone { get; set; }
            public string Status { get; set; }
            public string Events { get; set; }
            public string EventList { get; set; }
            public string Tags { get; set; }

            public string PrivvyIP { get; set; }

            public string PubIP { get; set; }
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
            DoFilter();
        }

        private void Process()
        {
            ProgressBar1.Visibility = System.Windows.Visibility.Visible;
            DataTable MyDataTable = GetEC2StatusTable();
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
            Queue<DataTable> ProfileResults = new Queue<DataTable>();
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
            while(ProfileResults.Count>0)
            {
                var atable = ProfileResults.Dequeue();
                MyDataTable.Merge(atable);
            }

            

            RawResults = MyDataTable;
            DaGrid.ItemsSource = MyDataTable.AsDataView();
            ProgressBar1.Visibility = System.Windows.Visibility.Hidden;
            ProcessingLabel.Content  = "Results Displayed: " + RawResults.Rows.Count;

            
            
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
            DoFilter();
        }
        private void DoFilter()
        {
            var newtable = RawResults.Copy();
            string fxp = ""; // The string what will build our query.
            if (FilterTagText.Text.Equals("")) return;
            string columntofilter = ColumnCombo.SelectedItem.ToString();
            if (fxp.Length > 2) fxp += " and ";
            else
            {
                var newbie = from record in RawResults.AsEnumerable()
                             where record.Field<string>(columntofilter).Contains(FilterTagText.Text)
                             select record;
                var newdt = GetEC2StatusTable();
                int count = newbie.Count();
                foreach (var element in newbie)
                {
                    var row = newdt.NewRow();
                    row = element;
                    newdt.ImportRow(row);

                }
                DaGrid.ItemsSource = newdt.AsDataView();
                ProcessingLabel.Content = "Filtered Results Displayed: " + newdt.Rows.Count + " of " + RawResults.Rows.Count;
            }
            ShowHideColumns();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            DaGrid.ItemsSource = RawResults.AsDataView();
            ProcessingLabel.Content = "Results Displayed: " + DaGrid.Items.Count;
            ShowHideColumns();
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

        private System.Windows.Controls.ContextMenu ECContext = new System.Windows.Controls.ContextMenu();

        private void SSH_Click(object sender, EventArgs e)
        {
            string keydir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string puttyexe = @"C:\Program Files (x86)\PuTTY\putty.exe";
            var rabbit = DaGrid.SelectedItem;// Get the datarowview
            DataRowView bunny = (DataRowView)rabbit;
            var hare = bunny.Row;
            var TargetIP = hare["Pub IP"];
            
            if(File.Exists(puttyexe)) //No point if not installed.
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

                       if(result.MainWindowTitle.Contains("ec2-user"))//Ugly, but we have to check if connected. Fails if we dont accept key in time.
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
            if(File.Exists("winscp.exe"))//Should be included in directory.
            {

                   var PPKs = Directory.GetFiles(keydir, "*.ppk");
                //Going to try each .ppk file in MyDocuments
               foreach (var akeyfile in PPKs)
               {
                   try
                   {
                       //string puttyargs = "-ssh -i " + akeyfile + " ec2-user@" + TargetIP + " 22";
                       string winscpargs = "scp://ec2-user@" + TargetIP + ":22 /privatekey=" + akeyfile ;
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


                       if(result.MainWindowTitle.Contains("ec2-user"))//Ugly, but we have to check if connected. Fails if we dont accept key in time.
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
                      int error = 0;
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
            foreach(DataRowView belch in DaGrid.ItemsSource)
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
                    Protocol = Protocol.Sftp,
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
            catch (Exception e)
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
        private void CKAllPMI_Click(object sender, RoutedEventArgs e)
        {
            //Checks all Profilemenu items
            foreach (System.Windows.Controls.MenuItem anitem in ProfilesMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
        }

        private void UCKAllPMI_Click(object sender, RoutedEventArgs e)
        {
            //Checks all Profilemenu items
            foreach (System.Windows.Controls.MenuItem anitem in ProfilesMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
        }

        private void CkAllRMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in RegionMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
        }

        private void UCkAllRMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in RegionMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
        }

        private void CkAllCMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in ColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
        }

        private void UCkAllCMI_Checked(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in ColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
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
                    var coney = hare["Pub IP"];

                    //Build context Menu
                    System.Windows.Controls.MenuItem SSH = new System.Windows.Controls.MenuItem();
                    SSH.Click += new RoutedEventHandler(SSH_Click);
                    SSH.Header = "Open SSH to " + coney;
                    SSH.Tag = coney;

                    System.Windows.Controls.MenuItem SCP = new System.Windows.Controls.MenuItem();
                    SCP.Click += new RoutedEventHandler(SCP_Click);
                    SCP.Header = "Open SCP to " + coney;
                    SCP.Tag = coney;

                    ECContext.Items.Add(SSH);
                    ECContext.Items.Add(SCP);

                }


                     }//==================================================================================================

            else
            {
                System.Windows.Controls.MenuItem NS = new System.Windows.Controls.MenuItem();
                NS.Header = "No selected rows";
                ECContext.Items.Add(NS);
            }
        }

        private void EC2Event_Monitor_Closing(object sender, CancelEventArgs e)
        {
            System.Windows.Forms.Application.Exit();
        }

        private void ColumnsClick(object sender, System.EventArgs e)
        {
            ShowHideColumns();
        }

        #endregion Eventhandlers
        public DataTable ScanProfile(ScanRequest Request)
        {
            try
            {
                var aprofile = Request.Profile;
                var regions2process = Request.Regions;
                var SubmitResults = Request.ResultQueue;

                Amazon.Runtime.AWSCredentials credential = new Amazon.Runtime.StoredProfileAWSCredentials(aprofile);
                var MyDataTable = GetEC2StatusTable();
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
                    }
                    DescribeInstancesResult DescResult = ec2.DescribeInstances(indatarequest);


                    int count = instatresponse.InstanceStatuses.Count();

                    foreach (var instat in instatresponse.InstanceStatuses)
                    {
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
                        string myregion = region.DisplayName + "  -  " + region.SystemName;
                        int eventnumber = instat.Events.Count();

                        string eventlist = "";
                        var urtburgle = DescResult.Reservations;

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

                        foreach (var atag in taglist)
                        {
                            if (atag.Key.Equals("Name"))
                            {
                                instancename = atag.Value;
                            }
                            if (!TagFilterCombo.Items.Contains(atag.Key))
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

                        //Need more info for SSH and SCP...

                        var privvyIP = (from t in urtburgle
                                        where t.Instances[0].InstanceId.Equals(instanceid)
                                        select t.Instances[0].PrivateIpAddress).FirstOrDefault();
                        if (String.IsNullOrEmpty(privvyIP)) privvyIP = "?";
                        
                        var publicIP = (from t in urtburgle
                                        where t.Instances[0].InstanceId.Equals(instanceid)
                                        select t.Instances[0].PublicIpAddress).FirstOrDefault();
                        if (String.IsNullOrEmpty(publicIP)) publicIP = "";

                        var publicDNS = (from t in urtburgle
                                         where t.Instances[0].InstanceId.Equals(instanceid)
                                         select t.Instances[0].PublicDnsName).FirstOrDefault();
                        if (String.IsNullOrEmpty(publicDNS)) publicDNS = "";

                        //Virtualization type (HVM, Paravirtual)
                        var ivirtType = (from t in urtburgle
                                         where t.Instances[0].InstanceId.Equals(instanceid)
                                         select t.Instances[0].VirtualizationType).FirstOrDefault();
                        if (String.IsNullOrEmpty(ivirtType)) ivirtType = "?";

                        // InstanceType (m3/Large etc)
                        var instancetype = (from t in urtburgle
                                       where t.Instances[0].InstanceId.Equals(instanceid)
                                       select t.Instances[0].InstanceType).FirstOrDefault();
                        if (String.IsNullOrEmpty(instancetype)) instancetype = "?";

                        var SGs = (from t in urtburgle
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


                        string rabbit = profile+ myregion+ instancename+ instanceid+ AZ+ status+ eventnumber+ eventlist+ tags+ privvyIP+ publicIP+ publicDNS+ istate+ ivirtType+instancetype+ sglist;

                        MyDataTable.Rows.Add(profile, myregion, instancename, instanceid, AZ, status, eventnumber, eventlist, tags,privvyIP ,publicIP, publicDNS, istate, ivirtType, instancetype,sglist);


                    }

                }


                
                return MyDataTable;
            }
            catch(Exception ex)
            {
                //Will figure out what to do with this later.
                var anerror = ex;
                throw ex; 
            }

        }





        private void ShowHideColumns()
        {
            foreach (var anitem in DaGrid.Columns)
            {
                string myheader = (string)anitem.Header;
                //Check status in Column Menu
                bool getcheckedstatus = (from System.Windows.Controls.MenuItem t in ColumnsMI.Items
                                        where t.Header.Equals(myheader)
                                        select t.IsChecked).FirstOrDefault();

                if (getcheckedstatus) anitem.Visibility = System.Windows.Visibility.Visible;
                else anitem.Visibility = System.Windows.Visibility.Hidden;
            }
            
        }

        private void FilterTagText_TextChanged(object sender, TextChangedEventArgs e)
        {
            DoFilter();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Export not yet implimented.");
        }

        private void LoadCred_Click(object sender, RoutedEventArgs e)
        {
            //Loading a credential file.

            //Select file
            string credfile = Filepicker("All Files|*.*");
            //Import creds

            //Add to VS Creds?
        }


    }

    public class ScanRequest
    {
        public string Profile { get; set; }
        public List<Amazon.RegionEndpoint> Regions { get; set; }
        public Queue<DataTable> ResultQueue { get; set; }
    }
}
