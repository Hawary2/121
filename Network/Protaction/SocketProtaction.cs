using C_Socket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AccServer.Network.Protaction
{
    public class Socket
    {
        public void init()
        {
            SocketEvents sockEvents = new SocketEvents();
            sockEvents.OnConnection = new ConnectionEvent(Handle_Connection);
            sockEvents.OnDisconnection = new ConnectionEvent(Handle_Disconnection);
            sockEvents.OnReceive = new BufferEvent(Handle_Receive);
            Server server = new Server(sockEvents);
            server.Start("0.0.0.0", 9977);
            Console.WriteLine("Protaction Start OnPort ." + 9977);
        }

        private bool Handle_Receive(C_Socket.Client sClient, int Size, byte[] Packet)
        {
            string mess = Encoding.Default.GetString(Packet);
            var data = mess.Split(':');
            if (data.Length < 2)
            {
                sClient.Disconnect();
            }
            switch (data[0])
            {
                case "OK"://simple OK:12252135   1225213 = Account UID
                    {
                        var uid = uint.Parse(data[1]);
                        if (uid == 0)
                            return true;
                        if (sClient.ClientEditor)
                        {
                            
                        }
                        sClient.Send(ASCIIEncoding.Default.GetBytes("OK:OFF" + "<eof>"));//Send A Single message to say the server is running 
                        break;
                    }
                case "FILE":
                    {
                        sClient.ClientEditor = true;
                        break;
                    }
                case "PROG":
                case "TIT":
                    {
                        sClient.ClientEditor = true;
                        break;
                    }
            }
            return true;
        }

        private bool Handle_Disconnection(C_Socket.Client sClient)
        {
            return true;
        }

        private bool Handle_Connection(C_Socket.Client sClient)
        {
            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:ini/MagicEffect.ini:CBCE2843D191D932C29FE7BF5E02B995E5D8BB46" + "<eof>"));//File To Cheack it with sha1 Sum Cheack
            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:ini/MagicType.dat:E84B8390708C559D10BBE8945937608354CC645E" + "<eof>"));//same
            sClient.Send(ASCIIEncoding.Default.GetBytes("PROG:cheat~click~speed~tasker" + "<eof>"));//Program exe  part of name ;
            sClient.Send(ASCIIEncoding.Default.GetBytes("TIT:cheat~click~speed~tasker" + "<eof>"));// Program Window Title part of name ;
            return true;
        }
    }

}




namespace C_Socket
{


    // State object for receiving data from remote device.
    public class Server
    {
        /// <summary>
        /// The socket associated with the server.
        /// </summary>
        private Socket serverSocket;

        /// <summary>
        /// The socket events associated with the server.
        /// </summary>
        private SocketEvents socketEvents;

        /// <summary>
        /// Creates a new socket server.
        /// </summary>
        /// <param name="socketEvents">The events associated with the server. Put null for nothing.</param>
        public Server(SocketEvents socketEvents)
        {
            if (socketEvents == null)
                this.socketEvents = new SocketEvents();
            else
                this.socketEvents = socketEvents;

            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="ip">The IP of the server.</param>
        /// <param name="port">The port of the server.</param>
        public void Start(string ip, int port)
        {
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(500);
            Accept();
        }

        /// <summary>
        /// Starts to accept connections.
        /// </summary>
        private void Accept()
        {
            serverSocket.BeginAccept(new AsyncCallback(Accept_Callback), null);
        }

        /// <summary>
        /// The callback from Accept()
        /// </summary>
        /// <param name="asyncResult">The async result.</param>
        private void Accept_Callback(IAsyncResult asyncResult)
        {
            try
            {
                Socket accepted = serverSocket.EndAccept(asyncResult);
                if (accepted.Connected)
                {
                    Client sClient = new Client(accepted, socketEvents);
                    if (socketEvents.OnConnection.Invoke(sClient))
                    {
                        sClient.Alive = true;
                        sClient.Receive();
                    }
                }
            }
            catch { }
            Accept();
        }
    }

    public class Client
    {
        /// <summary>
        /// The socket associated with the client.
        /// </summary>
        private Socket clientSocket;

        /// <summary>
        /// The socket events associated with the server.
        /// </summary>
        private SocketEvents socketEvents;

        /// <summary>
        /// The buffer holding all bytes received.
        /// </summary>
        private byte[] dataHolder;

        /// <summary>
        /// An object which is the owner of the client.
        /// </summary>
        public object Connector { get; set; }
        public string IP { get; set; }
        public string LocalIp { get; set; }
        public bool Alive { get; set; }
        private object send_lock;

        public Client(Socket acceptedsocket, SocketEvents socketEvents)
        {
            clientSocket = acceptedsocket;
            this.socketEvents = socketEvents;
            System.Threading.Interlocked.CompareExchange(ref send_lock, new object(), null);
        }
        public void Connect(IPAddress ip, int port)
        {

            clientSocket.Connect(new IPEndPoint(ip, port));
        }

