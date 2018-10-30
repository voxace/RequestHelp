using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;
using System.IO;

namespace RequestHelpClient
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class ClientWindow : Window
	{
		DispatcherTimer dispatcherTimer = new DispatcherTimer();
		string serverInfo;
		string serverIP;
		int serverPort;
		int currentPosition = 0;
		string name;
		private bool _isExit;

		public System.Windows.Forms.NotifyIcon _notifyIcon;

		public ClientWindow()
		{
			InitializeComponent();

			_notifyIcon = new System.Windows.Forms.NotifyIcon();
			_notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
			_notifyIcon.Visible = true;
			_notifyIcon.Icon = new System.Drawing.Icon("help-64.ico");
			_notifyIcon.Visible = true;
			_notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
			_notifyIcon.ContextMenuStrip.Items.Add("Request Help").Click += (s, e) => ShowMainWindow();	
			_notifyIcon.Text = "Request for help.";

			name = getFullName();
			CancelButton.IsEnabled = false;
		
			readServerInfo();

			//Trigger the correct method when a certain packet type is received
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("Update", Update);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("RemoveClient", RemoveClient);

			dispatcherTimer.Tick += dispatcherTimer_Tick;
			dispatcherTimer.Interval = new System.TimeSpan(1000);
		}

		private void readServerInfo()
		{
			try
			{
				string path = "serverInfo.txt";

				if (!File.Exists(path))
				{
					MessageBox.Show("Server info not found!");
				}

				serverInfo = File.ReadAllText(path);
				serverIP = serverInfo.Split(':').First();
				serverPort = int.Parse(serverInfo.Split(':').Last());
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void ShowMainWindow()
		{
			if (Mw.IsVisible)
			{
				if (Mw.WindowState == WindowState.Minimized)
				{
					Mw.WindowState = WindowState.Normal;
				}
				Mw.Activate();
			}
			else
			{
				Mw.Show();
				SendForHelp();
				_notifyIcon.ContextMenuStrip.Items.RemoveAt(0);
			}
		}

		private void Mw_Closing(object sender, CancelEventArgs e)
		{
			if (!_isExit)
			{
				genContextMenu();
				e.Cancel = true;
				Mw.Hide();
				_notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
				_notifyIcon.BalloonTipText = "This program has minimised to the tray. You can open it back up by double-clicking the icon.";
				_notifyIcon.BalloonTipTitle = "Program still running.";
				_notifyIcon.ShowBalloonTip(3000);
			}
		}

		private void genContextMenu()
		{
            _notifyIcon.ContextMenuStrip.Items.Clear();

			if (currentPosition >= 0)
			{
				_notifyIcon.ContextMenuStrip.Items.Add("Check Status").Click += (s, e) => ShowMainWindow();
			}
			else
			{
				_notifyIcon.ContextMenuStrip.Items.Add("Request Help").Click += (s, e) => ShowMainWindow();
			}

            // Exit button
            // _notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (s, e) => ExitApplication();
        }

        private void ExitApplication()
		{
			_isExit = true;
			Mw.Close();
			_notifyIcon.Dispose();
			_notifyIcon = null;
		}

		private void dispatcherTimer_Tick(object sender, EventArgs e)
		{
			try
			{
				if (Connection.Listening(ConnectionType.TCP) == true || Connection.Listening(ConnectionType.UDP) == true)
				{
					NetworkComms.SendObject("KeepAlive", serverIP, serverPort, name);
				}
			}
			catch (Exception)
			{
				Mw.Close();
				dispatcherTimer.Stop();
				_notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
				_notifyIcon.BalloonTipText = "Error connecting to server. Please try again later.";
				_notifyIcon.BalloonTipTitle = "Error connecting.";
				_notifyIcon.ShowBalloonTip(3000);
				_notifyIcon.Text = "Error connecting.";
				PositionLabel.Text = "Not in Queue...";
				CancelButton.IsEnabled = false;
			}		
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				NetworkComms.SendObject("Cancel", serverIP, serverPort, name);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error: " + ex.Message);
			}
		}

		public void SendForHelp()
		{
			try
			{
				NetworkComms.SendObject("Queue", serverIP, serverPort, name);

				List<ConnectionInfo> arr = NetworkComms.AllConnectionInfo();
				string connInfo = "";
				string connIP = "";
				int connPort = 0;

				foreach (ConnectionInfo conn in arr)
				{
					connInfo = conn.ToString().Split(']').Last().Split('-').First().Trim();
					connIP = connInfo.Split(':').First();
					connPort = int.Parse(connInfo.Split(':').Last().Trim());
				}

				//Start listening for incoming connections if not already doing so
				if (Connection.Listening(ConnectionType.TCP) == false)
				{					
					Connection.StartListening(ConnectionType.TCP, new System.Net.IPEndPoint(System.Net.IPAddress.Any, connPort));
					dispatcherTimer.Start();
				}
				
			}
			catch(Exception)
			{
				_notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
				_notifyIcon.BalloonTipText = "Error connecting to server. Please try again later.";
				_notifyIcon.BalloonTipTitle = "Error connecting.";
				_notifyIcon.ShowBalloonTip(3000);
				_notifyIcon.Text = "Error connecting.";
			}

		}

		private void Update(PacketHeader header, Connection connection, string message)
		{
            //MessageBox.Show("Client: " + message);
            bool parsed = int.TryParse(message, out currentPosition);

            if (parsed)
            {
                currentPosition++;

                PositionLabel.Dispatcher.BeginInvoke(new Action(() =>
                {
                    PositionLabel.Text = "Position: " + currentPosition.ToString();
                    CancelButton.IsEnabled = true;
                    _notifyIcon.Text = "Position: " + currentPosition.ToString();
                }));
            }
		}

		private void RemoveClient(PacketHeader header, Connection connection, string message)
		{
			currentPosition = -1;

			Dispatcher.BeginInvoke(new Action(() =>
			{
				PositionLabel.Text = "Not in Queue...";
				CancelButton.IsEnabled = false;
				_notifyIcon.Text = "Request for help.";
				Mw.Close();
			}));
			
			Connection.StopListening();
		}

		private string getFullName()
		{
			try
			{
				Thread.GetDomain().SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);
				WindowsPrincipal principal = (WindowsPrincipal)Thread.CurrentPrincipal;
				using (PrincipalContext pc = new PrincipalContext(ContextType.Domain))
				{
					UserPrincipal up = UserPrincipal.FindByIdentity(pc, principal.Identity.Name);
					return up.GivenName + " " + up.Surname;
				}
			}
			catch (Exception)
			{				
				try
				{
					return UserPrincipal.Current.DisplayName;
				}
				catch (Exception)
				{
					return Environment.UserName;
				}
			}
		}
	}
}
