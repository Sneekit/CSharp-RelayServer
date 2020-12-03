using System;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace RelayServer
{
	/// <summary>
	/// GUI interface to keep track of incoming and outgoing packets.
	/// </summary>
	public partial class Server : Form
	{
		public Config _config;
		public TCPProxy _proxy;
		public delegate void TextboxCallBack(string text);

		public Server()
		{
			InitializeComponent();
			StartProxy();
		}

		private void StartProxy()
		{
			_config = new Config();
			_proxy = new TCPProxy(_config);
			_proxy.Parent = this;
			AppendTextBox($"Started listener on port {_config.ListenPort}");
		}

		/// <summary>
		/// Adds a line of text to the textbox and the log
		/// Can be called outside of the class as invoke check is used
		/// </summary>
		/// <param name="text"></param>
		public void AppendTextBox(string text)
		{
			if (InvokeRequired)
				textBox1.BeginInvoke(new TextboxCallBack(AppendTextBox), new object[] { text });
			else
				textBox1.AppendText($"{Environment.NewLine}{DateTime.Now.ToString("MM/dd/yyyy hh:mm:tt")} - {text}");
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		/// <summary>
		/// Method to send a test packet over the specified port.
		/// Async allows textbox updates from the TCPProxy to be chronological.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void testPacketToolStripMenuItem_Click(object sender, EventArgs e)
		{
			// send a "checkService" socket message to java services
			byte[] msg = Encoding.ASCII.GetBytes("checkService\n");
			byte[] bytes = new byte[256];

			TcpClient testClient = new TcpClient(_config.HostIp, _config.ListenPort);
			NetworkStream netStream = testClient.GetStream();
			netStream.ReadTimeout = _config.ReadTimeout;

			AppendTextBox($"Sending service check to {_config.JavaIp}:{_config.JavaPort}");
			await netStream.WriteAsync(msg, 0, msg.Length);
			await netStream.FlushAsync();

			try
			{
				await netStream.ReadAsync(bytes, 0, bytes.Length);
				await netStream.FlushAsync();
				AppendTextBox($"Response Received: {Encoding.ASCII.GetString(bytes)}");
			}
			catch (Exception ex)
			{
				AppendTextBox(ex.Message);
			}
		}
	}
}
