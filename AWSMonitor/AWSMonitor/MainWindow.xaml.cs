using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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


        public string Filepicker()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "Data Sources (*.py, *.sh)|*.py*;*.sh|All Files|*.*"; ;
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

            ProfilesComboBox.Items.Add("_All_");
            RegionsCombobox.Items.Add("_All_");
            foreach(string aProfile in Profiles)
            {
                ProfilesComboBox.Items.Add(aProfile);
            }

            var Regions = RegionEndpoint.EnumerableAllRegions;
            foreach(var aregion in Regions)
            {
                //Skip Beijing and USGov
                if (aregion == Amazon.RegionEndpoint.USGovCloudWest1) continue;
                if (aregion == Amazon.RegionEndpoint.CNNorth1) continue;
                RegionsCombobox.Items.Add(aregion.DisplayName);
            }
            ProfilesComboBox.SelectedIndex = 0;
            RegionsCombobox.SelectedIndex = 0;
        }

        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp, Object value);

        static DataTable GetEC2StatusTable()
        {
            // Here we create a DataTable with four columns.
            DataTable table = new DataTable();
            table.Columns.Add("Profile", typeof(string));
            table.Columns.Add("Region", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("InstanceID", typeof(string));
            table.Columns.Add("AvailabilityZone", typeof(string));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("Events", typeof(int));
            table.Columns.Add("EventList", typeof(string));
            table.Columns.Add("Tags", typeof(string));
            table.Columns.Add("Pub IP", typeof(string));
            table.Columns.Add("Pub DNS", typeof(string));

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
            public int Events { get; set; }
            public string EventList { get; set; }
            public string Tags { get; set; }
            public string PubIP { get; set; }
            public string PubDNS { get; set; }

        }

        private List<EC2Instance>  LoadEC2Data()
        {
            List<EC2Instance> thedata = new List<EC2Instance>();

            return thedata;
        }

        private void EC2EventScanButton_Click(object sender, RoutedEventArgs e)
        {
            Process();
        }

        private void Process()
        {
            ProgressBar1.Visibility = System.Windows.Visibility.Visible;
            DataTable MyDataTable = GetEC2StatusTable();
            TagFilterCombo.Items.Clear();
            //Set Default For Testing...
            var region = Amazon.RegionEndpoint.USWest2;
            



            //Stores the value of the ProgressBar
            double value = 0;

            //Create a new instance of our ProgressBar Delegate that points
            // to the ProgressBar's SetValue method.
            UpdateProgressBarDelegate updatePbDelegate =  new UpdateProgressBarDelegate(ProgressBar1.SetValue);

            var prof2process = Amazon.Util.ProfileManager.ListProfileNames().OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase).ToList();
            var regions2process = Amazon.RegionEndpoint.EnumerableAllRegions.ToList();
            //override complete list with one profile.
            if(!ProfilesComboBox.SelectedValue.Equals("_All_"))
            {
                prof2process.Clear();
                prof2process.Add(ProfilesComboBox.SelectedValue.ToString());
            }

            if(!RegionsCombobox.SelectedValue.Equals("_All_"))
            {
                regions2process.Clear();
                foreach(var iregion in Amazon.RegionEndpoint.EnumerableAllRegions)
                {
                    if(iregion.DisplayName.Equals(RegionsCombobox.SelectedValue.ToString()))
                    {
                        regions2process.Add(iregion);
                    }
                }
            }

            //Configure the ProgressBar
            ProgressBar1.Minimum = 0;
            //Subtract 2 from the count for Beijing and GovWest
            ProgressBar1.Maximum = prof2process.Count * regions2process.Count;
            ProgressBar1.Value = 0;

            // Start the loops.  For each Profile, iterate through all regions.
            //Foreach Profile(credential) set aprofile

            
            foreach (var aprofile in prof2process)
            {
                Amazon.Runtime.AWSCredentials credential = new Amazon.Runtime.StoredProfileAWSCredentials(aprofile);

                //Foreach aregion
                foreach (var aregion in regions2process)
                {
                    //Skip GovCloud and Beijing. They require special handling and I dont need em.
                    if (aregion == Amazon.RegionEndpoint.USGovCloudWest1) continue;
                    if (aregion == Amazon.RegionEndpoint.CNNorth1) continue;
                    region = aregion;
                    ProcessingLabel.Content = "Pro:" + aprofile + "    Reg: " + region;
                    Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, value });
                    //Try to get scheduled events on my Profile/aregion
                    var ec2 = AWSClientFactory.CreateAmazonEC2Client(credential, region);
                    var request = new DescribeInstanceStatusRequest();


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
                        ProcessingLabel.Content = "Pro:" + aprofile + "    Reg: " + region + "   Ins: " + instanceid;
                        //How do we get the tag keys for an instance??? Argh!
                        var status = instat.Status.Status;
                        string AZ = instat.AvailabilityZone;
                        string profile = aprofile;
                        string myregion = region.DisplayName + "  -  " + region.SystemName;
                        int eventnumber = instat.Events.Count();
                        string eventlist = "";
                        var urtburgle = DescResult.Reservations;

                        string tags = ""; // Holds the list of tags to print out.

                        var loadtags = (from t in DescResult.Reservations
                                       where t.Instances[0].InstanceId.Equals(instanceid)
                                       select t.Instances[0].Tags).AsEnumerable();

                        Dictionary<string,string> taglist = new Dictionary<string,string>();
                        foreach(var rekey in loadtags)
                        {
                           foreach(var kvp in rekey)
                           {
                               taglist.Add(kvp.Key, kvp.Value);
                           }
                        }

                        foreach(var atag in taglist)
                        {
                            if (atag.Key.Equals("Name"))
                            {
                                instancename = atag.Value;
                                continue;
                            }
                            if(!TagFilterCombo.Items.Contains(atag.Key))
                            {
                                TagFilterCombo.Items.Add(atag.Key);
                            }
                            if (tags.Length > 1)
                            {
                                tags += "\n" + atag.Key + ":" + atag.Value ;
                            }
                            else
                            {
                                tags += atag.Key + ":" + atag.Value ;
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


                        var publicIP = (from t in urtburgle
                                      where t.Instances[0].InstanceId.Equals(instanceid)
                                      select t.Instances[0].PublicIpAddress).FirstOrDefault();

                        var publicDNS = (from t in urtburgle
                                     where t.Instances[0].InstanceId.Equals(instanceid)
                                     select t.Instances[0].PublicDnsName).FirstOrDefault();

                        //Add to table


                        MyDataTable.Rows.Add(profile, myregion, instancename, instanceid, AZ, status, eventnumber, eventlist, tags, publicIP, publicDNS);

                    }
                    value++;
                }
                
   

            }
            RawResults = MyDataTable;
            DaGrid.ItemsSource = MyDataTable.AsDataView();
            ProgressBar1.Visibility = System.Windows.Visibility.Hidden;
            ProcessingLabel.Content = "Done Processing";
            CountLabel.Content = "Results Displayed: " + RawResults.Rows.Count;
            System.Windows.Controls.ContextMenu Contextor = new System.Windows.Controls.ContextMenu();
            
            
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
            var newtable = RawResults.Copy();
            string fxp = ""; // The string what will build our query.
            if (FilterTagText.Equals("")) return;

            if (fxp.Length > 2) fxp += " and ";
            else
            {
                var newbie = from record in RawResults.AsEnumerable()
                             where record.Field<string>("Tags").Contains(FilterTagText.Text)
                             select record;
                var newdt = GetEC2StatusTable();
                int count = newbie.Count();
                foreach(var element in newbie)
                {
                    var row = newdt.NewRow();
                    row = element;
                    newdt.ImportRow(row);

                }
                DaGrid.ItemsSource = newdt.AsDataView();
                CountLabel.Content = "Results Displayed: "+ newdt.Rows.Count;

            }






        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            DaGrid.ItemsSource = RawResults.AsDataView();
        }

        private void DaGrid_Loaded(object sender, RoutedEventArgs e)
        {

            DaGrid.ItemsSource = LoadEC2Data();
            DaGrid.ContextMenu = ECContext;
            System.Windows.Controls.MenuItem SSH = new System.Windows.Controls.MenuItem();
            SSH.Click += new RoutedEventHandler(SSH_Click);
            SSH.Header = "Open SSH";

            System.Windows.Controls.MenuItem SCP = new System.Windows.Controls.MenuItem();
            SCP.Click += new RoutedEventHandler(SCP_Click);
            SCP.Header = "Open SCP";
            

            ECContext.Items.Add(SSH);
            ECContext.Items.Add(SCP);
        }

        private System.Windows.Controls.ContextMenu ECContext = new System.Windows.Controls.ContextMenu();

        private void SSH_Click(object sender, EventArgs e)
        {
            string action = "SSH";

            
        }

        private void SCP_Click(object sender, RoutedEventArgs e)
        {
            string action = "SCP";

           

        }

        private void ProcessContext(string action, string ipaddress)
        {

        }

        private void FilepickerButton_Click(object sender, RoutedEventArgs e)
        {
            LocalFileTextbox.Text = Filepicker();
        }

        private void FileCopyButton_Click(object sender, RoutedEventArgs e)
        {
            foreach(DataRowView belch in DaGrid.ItemsSource)
            {
                var rabbit = belch.Row.Field<string>("Pub DNS");
                var result = SFTPFileCopy(rabbit, "ec2-user", LocalFileTextbox.Text, EC2dirtoCopytoTextbox.Text);
            }
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
                        toreturn += "\n Upload of " + transfer.FileName + " succeeded"; 

                    }
                }

                return toreturn;
            }
            catch (Exception e)
            {
                toreturn += "\nError: " + e;
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
    }
}
