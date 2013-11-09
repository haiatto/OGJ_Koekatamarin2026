
using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Collections.Generic;

#if true
using WebSocketSharp;
 
public class KoeKatamarinBehaviourScript : MonoBehaviour {
 
    // Use this for initialization
    void Start () {
        Connect();
    }
 
    // Update is called once per frame
    void Update () {
 
    }
#if true
    /// <summary>
    /// The message which store current input text.
    /// </summary>
    string message = "";
    /// <summary>
    /// The list of chat message.
    /// </summary>
    List<string> messages = new List<string>();
#endif
    /// <summary>
    /// Raises the GU event.
    ///
    /// </summary>
    void OnGUI(){
#if true
        // Input text
        message = GUI.TextArea(new Rect(0,10,Screen.width * 0.7f,Screen.height / 10),message);
 
        // Send message button
        if(GUI.Button(new Rect(Screen.width * 0.75f,10,Screen.width * 0.2f,Screen.height / 10),"Send")){
            SendChatMessage();
        }
 
        // Show chat messages
        var l = string.Join("\n",messages.ToArray());
        var height = Screen.height * 0.1f * messages.Count;
        GUI.Label(
            new Rect(0,Screen.height * 0.1f + 10,Screen.width * 0.8f,height),
            l);
#endif
    }
#if true
    WebSocket ws;
#endif
    void Connect(){
#if true
        ws =  new WebSocket("ws://localhost:8888");
 
        // called when websocket messages come.
        ws.OnMessage += (sender, e) =>
        {
            string s = e.Data;
            Debug.Log(string.Format( "Receive {0}",s));
            messages.Add("> " + e.Data);
            if(messages.Count > 10){
                messages.RemoveAt(0);
            }
        };
 
        ws.Connect();
        Debug.Log("Connect to " + ws.Url);
#endif
    }
 
    void SendChatMessage(){
#if true
        Debug.Log("Send message " + message);
        ws.Send(message);
        this.message = "";
#endif
    }
}
#endif


#if true
public class KoeInputServerScript : MonoBehaviour {
	
	IPEndPoint remoteIP;
	byte[] data;
	Socket s;
	
	void OnGUI(){
		if(GUI.Button (new Rect (10,10,100,100), "Send Packet")){
			s.SendTo(data, 0, data.Length, SocketFlags.None, remoteIP);
		}
	}
	
	// Use this for initialization
	void Start () {
		remoteIP = new IPEndPoint(IPAddress.Parse("192.168.102.11"), 1111);
		data = new byte[16];
		s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 255);

	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
#endif

#if false
public delegate void OnReceiveMessage(string message);

public class KoeInputServerScript : MonoBehaviour
{
	private OnReceiveMessage onReceiveMessage;
	private bool running;
	
	private string msg = "";
	
	public int port;
	
	private Thread mThread;
	
	private TcpListener tcp_Listener = null;
	
	private List<Client> arrReader;
	void Start()
	{
		this.onReceiveMessage = onReceiveMessage;
		
		running = true;
		arrReader = new List<Client>();
		ThreadStart ts = new ThreadStart(ClientCheck );	
		mThread = new Thread(ts);
		mThread.Start();
	}
	void ClientCheck()
	{
		try
		{
			tcp_Listener = new TcpListener(port);
	
			tcp_Listener.Start();
			while (running)
			{
				if (!tcp_Listener.Pending())
				{
					Thread.Sleep(100);
				}
				else
				{
					Debug.Log("connected");
					TcpClient client = tcp_Listener.AcceptTcpClient();	
					Debug.Log("accepted");
					arrReader.Add(new Client(client , onReceiveMessage));
				}
			}	
		}
		catch (ThreadAbortException)
		{
			Debug.Log("exception");	
		}
		finally
		{
			running = false;
			tcp_Listener.Stop();	
		}
	}
	public void StopListening()
	{	
		running = false;	
		mThread.Join(500);
	}
}

public class Client
{
	private Thread tread;
	
	private StreamReader streamReader;
	
	private bool running = true;
	
	private OnReceiveMessage onReceiveMessage;
	
	private NetworkStream ns;
	public Client (TcpClient client , OnReceiveMessage onReceiveMessage)
	{
		this.onReceiveMessage = onReceiveMessage;
		ns = client.GetStream();
		
		streamReader = new StreamReader(ns);
		
		ThreadStart ts = new ThreadStart(StartTread);
		
		tread = new Thread(ts);
		
		tread.Start();
	}
	private void StartTread()
	{
		while(running)
		{
			string msg = streamReader.ReadLine();	
			onReceiveMessage(msg);
		}
	}
}

#endif