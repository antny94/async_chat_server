using System.Net.Sockets;
using System.Net;
using System.Text;

namespace async_chat_server
{
    internal class Program
    {
        //private static List<Task> _clientListeners = new List<Task>();
        private static readonly ushort char_limit = 1024;
        private static byte[] _buffer = new byte[char_limit];
        private static List<Task> _clientListener = new List<Task>();
        private static List<Socket> _clientSockets = new List<Socket>();
        private static Queue<string> inboundMessages = new Queue<string>();
        private static Socket _serverSocket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp
            );

        static void Main(string[] args)
        {
            Console.Title = "Chat Server";
            InitializeServer();

        }

        static void InitializeServer()
        {
            //Router must port forward server's ip address to receive any data
            Console.WriteLine("Starting Server...");
            var ipEP = new IPEndPoint(IPAddress.Any, 4949);
            _serverSocket.Bind(ipEP);
            _serverSocket.Listen(5);

            Console.WriteLine("Waiting for connections...");
            try
            {
                List<Task> listTask = new List<Task>();
                listTask.Add(new Task(AsyncOutputMessages));
                listTask.Add(new Task(AcceptConnections));
                listTask.Add(new Task(DebugPrintConnections));

                //Start all tasks
                Parallel.ForEach(listTask, task => task.Start());

                //Wait for all tasks to be completed
                Task.WaitAll(listTask.ToArray());

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void AddClientListener(Socket s)
        {
            Task? newClientListener = null;

            newClientListener = new Task(() =>
                {
                    while (true)
                    {
                        try
                        {
                            byte[] temp = new byte[char_limit];
                            int a = s.Receive(temp);

                            if (temp[0] != 0)
                            {
                                string client_msg = Encoding.ASCII.GetString(temp);

                                //Output to server
                                inboundMessages.Enqueue(client_msg);

                                //Broadcast message to all clients, except the original
                                BroadcastMessage(s, temp);
                            }

                        }
                        catch (Exception E)
                        {
                            Console.WriteLine("!~~A client has disconnected~~!");

                            //Remove client listener
                            if (newClientListener != null && s != null)
                            {
                                _clientListener.Remove(newClientListener);
                                _clientSockets.Remove(s);
                            }

                            //Shutdown and close socket connection of client
                            if (s != null)
                            {
                                s.Shutdown(SocketShutdown.Both);
                                s.Close();
                            }

                            //Break from the while loop
                            break;
                        }
                    }
                }
            );
            _clientListener.Add(newClientListener);
            newClientListener.Start();
        }

        static void AsyncOutputMessages()
        {
            while (true)
            {
                while (inboundMessages.Count > 0)
                {
                    string temp = inboundMessages.Dequeue();
                    Console.WriteLine(temp);
                }
            }
        }

        static void DebugPrintConnections()
        {
            Thread.CurrentThread.Name = "DebugPC";
            while (true)
            {
                Thread.Sleep(5000);
                Console.WriteLine("Count of connected clients: " + _clientSockets.Count);
                Console.WriteLine("Count of connected listeners: " + _clientListener.Count);
            }
        }

        static async void AcceptConnections()
        {
            while (true)
            {
                //Waits for a new connection socket from client
                Socket newClientSocket = await _serverSocket.AcceptAsync();

                //When client connects, add them to the list of client sockets
                _clientSockets.Add(newClientSocket);

                //Establish a client listener to always listen for messages from a client socket
                AddClientListener(newClientSocket);
            }
        }

        static void BroadcastMessage(Socket original_client, byte[] msg)
        {
            // Broadcast the messages receive from a client to all the other clients connected
            foreach (Socket clientSocket in _clientSockets)
            {
                if (original_client != clientSocket)
                {
                    clientSocket.Send(msg);
                }
            }
        }
        
    }
}