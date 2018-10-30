using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;

namespace Server
{
    /// <summary>
    /// Interaction logic for ServerWindow.xaml
    /// </summary>
    public partial class ServerWindow : Window
    {
		private List<ClientInfo> clients = new List<ClientInfo>();
		DispatcherTimer dispatcherTimer = new DispatcherTimer();

		public ServerWindow()
        {
            InitializeComponent();

			// Initialise the client list
			for (int i = 0; i < clients.Count; ++i)
			{
				clients[i] = new ClientInfo();
			}				

			//Trigger the correct method when a certain packet type is received
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("Queue", QueueHelp);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("Cancel", CancelHelp);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("KeepAlive", KeepAlive);

			//Start listening for incoming connections
			Connection.StartListening(ConnectionType.TCP, new System.Net.IPEndPoint(System.Net.IPAddress.Any, 5555));
		}

		private void QueueHelp(PacketHeader header, Connection connection, string message)
		{
            bool found = false;

            // Parse connection info
			string connInfo = connection.ToString().Split('>').Last().Trim();
			string connIP = connInfo.Split(':').First();
			int connPort = int.Parse(connInfo.Split(':').Last().Split('(').First().Trim());

            // Search for already existing client
            for (int position = 0; position < clients.Count; position++)
            {
                if (clients.ElementAt(position).conn == connection)
                {
                    //MessageBox.Show("Found update: " + position);
                    //MessageBox.Show("Found update: " + connIP);
                    NetworkComms.SendObject("Update", connIP, connPort, position.ToString());
                    found = true;
                    break;
                }
            }

            // If not found then create a new client and add them to the list
            if (!found)			
			{
                ClientInfo client = new ClientInfo();				
				client.queued = true;
				client.name = message;
				client.ip = connIP;
				client.port = connPort;
				client.conn = connection;
				client.timer = 5;
                clients.Add(client);
                int position = clients.IndexOf(client);
                //MessageBox.Show("Update" + connIP + connPort + position.ToString());
                NetworkComms.SendObject("Update", connIP, connPort, position.ToString());
			}			
		}

        // Cancel the client (remove from list)
		private void CancelHelp(PacketHeader header, Connection connection, string message)
		{
			for (int position = 0; position < clients.Count; position++)
			{
				if (clients.ElementAt(position).conn == connection)
				{
					shuffleDownFrom(position, false);
					break;
				}
			}			
		}

        // Removes the client and re-orders the list
		private void shuffleDownFrom(int index, bool dead)
		{
            //MessageBox.Show("Shuffle down from: " + index);

            // Remove the client even though they still have an active connection
			if (dead == false)
			{
				NetworkComms.SendObject("RemoveClient", clients.ElementAt(index).ip, clients.ElementAt(index).port, "RemoveClient");
			}

            clients.RemoveAt(index);

            // Send each remaining client their new position in the list
            for (int position = 0; position < clients.Count; position++)
            {
				if (clients.ElementAt(position).queued == true)
				{
                    //MessageBox.Show("New position: " + position);
					NetworkComms.SendObject("Update", clients.ElementAt(position).ip, clients.ElementAt(position).port, position.ToString());
				}
			}
		}

        // Keep connection alive between client and server
		private void KeepAlive(PacketHeader header, Connection connection, string message)
		{
			for (int position = 0; position < clients.Count; position++)
			{
				if (clients.ElementAt(position).conn == connection)
				{
                    // If we establish a connection with the client then reset their timer
                    clients.ElementAt(position).timer = 5;
					break;
				}
			}
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			refreshConns();
		}

		private void sw_Loaded(object sender, RoutedEventArgs e)
		{
			dispatcherTimer.Tick += dispatcherTimer_Tick;
			dispatcherTimer.Interval = new System.TimeSpan(0,0,1);
			dispatcherTimer.Start();
		}

		private void dispatcherTimer_Tick(object sender, EventArgs e)
		{
			refreshConns();
			updateLabels();
			removeDead();
		}

		private void refreshConns()
		{
			connectionsBox.Clear();
			List<ConnectionInfo> arr = NetworkComms.AllConnectionInfo();

			foreach (ConnectionInfo conn in arr)
			{
				if (conn.ToString().Length > 43)
				{
					string connInfo = conn.ToString().Split('>').Last().Trim();
					string connIP = connInfo.Split(':').First();
					int connPort = int.Parse(connInfo.Split(':').Last().Split('(').First().Trim());

					connectionsBox.Dispatcher.BeginInvoke(new Action(() =>
					{
						connectionsBox.AppendText(connIP + ":" + connPort + "\n");
					}));
				}				
			}
		}

        // Print the names for top 3 clients in the list
		private void updateLabels()
		{
			if (clients.Count > 0 && clients.ElementAt(0).name.Length > 0)
			{
				Pos1.Text = "1. " + clients.ElementAt(0).name;
			}
			else
			{
				Pos1.Text = "";
			}

			if (clients.Count > 1 && clients.ElementAt(1).name.Length > 0)
			{
				Pos2.Text = "2. " + clients.ElementAt(1).name;
			}
			else
			{
				Pos2.Text = "";
			}

			if (clients.Count > 2 && clients.ElementAt(2).name.Length > 0)
			{
				Pos3.Text = "3. " + clients.ElementAt(2).name;
			}
			else
			{
				Pos3.Text = "";
			}
		}

        // Remove clients that have expired timers (no active connection)
		private void removeDead()
		{
			for (int position = 0; position < clients.Count; position++)
			{
				if (clients.ElementAt(position).queued == true)
				{
					if (clients.ElementAt(position).timer <= 0)
					{
                        //MessageBox.Show("No ping from client " + position);
						shuffleDownFrom(position,true);
					}
					else
					{
						clients.ElementAt(position).timer--;
					}
				}
			}
		}

        private void CancelPos1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            shuffleDownFrom(0, false);
        }        

        private void CancelPos2(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            shuffleDownFrom(1, false);
        }

        private void CancelPos3(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            shuffleDownFrom(2, false);
        }

        private void NameLabelEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var obj = sender as TextBlock;
            obj.Foreground = Brushes.Red;
        }

        private void NameLabelLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var obj = sender as TextBlock;
            obj.Foreground = Brushes.Black;
        }

    }

    public class ClientInfo : IEquatable<Connection>
    {
        public bool queued;
        public string ip;
        public string name;
        public int port;
        public Connection conn;
        public int timer;

        public ClientInfo()
        {
            queued = false;
            ip = "";
            name = "";
            port = 0;
            conn = null;
            timer = 5;
        }

        public bool Equals(Connection connection)
        {
            if (connection == null) return false;
            return (this.conn.Equals(connection));
        }
    };
}
