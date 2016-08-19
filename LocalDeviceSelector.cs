using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;

namespace Local_Device_Selector.NET
{
    public static partial class Extensions
    {
        public static Boolean IsNull(this Object obj) =>
            obj is String ? String.IsNullOrEmpty(obj as String) : obj == null;

        public static Boolean IsHexString(this String hexStr) =>
            hexStr.Select(c => c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F' || c >= '0' && c <= '9').All(isHexChar => isHexChar);
    }

    internal class LocalDeviceInfo
    {
        public readonly String ipAddress, macAddress;
        public readonly Boolean IsAvailable;

        protected String hostName;

        public String HostName
        {
            get
            {
                if (hostName.IsNull())
                {
                    try
                    {
                        // Retrieve the "Host Name" for this IP Address. This is the "Name" of the machine.
                        hostName = System.Net.Dns.GetHostEntry(ipAddress).HostName;
                    }
                    catch
                    {
                        hostName = "N/A";
                    }
                }
                return hostName;
            }
        }

        public LocalDeviceInfo(String IPAddress, String MACAddress)
        {
            ipAddress = IPAddress;
            macAddress = MACAddress;
        }

        public override String ToString() =>
            String.Format("ARP-Table Index: {0}\r\nIP-Address\t{1}\nMAC-Address\t{2}", LocalDeviceInfo.GetDeviceIndex(ipAddress), ipAddress, macAddress);

        #region Static
        protected static List<LocalDeviceInfo> deviceList;

        //overload operator for comparison
        public static Boolean operator ==(LocalDeviceInfo a, LocalDeviceInfo b) => EqualityComparer<LocalDeviceInfo>.Default.Equals(a, b);
        public static Boolean operator !=(LocalDeviceInfo a, LocalDeviceInfo b) => !(a == b);

        public static List<LocalDeviceInfo> GetARP()
        {
            deviceList = new List<LocalDeviceInfo>();
            foreach (var arp in FetchARPTable().Split(new Char[] { '\n', '\r' }))
            {
                //Parse out all the MAC / IP Address combinations
                if (!arp.IsNull())
                {
                    var parts = (from part in arp.Split(new Char[] { ' ', '\t' })
                                 where !part.IsNull()
                                 select part).ToArray();

                    if (parts.Length == 3) deviceList.Add(new LocalDeviceInfo(parts[0], parts[1]));
                    //else throw new Exception("Could not parse ARP-Table...");
                }
            }
            return deviceList;
        }

        //This runs the "arp" utility in Windows to retrieve all the MAC / IP Address entries.
        protected static String FetchARPTable()
        {
            String res = null;

            try
            {
                var proc = Process.Start(new ProcessStartInfo("arp", "-a")
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });

                res = proc.StandardOutput.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw new Exception("GetARPResults(): Error Retrieving 'arp -a' results", ex);
            }
            return res;
        }

        public static LocalDeviceInfo GetDeviceInfoByMAC(String macAddress) => 
            GetARP().Where(dev => dev.macAddress.ToLowerInvariant() == macAddress.ToLowerInvariant()).Select(dev => dev).FirstOrDefault();

        public static LocalDeviceInfo GetDeviceInfoByIP(String ipAddress) =>
            GetARP().Where(dev => dev.ipAddress.ToLowerInvariant() == ipAddress.ToLowerInvariant()).Select(dev => dev).FirstOrDefault();

        public static Int32 GetDeviceIndex(String ipAddress) =>
            GetDeviceIndex(GetDeviceInfoByIP(ipAddress));

        protected static Int32 GetDeviceIndex(LocalDeviceInfo dev)
        {
            var index = ~0;
            for (var i = 0; i < deviceList.Count; i++) if (dev == deviceList[i]) index = i;
            return index;
        }

        

        #endregion

        #region Mac-Address Lookup API
        /* *************************
         * Mac-Address Lookup API  *
         *****************************************************************************************
         * All Documentations can be found here: http://www.macvendorlookup.com/mac-address-api ***
         * ***************************************************************************************
         * 
         * Return Value Descriptions:
         * ***************************
         * In all of the reponse formats, the order of the values are the same.
         * A description of each value is below.
         * 
         * startHex    The start of the MAC address range the vendor owns in hexadecimal format
         * endHex      The end of the MAC address range the vendor owns in hexadecimal format
         * startDec    The start of the MAC address range the vendor owns in decimal format
         * endDec      The end of the MAC address range the vendor owns in decimal format
         * company     Company name of the vendor or manufacturer
         * addressL1   First line of the address the company provided to IEEE
         * addressL2   Second line of the address the company provided to IEEE
         * addressL3   Third line of the address the company provided to IEEE
         * country     Country the company is located in
         * type        There are 3 different IEEE databases: oui24, oui36, and iab
         * 
         */

        public String getVendor(String macAddress)
        {
            String mac = macAddress.Replace("-", "");

            if (!mac.IsNull() && mac.Length < 6 && mac.IsHexString())
                throw new FormatException("Exception occured in 'getVendor()'\n\nMAC-Address has a incorrect format!");

            /* OUI => Organizationally Unique Identifier
             * First 24 Bits of MAC-Address => 3 Bytes => 6 Character   */
            String oui = mac.Substring(0, 6);

            String[] lookupResult = null;

            try
            {
                lookupResult = new System.Net.WebClient().DownloadString("http://www.macvendorlookup.com/api/v2/" + oui + "/pipe").Split('|');

                if (lookupResult.Count() != 10)
                    throw new Exception("macvendorlookup.com API Error");

                return lookupResult[4];
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.ToString());
            }

