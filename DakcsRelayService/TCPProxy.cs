using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace RelayServer
{
	public partial class TCPProxy
	{
		// Load utliity configurations
		Config _config;
		public TcpListener _server;

		public TCPProxy(Config config)
		{
			_config = config;

			//Initialize listener
			Task.Run(StartListener);
		}


		/// <summary>
		/// Method to start a listener for incoming TCP packets
		/// </summary>
		/// <returns></returns>
		public async Task StartListener()
		{
			// Initialize a server listener with the provided local ip address and port
			Server = new TcpListener(IPAddress.Any, _config.ListenPort);
			Server.Start();

			while (true)
			{
				// accept new incoming tcp clients
				TcpClient listenClient = await Server.AcceptTcpClientAsync().ConfigureAwait(false);
				listenClient.NoDelay = true;

				// a janky way to have this work but I liked it more than Threadpools
				try
				{
					Task.Run(() => { TransferStreams(listenClient); });
				}
				catch (Exception e)
				{
					Parent?.AppendTextBox($"[Server] Error Encountered: {e.Message}\n\n{e.InnerException}");
				}
			}
		}

		private byte[] GetBytes(String text)
		{
			return new UTF8Encoding(true).GetBytes(text);
		}

		/// <summary>
		/// Task to read the TCP socket, translate to SSL, send to server, and translate the response back to the original TCP connection
		/// </summary>
		/// <param name="listenclient"></param>
		/// <returns></returns>
		public async Task TransferStreams(TcpClient listenclient)
		{
			using (NetworkStream clientStream = listenclient.GetStream())
			using (TcpClient javaclient = new TcpClient(_config.JavaIp, _config.JavaPort))
			using (SslStream javaStream = new SslStream(javaclient.GetStream(), false, new RemoteCertificateValidationCallback(RemoteCertificateValidation), new LocalCertificateSelectionCallback(LocalCertificateSelection)))
			{
				Parent?.AppendTextBox($"[Server] New TCP connection received");

				// set all the time outs
				listenclient.ReceiveTimeout = javaclient.ReceiveTimeout = _config.ReadTimeout;
				listenclient.SendTimeout = javaclient.SendTimeout = _config.WriteTimeout;

				//Create collection of certificates ( not sure why it needs a collection but it does )
				X509Certificate2Collection certs = new X509Certificate2Collection();
				X509Certificate2 cert = _config.Certificate;
				certs.Add(cert);

				// Authenticate, set TLS and attach cert
				try
				{
					await javaStream.AuthenticateAsClientAsync(_config.JavaIp, certs, SslProtocols.Tls12, false);
				}
				catch (Exception e)
				{
					Parent?.AppendTextBox($"[Server] Error Encountered: {e.Message}\n\n{e.InnerException}");
				}

				// read from listening stream
				byte[] payload = await ReadNetworkStream(clientStream);

				// write to java stream
				await javaStream.WriteAsync(payload, 0, payload.Length);
				await javaStream.FlushAsync();

				// read the response from the java stream
				payload = await ReadSslStream(javaStream);

				// write the response back to the original client
				await clientStream.WriteAsync(payload, 0, payload.Length);
				await clientStream.FlushAsync();

				Parent?.AppendTextBox($"[Server] Response sent to TCP connection");

				// close listening client
				listenclient.Dispose();
			}
		}

		/// <summary>
		/// Method to confirm that the certificate defined in the configuration file is not expired.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="certificate"></param>
		/// <param name="chain"></param>
		/// <param name="sslPolicyErrors"></param>
		/// <returns></returns>
		private bool RemoteCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			X509Certificate2 clientCert = certificate == null ? null : new X509Certificate2(certificate);
			if (clientCert != null)
			{
				DateTime effectivDate = DateTime.Parse(clientCert.GetEffectiveDateString());
				DateTime expireDate = DateTime.Parse(clientCert.GetExpirationDateString());

				//make sure at least this certificate is valid
				if (DateTime.Now < effectivDate || DateTime.Now > expireDate)
					return false;
			}

			return true;
		}

		/// <summary>
		/// Task to read the incoming TCP socket stream
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		private async Task<byte[]> ReadNetworkStream(NetworkStream stream)
		{
			byte[] buffer = new byte[1024];
			using (MemoryStream ms = new MemoryStream())
			{
				int bytesread = 0;
				while (stream.DataAvailable)
				{
					bytesread = await stream.ReadAsync(buffer, 0, buffer.Length);
					ms.Write(buffer, 0, bytesread);
				}

				return ms.ToArray();
			}
		}

		/// <summary>
		/// Task to read the incoming SSL stream
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		private async Task<byte[]> ReadSslStream(SslStream stream)
		{
			byte[] buffer = new byte[1024];
			using (MemoryStream ms = new MemoryStream())
			{
				int bytesread = 0;
				while ((bytesread = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
				{
					ms.Write(buffer, 0, bytesread);
				}

				return ms.ToArray();
			}
		}

		/// <summary>
		/// Allows certification selection. Defaulted to global certificate for simplification of sample code.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="targetHost"></param>
		/// <param name="localCertificates"></param>
		/// <param name="remoteCertificate"></param>
		/// <param name="acceptableIssuers"></param>
		/// <returns></returns>
		private X509Certificate2 LocalCertificateSelection(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
		{
			return _config.Certificate;
		}

		public Server Parent { get; set; }
		public TcpListener Server
		{
			get
			{
				return _server;
			}
			set
			{
				_server = value;
			}
		}

	}
}
