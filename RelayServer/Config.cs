using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Forms;

namespace RelayServer
{
	/// <summary>
	/// Global configurations for the server
	/// </summary>
	public class Config
	{
		// public variables
		public string _programdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RelayServer");
		public X509Certificate2 _certificate;
		public string _listenip;
		public string _javaip;
		public int _listenport;
		public int _javaport;
		public int _readtimeout;
		public int _writetimeout;
		public string _certfile;
		public string _inipath;

		// load kernel32 as it already contains code to read ini files
		[DllImport("kernel32")]
		private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

		public Config()
		{
			// build necessary files and directories
			BuildFiles();

			// Load Certificate File
			LoadCertificate();

			// Load settings from ini file
			LoadConfigs();
		}

		public void BuildFiles()
		{
			// create if it does not exist
			if (!Directory.Exists(_programdir))
				Directory.CreateDirectory(_programdir);

			// Load the ini file, creating it with defaults if it does not exist
			_inipath = Path.Combine(_programdir, "config.ini");
			if (!File.Exists(_inipath))
			{
				using (StreamWriter sw = File.CreateText(_inipath))
				{
					// default values
					sw.WriteLine("[Config]");
					sw.WriteLine("hostip=127.0.0.1");
					sw.WriteLine("*REDACTED*");
					sw.WriteLine("hostport=8841");
					sw.WriteLine("servicesport=4001");
					sw.WriteLine("certificate.pfx");
					sw.WriteLine("readtimeout=20000");
					sw.WriteLine("writetimeout=20000");
					sw.Close();
				}
			}
		}

		public void LoadCertificate()
		{
			_certfile = IniReadValue("Config", "certificate");
			string certpath = Path.Combine(_programdir, _certfile);
			if (!File.Exists(certpath))
			{
				MessageBox.Show($"Unable to locate certificate file: {_certfile}", "Error Loading File", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}
			else
				_certificate = new X509Certificate2(certpath);
		}

		public void LoadConfigs()
		{
			_listenip = IniReadValue("Config", "hostip");
			_listenport = Convert.ToInt32(IniReadValue("Config", "hostport"));
			_javaip = (Dns.GetHostAddresses(IniReadValue("Config", "servicesaddress"))[0]).ToString();
			_javaport = Convert.ToInt32(IniReadValue("Config", "servicesport"));
			_readtimeout = Convert.ToInt32(IniReadValue("Config", "readtimeout"));
			_writetimeout = Convert.ToInt32(IniReadValue("Config", "writetimeout"));
		}

		// kernel32 method to read from ini file
		public string IniReadValue(string section, string key)
		{
			StringBuilder temp = new StringBuilder(255);
			int i = GetPrivateProfileString(section, key, "", temp, 255, _inipath);
			return temp.ToString();
		}

		public string HostIp { get { return _listenip; } }
		public string JavaIp { get { return _javaip; } }
		public int ListenPort { get { return _listenport; } }
		public int JavaPort { get { return _javaport; } }
		public int ReadTimeout { get { return _readtimeout; } }
		public int WriteTimeout { get { return _writetimeout; } }
		public X509Certificate2 Certificate { get { return _certificate; } }
	}
}