        internal void Receive()
        {
            try
            {
                if (!Connected)
                {
                    Disconnect("Not connected.");
                    return;
                }

                dataHolder = new byte[65535];
                clientSocket.BeginReceive(dataHolder, 0, dataHolder.Length, SocketFlags.None, new AsyncCallback(Receive_Callback), null);
            }
            catch (Exception e)
            {
                Disconnect("Failed beginreceive. Exception: " + e.ToString());
            }
        }

        private void Receive_Callback(IAsyncResult asyncResult)
        {
            try
            {
                if (!clientSocket.Connected)
                {
                    Disconnect("Not connected.");
                    return;
                }
                SocketError err;
                int rSize = clientSocket.EndReceive(asyncResult, out err);

                if (err != SocketError.Success)
                {
                    Disconnect("Failed receive. (99% regular DC) Socket Error: " + err.ToString());
                    return;
                }
                if (rSize < 4)
                {
                    Disconnect("Invalid Packet Header. (99% regular DC) Size: " + rSize);
                    return;
                }
                byte[] rBuffer = new byte[rSize];
                System.Buffer.BlockCopy(dataHolder, 0, rBuffer, 0, rSize);
                if (!alreadyDisconnected)
                {
                    socketEvents.OnReceive.Invoke(this, rSize, rBuffer);
                }
            }
            catch (Exception e)
            {
                Disconnect(e.ToString());
            }
            Receive();
        }
        public bool Connected
        {
            get
            {
                if (clientSocket == null)
                    return false;
                return clientSocket.Connected;
            }
        }
        public void Disconnect(string reason)
        {
            if (alreadyDisconnected)
                return;
            alreadyDisconnected = true;
            dcReason = reason;
            Console.WriteLine(reason);
            try
            {
                clientSocket.Disconnect(false);
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            socketEvents.OnDisconnection.Invoke(this);
        }
        public void Disconnect()
        {
            if (alreadyDisconnected)
                return;
            alreadyDisconnected = true;
            try
            {
                clientSocket.Disconnect(false);
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }

            socketEvents.OnDisconnection.Invoke(this);
        }
        private bool alreadyDisconnected = false;

        /// <summary>
        /// Gets a boolean defining whether the client is already disconnected or not.
        /// </summary>
        public bool AlreadyDisconnected
        {
            get { return alreadyDisconnected; }
        }
        /// <summary>
        /// The disconnect reason.
        /// </summary>
        private string dcReason;

        /// <summary>
        /// Gets the disconnect reason.
        /// </summary>
        public string DCReason
        {
            get { return dcReason; }
        }

        public bool ClientEditor { get; internal set; }

        public void Send(byte[] Buffer)
        {
            if (Buffer.Length > 65535)
            {
                Disconnect("Too big packet...");
                return;
            }

            lock (send_lock)
            {
                try
                {
                    if (clientSocket.Connected)
                    {

                        clientSocket.Send(Buffer);
                    }
                    else
                        Disconnect("Not Connected.");
                }
                catch (SocketException se)
                {
                    Disconnect(string.Format("Failed to send packet... Error: {0}", se.SocketErrorCode.ToString()));
                }
            }
        }


        /// <summary>
        /// The callback from Send().
        /// </summary>
        /// <param name="asyncResult">The async result.</param>
    }




    public delegate bool ConnectionEvent(Client sClient);

    /// <summary>
    /// A delegate associated with socket-buffer events.
    /// </summary>
    public delegate bool BufferEvent(Client sClient, int Size, byte[] Packet);

    /// <summary>
    /// Events called through sockets.
    /// </summary>
    public class SocketEvents
    {
        /// <summary>
        /// An event raised when a client is connected.
        /// </summary>
        public ConnectionEvent OnConnection = new ConnectionEvent(empty_conn);

        /// <summary>
        /// Empty method for the connection events.
        /// </summary>
        /// <param name="sClient">The socket client.</param>
        /// <returns>Returns true always.</returns>
        static bool empty_conn(Client sClient) { return true; }


        /// <summary>
        /// An event raised when a client is disconnecting.
        /// </summary>
        public ConnectionEvent OnDisconnection = new ConnectionEvent(empty_conn);



        /// <summary>
        /// An event raised when a client has send data to the server.
        /// </summary>
        public BufferEvent OnReceive = new BufferEvent(empty_buff);

        /// <summary>
        /// Empty method for the buffer events.
        /// </summary>
        /// <param name="sClient">The socket client.</param>
        /// <param name="packet">The data packet.</param>
        /// <returns>Returns true always.</returns>
        static bool empty_buff(Client sClient, int Size, byte[] packet) { return true; }
    }
}

