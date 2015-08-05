using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.IdentityManagement;
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
        public DataTable Users = GetUsersStatusTable();

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
                mi.Click += ProfileChecked;
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
                mi.Click += ProfileChecked;
                mi.StaysOpenOnClick = true;
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[2];
                Proot.Items.Add(mi);
            }
            ColumnCombo.Items.Add("_ANY_");
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
            ColumnCombo.SelectedItem = "_ANY_";
        }



        static DataTable GetEC2StatusTable()
        {
            // Here we create a DataTable .
            DataTable table = new DataTable();
            table.Columns.Add("AccountID", typeof(string));
            table.Columns.Add("Profile", typeof(string));
            table.Columns.Add("Region", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("InstanceID", typeof(string));
            table.Columns.Add("AvailabilityZone", typeof(string));
            table.Columns.Add("Platform", typeof(string));
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

        static DataTable GetUsersStatusTable()
        {
            // Here we create a DataTable .
            DataTable table = new DataTable();
            table.Columns.Add("Account", typeof(string));
            table.Columns.Add("AccountID", typeof(string));
            table.Columns.Add("Username", typeof(string));
            table.Columns.Add("UserID", typeof(string));
            table.Columns.Add("ARN", typeof(string));
            table.Columns.Add("CreateDate", typeof(string));
            table.Columns.Add("PasswordLastUsed", typeof(string));
            return table;
        }



        public class EC2Instance
        {
            public string Account { get; set; }
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
            Users = GetUsersStatusTable();
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
            UserGrid.ItemsSource = Users.AsDataView();
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
            if (RawResults.Rows.Count < 1) return;
            var newtable = RawResults.Copy();
            var newbie = RawResults.AsEnumerable();

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
                    if (RawResults.Rows.Count < 1)
                    {
                       newbie=RawResults.AsEnumerable() ;

                    }
                    try
                    {
                        newbie = RawResults.AsEnumerable().Where(p => p.Field<string>("Profile").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Region").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Name").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("InstanceID").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("AvailabilityZone").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Platform").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Status").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Events").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("EventList").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Tags").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Priv IP").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Pub IP").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("Pub DNS").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("State").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("vType").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("iType").Contains(FilterTagText.Text) ||
                                                                          p.Field<string>("SecurityGroups").Contains(FilterTagText.Text));
                    }
                    catch(Exception ex)
                    {
                        newbie = RawResults.AsEnumerable();
                    }
                }
                else
                {
                    newbie = from record in RawResults.AsEnumerable()
                                 where record.Field<string>(columntofilter).Contains(FilterTagText.Text)
                                 select record;
                }
                


                var newdt = GetEC2StatusTable();
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

                    if(!isregionchecked)
                    {
                        string rabbit = "filtered out!";
                    }

                    if(isprofilechecked & isregionchecked)
                    { 
                    newdt.ImportRow(row);
                    }

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
        private void RDP_Click(object sender, EventArgs e)
        {
            string keydir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string puttyexe = @"C:\Program Files (x86)\PuTTY\putty.exe";
            var selecteditem = DaGrid.SelectedItem;// Get the datarowview
            DataRowView drv = (DataRowView)selecteditem;
            var myrow = drv.Row;
            var TargetIP = myrow["Pub IP"];
            if (TargetIP.Equals("")) TargetIP = myrow["Priv IP"];


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
            var TargetIP = myrow["Pub IP"];
            if (TargetIP.Equals("")) TargetIP = myrow["Priv IP"];

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
            if (DaGrid.Items.Count > 0) DoFilter();
        }

        private void UCKAllPMI_Click(object sender, RoutedEventArgs e)
        {
            //Checks all Profilemenu items
            foreach (System.Windows.Controls.MenuItem anitem in ProfilesMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
            if (DaGrid.Items.Count > 0) DoFilter();
        }

        private void CkAllRMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in RegionMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
            if (DaGrid.Items.Count > 0) DoFilter();
        }

        private void UCkAllRMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in RegionMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
            if (DaGrid.Items.Count > 0) DoFilter();
        }

        private void CkAllCMI_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in ColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = true;
            }
            if (DaGrid.Items.Count > 0) DoFilter();
        }

        private void UCkAllCMI_Checked(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem anitem in ColumnsMI.Items)
            {
                if (anitem.IsCheckable) anitem.IsChecked = false;
            }
            if (DaGrid.Items.Count > 0) DoFilter();
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
                    var mypubip = hare["Pub IP"];
                    if (mypubip.Equals(""))  mypubip=hare["Priv IP"];
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

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Export not yet implimented.");
        }

        private void FilterTagText_TextChanged(object sender, TextChangedEventArgs e)
        {
            DoFilter();
        }
        private void LoadCred_Click(object sender, RoutedEventArgs e)
        {
            //Loading a credential file.
            string results = "";
            //Select file
            string credfile = Filepicker("All Files|*.*");
            //Import creds
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
            DoFilter();
        }

        #endregion Eventhandlers



        public DataTable ScanProfile(ScanRequest Request)
        {
            Amazon.Runtime.AWSCredentials credential;
            var aprofile = Request.Profile;
            var regions2process = Request.Regions;
            var SubmitResults = Request.ResultQueue;
            try
            {
                credential = new Amazon.Runtime.StoredProfileAWSCredentials(aprofile);
                //Try to get the Account ID//

                var iam = new AmazonIdentityManagementServiceClient(credential);
                var gribble = iam.ListUsers().Users;
                var accountid = gribble[0].Arn.Split(':')[4];//Get the ARN and extract the Account ID


                foreach (var auser in gribble)
                {
                    
                    string ausername = auser.UserName;
                    string auserid = auser.UserId;
                    string arn = auser.Arn;
                    string createddate = auser.CreateDate.ToShortDateString() + " " + auser.CreateDate.ToShortTimeString();
                    string plu = auser.PasswordLastUsed.ToShortDateString() + " " + auser.PasswordLastUsed.ToShortTimeString();



                    Users.Rows.Add(aprofile,accountid, ausername, auserid, arn, createddate, plu);
                }







                //////////////////////////////////////////////////////////
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
                        string myregion = region.ToString();
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

                        var platform = (from t in urtburgle
                                        where t.Instances[0].InstanceId.Equals(instanceid)
                                        select t.Instances[0].Platform).FirstOrDefault();
                        if (String.IsNullOrEmpty(platform)) platform = "Linux";

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
                        if (String.IsNullOrEmpty(sglist)) sglist = "NullOrEmpty";

                        if (String.IsNullOrEmpty(instancename)) instancename = "";
                        string rabbit = accountid+profile+ myregion+ instancename+ instanceid+ AZ+ status+ eventnumber+ eventlist+ tags+ privvyIP+ publicIP+ publicDNS+ istate+ ivirtType+instancetype+ sglist;

                        MyDataTable.Rows.Add(accountid, profile, myregion, instancename, instanceid, AZ, platform, status, eventnumber, eventlist, tags,privvyIP ,publicIP, publicDNS, istate, ivirtType, instancetype,sglist);


                    }

                }


                
                return MyDataTable;
            }
            catch(Exception ex)
            {
                //If we failed to connect with creds.

                string error = new string(ex.ToString().TakeWhile(c => c != '\n').ToArray());
                System.Windows.MessageBox.Show(error, Request.Profile.ToString() + " credentials failed to work.\n");
                //Try to flag the menu item so it no longer selectable, and maybe make she red.
                System.Windows.Controls.MenuItem Proot = (System.Windows.Controls.MenuItem)this.MainMenu.Items[1];
                foreach(System.Windows.Controls.MenuItem amenuitem in Proot.Items)
                {
                    if(amenuitem.Header==aprofile)
                    {
                        amenuitem.IsCheckable = false;
                        amenuitem.IsChecked = false;
                        amenuitem.Background = Brushes.Red;
                        amenuitem.ToolTip = Request.Profile.ToString() + " credentials failed to work.\n";
                    }
                }




                var MyDataTable = GetEC2StatusTable();
                return MyDataTable;

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

        private void EC2Tab_GotFocus(object sender, RoutedEventArgs e)
        {
            DoFilter();
        }

        private void EC2Tab_Loaded(object sender, RoutedEventArgs e)
        {
            DoFilter();
        }

        //endlc
    }

    public class ScanRequest
    {
        public string Profile { get; set; }
        public List<Amazon.RegionEndpoint> Regions { get; set; }
        public Queue<DataTable> ResultQueue { get; set; }
    }
}
