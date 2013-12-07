using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BoggleClient;
using System.Windows.Forms;
using BoggleClientModel;
using CustomNetworking;
using BB;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BoggleClientTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestSummaryMessage()
        {
            BoggleClientController tester = new BoggleClientController();

            string[] testArray = new string[] { "2", "Dalt", "Brandon", "2", "Dalt", "Brandon", "2", "Dalt", "Brandon", "2", "Dalt", "Brandon", "2", "Dalt", "Brandon" };

            string summary = tester.SummaryMessage(testArray);

            Assert.AreEqual("Client played 2 words: Dalt Brandon \nOpponent played 2 words: Dalt Brandon \nBoth played 2 duplicate words: Dalt Brandon \nClient played 2 illegal words: Dalt Brandon \nOpponent played 2 illegal words: Dalt Brandon \n", summary);

        }

        /// <summary>
        /// Tests the entire model class.
        /// </summary>
        [TestMethod]
        public void TestModel()
        {

            BoggleServer server = new BoggleServer("5", ".../.../.../dictionary.txt", "ABCDEFGHIJKLMNOP");

            Model testModel = new Model();

            //Assert.AreEqual(null, testModel.getSocket());

            testModel.Connect("localhost", 2000, "Luke Skywalker");

            //bool validSocket = testModel.getSocket() != null;

            //Assert.AreEqual(true, validSocket);

            testModel.SendMessage("May the Force be with you.  Always");

            testModel.Disconnect();

            // Assert.AreEqual(null, testModel.getSocket());

            BoggleClientController player1 = new BoggleClientController();

            player1.startButton_Click(null, null);

            server.Stop();


        }

        /// <summary>
        /// Test to ensure that scoring an illegal word one time works correctly
        /// </summary>
        [TestMethod]
        public void TestIllegalScoreKeeping()
        {
            Thread.Sleep(1000);
            new Test1Class().run(2000);
        }

        public class Test1Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre3;
            private ManualResetEvent mre4;
            private ManualResetEvent mre5;
            private ManualResetEvent mre6;
            private String s3;
            private object p3;
            private String s4;
            private object p4;
            private String s5;
            private object p5;
            private String s6;
            private object p6;
            Model model = new Model();


            // Timeout used in test case
            private static int timeout = 20000;

            public void run(int port)
            {
                // Create and start a BoggleServer, this will be used for all subsequent tests
                BoggleServer server = new BoggleServer("5", ".../.../.../dictionary.txt", "ABCDEFGHIJKLMNOP");

                TcpClient player1 = null;
                TcpClient player2 = null;

                StringSocket player1SS = null;
                StringSocket player2SS = null;

                try
                {

                    // Add events for various incoming lines
                    model.StartLineEvent += StartReceived;
                    model.TimeLineEvent += TimeReceived;
                    model.ScoreLineEvent += ScoreReceived;
                    model.StopLineEvent += StopReceived;
                    model.IgnoreLineEvent += IgnoreReceived;
                    model.TerminateLineEvent += TerminateReceived;

                    // Create two new players
                    player1 = new TcpClient("localhost", port);
                    player2 = new TcpClient("localhost", port);

                    // Create the internal sockets
                    Socket player1Socket = player1.Client;
                    Socket player2Socket = player2.Client;

                    // Create the players String Sockets
                    player1SS = new StringSocket(player1Socket, new UTF8Encoding());
                    player2SS = new StringSocket(player2Socket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre3 = new ManualResetEvent(false);
                    mre4 = new ManualResetEvent(false);
                    mre5 = new ManualResetEvent(false);
                    mre6 = new ManualResetEvent(false);

                    // Make the receive requests
                    player1SS.BeginReceive(CompletedReceive3, player1SS);
                    player2SS.BeginReceive(CompletedReceive4, player2SS);
                    player1SS.BeginReceive(CompletedReceive5, player1SS);
                    player2SS.BeginReceive(CompletedReceive6, player2SS);

                    // Send some commands
                    player1SS.BeginSend("play dalton \n", (e, o) => { }, 1);
                    Thread.Sleep(1000);
                    player2SS.BeginSend("play brandon \n", (e, o) => { }, 1);
                    //Thread.Sleep(3000);
                    player1SS.BeginSend("word fakeword \n", (e, o) => { }, 1);

                    //// Make sure the lines were received properly.
                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 brandon", s3);


                    Assert.AreEqual(true, mre4.WaitOne(timeout), "Timed out waiting 4");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 dalton", s4);


                    Assert.AreEqual(true, mre5.WaitOne(timeout), "Timed out waiting 5");
                    Assert.AreEqual("SCORE -1 0", s5);


                    Assert.AreEqual(true, mre6.WaitOne(timeout), "Timed out waiting 6");
                    Assert.AreEqual("SCORE 0 -1", s6);

                }
                finally
                {
                    player1SS.Close();
                    player2SS.Close();
                    server.Stop();
                }
            }

            #region Receive Callbacks

            private void CompletedReceive3(String s, Exception o, object payload)
            {
                s3 = s;
                p3 = payload;
                mre3.Set();

                model.LineReceived(s, o, payload);
            }

            private void CompletedReceive4(String s, Exception o, object payload)
            {
                s4 = s;
                p4 = payload;
                mre4.Set();

                model.LineReceived(s, o, payload);
            }

            private void CompletedReceive5(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s5 = s;
                    p5 = payload;
                    mre5.Set();

                    model.LineReceived(s, o, payload);
                }
                // Keep listening
                else
                {
                    StringSocket player1SS = (StringSocket)payload;

                    player1SS.BeginReceive(CompletedReceive5, player1SS);
                }

            }

            private void CompletedReceive6(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s6 = s;
                    p6 = payload;
                    mre6.Set();

                    model.LineReceived(s, o, payload);
                }
                // Keep listening
                else
                {
                    StringSocket player2SS = (StringSocket)payload;

                    player2SS.BeginReceive(CompletedReceive6, player2SS);
                }
            }
            #endregion

            #region Various Line Received Methods

            /// <summary>
            /// Changes the status label for starting the game.
            /// </summary>
            /// <param name="startInfo"></param>
            private void StartReceived(string[] startInfo)
            {

            }

            /// <summary>
            /// Updates the time label during the game
            /// </summary>
            /// <param name="text"></param>
            private void TimeReceived(string currentTime)
            {

            }

            /// <summary>
            /// Updates the score during the game
            /// </summary>
            /// <param name="scores"></param>
            private void ScoreReceived(string[] scores)
            {

            }

            /// <summary>
            /// Presents the game summary to the user
            /// </summary>
            /// <param name="text"></param>
            private void StopReceived(string[] summaryInfo)
            {

            }

            /// <summary>
            /// Notify the user that we are ignoring the submission
            /// </summary>
            /// <param name="text"></param>
            private void IgnoreReceived(string ignoring)
            {

            }

            /// <summary>
            /// Notify the user that the game has ended prematurely
            /// </summary>
            /// <param name="text"></param>
            private void TerminateReceived(string ignoring)
            {

            }

            #endregion
        }

        /// <summary>
        /// Tests the IGNORE Before a game begins as well as testing the regular progression of a game
        /// ending with the summary
        /// </summary>
        [TestMethod]
        public void TestABetterWay()
        {
            new Test7Class().run(2000);
            new Test8Class().run(2000);
        }

        public class Test7Class
        {
            // Create and start a BoggleServer, this will be used for all subsequent tests
            BoggleServer server = new BoggleServer("5", ".../.../.../dictionary.txt", "ABCDEFGHIJKLMNOP");

            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;

            private String s1;
            private object p1;

            private String s2;
            private object p2;

            Model model = new Model();

            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {

                TcpClient player1 = null;
                TcpClient player2 = null;

                StringSocket player1SS = null;
                StringSocket player2SS = null;

                try
                {

                    // Add events for various incoming lines
                    model.StartLineEvent += StartReceived;
                    model.TimeLineEvent += TimeReceived;
                    model.ScoreLineEvent += ScoreReceived;
                    model.StopLineEvent += StopReceived;
                    model.IgnoreLineEvent += IgnoreReceived;
                    model.TerminateLineEvent += TerminateReceived;

                    // Create the two players
                    player1 = new TcpClient("localhost", port);
                    player2 = new TcpClient("localhost", port);

                    // Create the internal sockets
                    Socket player1Socket = player1.Client;
                    Socket player2Socket = player2.Client;

                    // Create the players String Sockets
                    player1SS = new StringSocket(player1Socket, new UTF8Encoding());
                    player2SS = new StringSocket(player2Socket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);

                    // Make the receive requests
                    player1SS.BeginReceive(CompletedReceive1, player1SS);
                    player2SS.BeginReceive(CompletedReceive2, player2SS);

                    // Send some commands
                    player1SS.BeginSend("gobbeldy gook \n", (e, o) => { }, 1);
                    player2SS.BeginSend("gobbeldy gook \n", (e, o) => { }, 1);


                    player1SS.BeginSend("play dalton \n", (e, o) => { }, 1);
                    player2SS.BeginSend("play brandon \n", (e, o) => { }, 1);
                    player1SS.BeginSend("word knife \n", (e, o) => { }, 1);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("IGNORING gobbeldy gook ", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("IGNORING gobbeldy gook ", s2);

                    #region Repeat This When Looking for the next Line
                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 brandon", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 dalton", s2);
                    #endregion

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("SCORE 2 0", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("SCORE 0 2", s2);

                    // Sleep away the rest of the game
                    Thread.Sleep(5000);

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("SCORE 2 0", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("SCORE 0 2", s2);

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("STOP 1 KNIFE 0 0 0 0 ", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("STOP 0 1 KNIFE 0 0 0 ", s2);
                }
                finally
                {
                    player1SS.Close();
                    player2SS.Close();
                    server.Stop();
                }
            }

            public void resetCallbacks(StringSocket player1SS, StringSocket player2SS)
            {
                mre1 = new ManualResetEvent(false);
                mre2 = new ManualResetEvent(false);
                player1SS.BeginReceive(CompletedReceive1, player1SS);
                player2SS.BeginReceive(CompletedReceive2, player2SS);
            }

            #region Receive Callbacks

            private void CompletedReceive1(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s1 = s;
                    p1 = payload;
                    mre1.Set();

                    model.LineReceived(s, o, payload);
                }
                // Keep listening
                else
                {
                    StringSocket player1SS = (StringSocket)payload;

                    player1SS.BeginReceive(CompletedReceive1, player1SS);
                }
            }

            private void CompletedReceive2(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s2 = s;
                    p2 = payload;
                    mre2.Set();

                    model.LineReceived(s, o, payload);
                }
                // Keep listening
                else
                {
                    StringSocket player2SS = (StringSocket)payload;

                    player2SS.BeginReceive(CompletedReceive2, player2SS);
                }
            }

            #endregion

            #region Various Line Received Methods

            /// <summary>
            /// Changes the status label for starting the game.
            /// </summary>
            /// <param name="startInfo"></param>
            private void StartReceived(string[] startInfo)
            {

            }

            /// <summary>
            /// Updates the time label during the game
            /// </summary>
            /// <param name="text"></param>
            private void TimeReceived(string currentTime)
            {

            }

            /// <summary>
            /// Updates the score during the game
            /// </summary>
            /// <param name="scores"></param>
            private void ScoreReceived(string[] scores)
            {

            }

            /// <summary>
            /// Presents the game summary to the user
            /// </summary>
            /// <param name="text"></param>
            private void StopReceived(string[] summaryInfo)
            {

            }

            /// <summary>
            /// Notify the user that we are ignoring the submission
            /// </summary>
            /// <param name="text"></param>
            private void IgnoreReceived(string ignoring)
            {

            }

            /// <summary>
            /// Notify the user that the game has ended prematurely
            /// </summary>
            /// <param name="text"></param>
            private void TerminateReceived(string ignoring)
            {

            }

            #endregion
        }

        public class Test8Class
        {
            // Create and start a BoggleServer, this will be used for all subsequent tests
            BoggleServer server = new BoggleServer("5", ".../.../.../dictionary.txt", "SERSPATGLINESERS");

            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;

            private String s1;
            private object p1;

            private String s2;
            private object p2;

            Model model = new Model();

            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {

                TcpClient player1 = null;
                TcpClient player2 = null;

                StringSocket player1SS = null;
                StringSocket player2SS = null;

                try
                {

                    // Add events for various incoming lines
                    model.StartLineEvent += StartReceived;
                    model.TimeLineEvent += TimeReceived;
                    model.ScoreLineEvent += ScoreReceived;
                    model.StopLineEvent += StopReceived;
                    model.IgnoreLineEvent += IgnoreReceived;
                    model.TerminateLineEvent += TerminateReceived;

                    // Create the two players
                    player1 = new TcpClient("localhost", port);
                    player2 = new TcpClient("localhost", port);

                    // Create the internal sockets
                    Socket player1Socket = player1.Client;
                    Socket player2Socket = player2.Client;

                    // Create the players String Sockets
                    player1SS = new StringSocket(player1Socket, new UTF8Encoding());
                    player2SS = new StringSocket(player2Socket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);

                    // Make the receive requests
                    player1SS.BeginReceive(CompletedReceive1, player1SS);
                    player2SS.BeginReceive(CompletedReceive2, player2SS);

                    // Send some commands
                    player1SS.BeginSend("gobbeldy gook \n", (e, o) => { }, 1);
                    player2SS.BeginSend("gobbeldy gook \n", (e, o) => { }, 1);


                    player1SS.BeginSend("play dalton \n", (e, o) => { }, 1);
                    player2SS.BeginSend("play brandon \n", (e, o) => { }, 1);
                    player1SS.BeginSend("word tapers \n", (e, o) => { }, 1);
                    player1SS.BeginSend("word taper \n", (e, o) => { }, 1);
                    player1SS.BeginSend("word planers \n", (e, o) => { }, 1);
                    player1SS.BeginSend("word splinters \n", (e, o) => { }, 1);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("IGNORING gobbeldy gook ", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("IGNORING gobbeldy gook ", s2);

                    #region Repeat This When Looking for the next Line
                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("START SERSPATGLINESERS 5 brandon", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("START SERSPATGLINESERS 5 dalton", s2);
                    #endregion

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("SCORE 3 0", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("SCORE 0 3", s2);

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("SCORE 5 0", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("SCORE 0 5", s2);

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("SCORE 10 0", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("SCORE 0 10", s2);

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("SCORE 21 0", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("SCORE 0 21", s2);

                    // Sleep away the rest of the game
                    Thread.Sleep(10000);

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("SCORE 21 0", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("SCORE 0 21", s2);

                    // Get ready for another round of assertions
                    resetCallbacks(player1SS, player2SS);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("STOP 4 TAPERS TAPER PLANERS SPLINTERS 0 0 0 0 ", s1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("STOP 0 4 TAPERS TAPER PLANERS SPLINTERS 0 0 0 ", s2);
                }
                finally
                {
                    player1SS.Close();
                    player2SS.Close();
                    server.Stop();
                }
            }

            public void resetCallbacks(StringSocket player1SS, StringSocket player2SS)
            {
                mre1 = new ManualResetEvent(false);
                mre2 = new ManualResetEvent(false);
                player1SS.BeginReceive(CompletedReceive1, player1SS);
                player2SS.BeginReceive(CompletedReceive2, player2SS);
            }

            #region Receive Callbacks

            private void CompletedReceive1(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s1 = s;
                    p1 = payload;
                    mre1.Set();

                    model.LineReceived(s, o, payload);
                }
                // Keep listening
                else
                {
                    StringSocket player1SS = (StringSocket)payload;

                    player1SS.BeginReceive(CompletedReceive1, player1SS);
                }
            }

            private void CompletedReceive2(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s2 = s;
                    p2 = payload;
                    mre2.Set();

                    model.LineReceived(s, o, payload);
                }
                // Keep listening
                else
                {
                    StringSocket player2SS = (StringSocket)payload;

                    player2SS.BeginReceive(CompletedReceive2, player2SS);
                }
            }

            #endregion

            #region Various Line Received Methods

            /// <summary>
            /// Changes the status label for starting the game.
            /// </summary>
            /// <param name="startInfo"></param>
            private void StartReceived(string[] startInfo)
            {

            }

            /// <summary>
            /// Updates the time label during the game
            /// </summary>
            /// <param name="text"></param>
            private void TimeReceived(string currentTime)
            {

            }

            /// <summary>
            /// Updates the score during the game
            /// </summary>
            /// <param name="scores"></param>
            private void ScoreReceived(string[] scores)
            {

            }

            /// <summary>
            /// Presents the game summary to the user
            /// </summary>
            /// <param name="text"></param>
            private void StopReceived(string[] summaryInfo)
            {

            }

            /// <summary>
            /// Notify the user that we are ignoring the submission
            /// </summary>
            /// <param name="text"></param>
            private void IgnoreReceived(string ignoring)
            {

            }

            /// <summary>
            /// Notify the user that the game has ended prematurely
            /// </summary>
            /// <param name="text"></param>
            private void TerminateReceived(string ignoring)
            {

            }

            #endregion
        }

   }
}