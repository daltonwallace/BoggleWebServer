using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomNetworking;
using System.Net.Sockets;

namespace BoggleClientModel
{
    /// <summary>
    /// Model used to communicate with the Boggle Server
    /// </summary>
    public class Model
    {
        // The socket used to communicate with the server.  If no connection has been
        // made yet, this is null.
        private StringSocket socket;

        // Register for these events when a line of text arrives
        public event Action<String[]> StartLineEvent;
        public event Action<String> TimeLineEvent;
        public event Action<String[]> ScoreLineEvent;
        public event Action<String[]> StopLineEvent;
        public event Action<String> IgnoreLineEvent;
        public event Action<String> TerminateLineEvent;

        /// <summary>
        /// Creates a not-yet-connected client model.
        /// </summary>
        public Model()
        {
            socket = null;
        }

        /// <summary>
        /// Connect to the server at the given hostname and port and with the given player name.
        /// </summary>
        public void Connect(string hostname, int port, String name)
        {
            socket = null;

            if (socket == null)
            {
                TcpClient client = new TcpClient(hostname, port);
                socket = new StringSocket(client.Client, UTF8Encoding.Default);
                socket.BeginSend("play " + name + "\n", (e, p) => { }, null);
                socket.BeginReceive(LineReceived, null);
            }

            
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public void Disconnect()
        {
            if (socket != null)
            {
                socket.Close();
            }


        }

        /// <summary>
        /// Send a line of text to the server.
        /// </summary>
        /// <param name="line"></param>
        public void SendMessage(String line)
        {
            if (socket != null)
            {
                socket.BeginSend(line + "\n", (e, p) => { }, null);
            }
        }

        /// <summary>
        /// Deal with an arriving line of text.
        /// </summary>
        private void LineReceived(String s, Exception e, object p)
        {
            // If the string is null keep listening and return
            if (s == null)
            {
                if(socket != null)
                    socket.BeginReceive(LineReceived, null);

                return;
            }

            // Send the StartLineEvent
            if (StartLineEvent != null && s.StartsWith("START", true, null))
            {
                // Store BoggleBoard, Total Game Time and Opponent Name
                String[] startInfo = s.Substring(5).Trim().Split(' ');

                // Send to the client
                StartLineEvent(startInfo);
            }

            // Send the TimeLineEvent
            else if (TimeLineEvent != null && s.StartsWith("TIME", true, null))
            {
                // Send the number of seconds remaining
                TimeLineEvent(s.Substring(4).Trim());
            }

            // Send the ScoreLineEvent
            else if (ScoreLineEvent != null && s.StartsWith("SCORE", true, null))
            {
                // Store player score and opponent score
                String[] scoreInfo = s.Substring(5).Trim().Split(' ');

                // Send score information to player
                ScoreLineEvent(scoreInfo);
            }

            // Send the StopLineEvent
            else if (StopLineEvent != null && s.StartsWith("STOP", true, null))
            {
                // Store player score and opponent score
                String[] summaryInfo = s.Substring(4).Trim().Split(' ');

                // Send game summary to player
                StopLineEvent(summaryInfo);
            }

            // Send the IgnoreLineEvent
            else if (IgnoreLineEvent != null && s.StartsWith("IGNORING", true, null))
            {
                // Send the input that will be ignored to the player
                IgnoreLineEvent(s.Substring(8).Trim());
            }

            // Send the TerminatedLineEvent
            else if (TerminateLineEvent != null && s.StartsWith("TERMINATED", true, null))
            {
                // Send the terminated string to the player
                TerminateLineEvent(s.Trim());
            }

            // Continue listening
            socket.BeginReceive(LineReceived, null);
        }
    }
}
