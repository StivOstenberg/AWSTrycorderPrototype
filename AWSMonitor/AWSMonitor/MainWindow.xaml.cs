using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AWSMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public DataTable RawResults = GetEC2StatusTable();
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

            return table;
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
                    ProcessingLabel.Content = "Checking Profile:" + aprofile + "    Region: " + region;
                    Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { ProgressBar.ValueProperty, value });
                    //Try to get scheduled events on my Profile/aregion
                    var ec2 = AWSClientFactory.CreateAmazonEC2Client(credential, region);
                    var request = new DescribeInstanceStatusRequest();
                    var response = ec2.DescribeInstanceStatus(request);


                    int count = response.InstanceStatuses.Count();

                    foreach (var instat in response.InstanceStatuses)
                    {
                        //Collect the datases
                        string instanceid = instat.InstanceId;
                        string instancename = "";
                        //How do we get the tag keys for an instance??? Argh!
                        var status = instat.Status.Status;
                        string AZ = instat.AvailabilityZone;
                        string profile = aprofile;
                        string myregion = region.DisplayName + "  -  " + region.SystemName;
                        int eventnumber = instat.Events.Count();
                        string eventlist = "";

                        var requesttags = new DescribeTagsRequest();
                        var dafilter = new Filter();
                        dafilter.Name = "resource-id";
                        dafilter.Values.Add(instanceid);
                        requesttags.Filters.Add(dafilter);
                        var tagresponse = ec2.DescribeTags(requesttags);

                        var rabbit = tagresponse.Tags;
                        string tags = "";
                        foreach(var atag in rabbit)
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



                        //Add to table


                        MyDataTable.Rows.Add(profile, myregion, instancename, instanceid, AZ, status, eventnumber, eventlist, tags);

                    }
                    value++;
                }
                
   

            }
            RawResults = MyDataTable;
            DaGrid.ItemsSource = MyDataTable.AsDataView();
            ProgressBar1.Visibility = System.Windows.Visibility.Hidden;
            ProcessingLabel.Content = "Done Processing";
            CountLabel.Content = "Results Displayed: " + RawResults.Rows.Count;
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
            if (StatusCheckbox.IsChecked==true)
            {
                fxp += "Events != '0' ";
            }
            if(EventCheckbox.IsChecked==true)
            {
                if (fxp.Length > 2) fxp += " and ";
                fxp += "Status != 'ok'";
            }
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

    }
}