            return "Couldn't resolve device vendor";
        }
        #endregion
    }

    public class DeviceSelector : Form
    {
        List<LocalDeviceInfo> arpTable;

        Int32 LVIndex
        {
            get
            {
                if (deviceLV.SelectedItems.Count > 0)
                    return deviceLV.SelectedItems[0].Index;

                return ~0;
            }
        }

        void Refresh()
        {
            arpTable = LocalDeviceInfo.GetARP();
            Int32 tableCount = arpTable.Count;
            if (tableCount <= 0) MessageBox.Show("ARP-Table empty");
            else
            {
                deviceLV.Items.Clear();
                MessageBox.Show(arpTable.Count.ToString());
                for (var i = 0; i < tableCount; i++)
                {
                    var items = new[]
                    {
                    new ListViewItem.ListViewSubItem() { Text = arpTable[i].ipAddress, Tag = arpTable[i] },
                    new ListViewItem.ListViewSubItem() { Text = arpTable[i].macAddress, Tag = arpTable[i] },
                   // new ListViewItem.ListViewSubItem() { Text = arpTable[i].IsAvailable.ToString(), Tag = arpTable[i], ForeColor = arpTable[i].IsAvailable ? Color.LightGreen : Color.Red }
                    };

                    deviceLV.Items.Add(i.ToString()).SubItems.AddRange(items);
                }
            }
        }

        void Select()
        {

        }
        
        void HandleButtonEvents(Object sender, dynamic e)
        {
            var btn = sender as Button;

            if (btn == refreshBtn)
                Refresh();
            else if (btn == selectBtn)
                Select();
            else
                throw new Exception("Unknown sender in 'HandleButtonEvent()'");
        }

        #region Draw GUI
        System.ComponentModel.IContainer components = null;

        public DeviceSelector()
        {
            InitializeForm();

            //virtual refresh btn click
            refreshBtn.PerformClick();
        }

        protected override void Dispose(Boolean disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        ListView deviceLV;
        Label hostLbl, vendorLbl;
        Label[] labels;
        GroupBox box;
        Button refreshBtn, selectBtn;

        //don't change anything here unless you know what you do...
        void InitializeForm()
        {
            Application.EnableVisualStyles();
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            deviceLV = new ListView()
            {
                Size = new Size(0, 131),
                Location = new Point(5, 5),
                View = View.Details,
                GridLines = true,
                MultiSelect = false,
                FullRowSelect = true,
                AllowColumnReorder = false,
            };

            //deviceLV.SelectedIndexChanged += (() => );

            //setup all columns to calculate the correct component sizes
            var columns = new[]
            {
                new ColumnHeader()
                {
                    Text = "Index",
                    Width = 50
                },
                new ColumnHeader()
                {
                    Text = "IPv4-Address",
                    Width = 100,
                    TextAlign = HorizontalAlignment.Center
                },
                new ColumnHeader()
                {
                    Text = "MAC-Address",
                    Width = 110,
                    TextAlign = HorizontalAlignment.Center
                },
                new ColumnHeader()
                {
                    Text = "Available?",
                    Width = 69,
                    TextAlign = HorizontalAlignment.Center
                }
            };

            deviceLV.Columns.AddRange(columns);

            //Set now the correct size for the control
            var countedSize = 0;
            Array.ForEach<ColumnHeader>(deviceLV.Columns.Cast<ColumnHeader>().ToArray(), elem => countedSize += elem.Width);
            deviceLV.Width = countedSize + (deviceLV.Columns.Count - 1 * 8) + 1;

            var boldFont = new Font(DefaultFont, FontStyle.Bold);

            labels = new[]
            {
                new Label
                {
                    AutoSize = true,
                    Text = "Device Vendor",
                    Font = boldFont,
                    Location = new Point(57, 18),

                },
                new Label
                {
                    AutoSize = true,
                    Text = "DNS Hostname",
                    Font = boldFont,
                    Location = new Point(50, 36),
                }
            };

            vendorLbl = new Label()
            {
                AutoSize = true,
                Text = "Unknown",
                Location = new Point(labels[1].Location.X + 115, labels[0].Location.Y)
            };
            hostLbl = new Label()
            {
                AutoSize = true,
                Text = "Unknown",
                Location = new Point(vendorLbl.Location.X, labels[0].Location.Y + 17)
            };


            refreshBtn = new Button()
            {
                TabIndex = 0,
                Text = "Refresh",
                Size = new Size((Width / 2) - 17, 21),
                Location = new Point(deviceLV.Location.X, deviceLV.Size.Height + 10)
            };

            selectBtn = new Button()
            {
                TabIndex = 1,
                Text = "Select Device",
                Size = new Size(refreshBtn.Width, 21),
                Location = new Point((Width / 2) - 4, refreshBtn.Location.Y)
            };

            box = new GroupBox()
            {
                Text = "Current Selection",
                Location = new Point(deviceLV.Location.X, refreshBtn.Location.Y + refreshBtn.Size.Height + 10),
                Size = new Size(deviceLV.Width, 31 * 2)
            };

            //Setup form-controls events
            var handler = new EventHandler(HandleButtonEvents);
            refreshBtn.Click += handler;
            selectBtn.Click += handler;

            box.Controls.AddRange(labels);
            box.Controls.Add(vendorLbl);
            box.Controls.Add(hostLbl);
            Controls.Add(deviceLV);
            Controls.Add(box);
            Controls.Add(refreshBtn);
            Controls.Add(selectBtn);

            Width = deviceLV.Width + 26;
            Height = deviceLV.Height + refreshBtn.Height + box.Height + 69;

            box.SuspendLayout();
            SuspendLayout();
        }

        #endregion
    }
}
