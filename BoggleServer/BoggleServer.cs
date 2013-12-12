using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using BB;
using CustomNetworking;
using System.Threading;
using System.Timers;
using MySql.Data.MySqlClient;
using System.Data;

namespace BB
{
    /// <summary>
    /// 
    /// </summary>
    public class BoggleServer
    {
        #region Globals

        // Listens for incoming connectoin requests
        private TcpListener boggleServer;
        private TcpListener webServer;

        // Encoding used for incoming/outgoing data
        private static System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();

        // Queue for holding latest two String Sockets that come in
        Queue<Player> playerQueue;

        // Locks the player queue
        private readonly Object playerQueueLock;

        // Used to track game data
        private int gameTime;
        private static HashSet<string> dictionaryFile;
        private string optionalBoggleBoard;
        private bool gameStarted;
        Player waitingPlayer;

        // Database access
        private const string connectionString = "server=atr.eng.utah.edu;database=bkoch;uid=bkoch;password=510368829";

        #endregion

        #region Pre-Game Warmup

        /// <summary>
        /// Receives argument from user.  Requires 2 - 3 arguments to begin boggle game,
        /// otherwise it will throw an exception.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            args = new String[] { "45", "C:/Users/Dalton/Desktop/School/CS 3500/Assignments/PS8Git/dictionary.txt", "" };

            // Check to see that the appropriate number of arguments has been passed
            // to the server via the string[] args
            if (args.Length == 2)
            {
                // If file path is not valid, throw exception.
                if (!System.IO.File.Exists(args[1]))
                    throw new Exception("Invalid dictionary file path.");

                new BoggleServer(args[0], args[1], "");

                // Keep the main thread active so we can see output to the console
                Console.ReadLine();
            }
            else if (args.Length == 3)
            {

                // If file path is not valid, throw exception.
                if (!System.IO.File.Exists(args[1]))
                    throw new Exception("Invalid dictionary file path.");

                new BoggleServer(args[0], args[1], args[2]);

                // Keep the main thread active so we can see output to the console
                Console.ReadLine();
            }
            else
                throw new Exception("Invalid number of arguments");
        }

        /// <summary>
        /// Constructor that creates a BoggleServer that listens for connection requests on port 2000
        /// </summary>
        public BoggleServer(string gameLength, string dictionaryFilePath, string customBoard)
        {
            // Assign to global variables:
            int.TryParse(gameLength, out gameTime);
            dictionaryFile = new HashSet<string>();
            gameStarted = false;

            // Attempt to create dictionary from file
            try
            {
                // Create a dictionary set of words based on the filepath of the dictionary.
                string[] lines = System.IO.File.ReadAllLines(dictionaryFilePath);
                foreach (string line in lines)
                {
                    dictionaryFile.Add(line);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Issues parsing dictionary file");
            }

            // Check to see if the optionalBoard exists and if it is 16 characters
            if (customBoard.Length == 16)
            {
                this.optionalBoggleBoard = customBoard;
            }
            else
            {
                this.optionalBoggleBoard = "";
            }

            // Instantiate variables
            playerQueue = new Queue<Player>();
            playerQueueLock = new Object();

            // A TcpListener listening for any incoming connections
            boggleServer = new TcpListener(IPAddress.Any, 2000);
            // Start the TcpListener
            boggleServer.Start();

            // A TcpListener listening for any incoming connections
            webServer = new TcpListener(IPAddress.Any, 2500);
            // Start the TcpListener
            webServer.Start();

            // Ask our new boggle server to call a specific method once a connection arrives
            // the waiting and calling will happen on another thread.  This call will return immediately
            // and the constructor will return to main
            boggleServer.BeginAcceptSocket(ConnectionRequested, null);

            // Ask our new boggle server to call a specific method once a connection arrives
            // the waiting and calling will happen on another thread.  This call will return immediately
            // and the constructor will return to main
            webServer.BeginAcceptSocket(WebConnectionRequested, null);
        }

        /// <summary>
        /// This method is called when webServer.BeginAcceptSocket receives an incoming connection
        /// to the server
        /// </summary>
        /// <param name="result"></param>
        public void WebConnectionRequested(IAsyncResult result)
        {
            // Obtain the socket corresponding to the incoming request.
            Socket s = webServer.EndAcceptSocket(result);

            // Should probably use a StringSocket here...
            StringSocket ss = new StringSocket(s, encoding);

            // Start listening to that player
            ss.BeginReceive(WebRequestRetreived, ss);

            // Start listening for more incoming connections
            webServer.BeginAcceptSocket(WebConnectionRequested, null);
        }

        /// <summary>
        /// This method should enqueue a player in our player pool if they have
        /// requested to play with a player name and place them on a new thread
        /// with a game if there exists a second player on the queue.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        /// <param name="payload"></param>
        private void WebRequestRetreived(string s, Exception e, object payload)
        {
            if (s == null || s == "GET /favicon.ico HTTP/1.1\r")
            {
                return;
            }

            // Cast string socket
            StringSocket webSocket = (StringSocket)payload;

            String pageHeader = "HTTP/1.1 200 OK \r\n Connection: close \r\n Content-Type: text/html; charset=UTF-8\r\n";

            // Send the page header
            webSocket.BeginSend(pageHeader, (exc1, o) => { }, webSocket);

            // Send a blank line consisting only of "\r\n"
            webSocket.BeginSend("\r\n", (exc1, o) => { }, webSocket);

            // Remove the \r from the string
            s = s.Trim();

            // We get a favicon.ico request as well...
            // Determine GET request
            // Return appropriate HTML with data from database
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    StringBuilder html = new StringBuilder();

                    #region Player Stats Html

                    if (s == "GET /players HTTP/1.1")
                    {
                        // The server should send back an HTML page containing a table of information.  There should
                        // be one row for each player in the database and four columns.  Each row should consist of 
                        // the player's name, the number of games won by the player, the number of games lost by the
                        // player, and the number of games tied by the player.

                        //webSocket.BeginSend("<h1>Test 1 </h1>", (exc1, o) => { webSocket.Close(); }, webSocket);

                        try
                        {
                            conn.Open();

                            string stm = "SELECT Player.PlayerName, Player.Wins, Player.Losses, Player.Ties FROM Player";
                            MySqlDataAdapter da = new MySqlDataAdapter(stm, conn);

                            DataSet ds = new DataSet();

                            da.Fill(ds, "PlayerStats");
                            DataTable dt = ds.Tables["PlayerStats"];

                            dt.WriteXml("PlayerStats.xml");

                            html.Append("<table border = '1'>");

                            html.Append("<tr> <td>Player Name</td> <td>Wins</td> <td>Losses</td> <td>Ties</td> </tr>");

                            foreach (DataRow row in dt.Rows)
                            {
                                html.Append("<tr>");

                                foreach (DataColumn col in dt.Columns)
                                {
                                    html.Append("<td>" + row[col] + "</td>");
                                }

                                html.Append("</tr>");
                            }

                            html.Append("</table>");

                            webSocket.BeginSend(html.ToString(), (exc1, o) => { webSocket.Close(); }, webSocket);

                        }
                        catch (MySqlException ex)
                        {
                            Console.WriteLine("Error Homie!");

                        }
                    }

                    #endregion

                    #region Game Stats By Player Html

                    else if (s.StartsWith("GET /games?player=") && s.EndsWith("HTTP/1.1"))
                    {
                        // The server should send back an HTML page containing a table of information.  There should
                        // be one row for each game played by the player named in the line of text and six columns.
                        // Each row should consist of a number that uniquely identifies the game, the date and time
                        // when the game was played, the name of the opponent, the score for the named player, and
                        // the named player, and the score for the opponent.

                        // Get the name of the requested player
                        String[] name = s.Substring(18).Split(' ');

                        #region Requested Player is Player One in Database
                        try
                        {
                            conn.Open();

                            // Build the query
                            string stm = "SELECT Game.GameID, Game.GameDate, Game.Player1Name, Game.Player1Score, Game.Player2Name, Game.Player2Score FROM Game WHERE (Game.Player1Name = '" + name[0] + "')";

                            MySqlDataAdapter da = new MySqlDataAdapter(stm, conn);

                            DataSet ds = new DataSet();

                            da.Fill(ds, "GameStats");
                            DataTable dt = ds.Tables["GameStats"];

                            dt.WriteXml("GameStats.xml");

                            // Build an HTML table with the following column headers:
                            html.Append("<table border = '1'>");

                            html.Append("<tr> <td>Game ID</td> <td>Date</td> <td>Player Name</td> <td>Player Score</td> <td>Opponent Name</td> <td>Opponent Score</td> </tr>");

                            foreach (DataRow row in dt.Rows)
                            {
                                html.Append("<tr>");

                                foreach (DataColumn col in dt.Columns)
                                {
                                    html.Append("<td>" + row[col] + "</td>");
                                }

                                html.Append("</tr>");
                            }

                        }
                        catch (MySqlException ex)
                        {
                            Console.WriteLine("Error Homie!");

                        }
                        #endregion

                        #region Requested Player is Player Two in Database
                        try
                        {
                            // Requery the database with the requested player as player two.
                            string stm = "SELECT Game.GameID, Game.GameDate, Game.Player2Name, Game.Player2Score, Game.Player1Name, Game.Player1Score FROM Game WHERE (Game.Player2Name = '" + name[0] + "')";
                            MySqlDataAdapter da = new MySqlDataAdapter(stm, conn);

                            DataSet ds = new DataSet();

                            da.Fill(ds, "PlayerStats");
                            DataTable dt = ds.Tables["PlayerStats"];

                            dt.WriteXml("PlayerStats.xml");

                            // Add all stats to the previously built html table
                            foreach (DataRow row in dt.Rows)
                            {
                                html.Append("<tr>");

                                foreach (DataColumn col in dt.Columns)
                                {
                                    html.Append("<td>" + row[col] + "</td>");
                                }

                                html.Append("</tr>");
                            }

                            // Close the table
                            html.Append("</table>");

                            // Send the web page
                            webSocket.BeginSend(html.ToString(), (exc1, o) => { webSocket.Close(); }, webSocket);

                        }
                        catch (MySqlException ex)
                        {
                            Console.WriteLine("Error Homie!");

                        }
                        #endregion
                    }

                    #endregion

                    #region Game Stats By Game ID Html

                    else if (s.StartsWith("GET /game?id=") && s.EndsWith("HTTP/1.1"))
                    {
                        // The server should send back an HTML page containing information about the specified game 
                        // The page should contain the names and scores of the two players involved, the date and
                        // and time when the game was played, a 4x4 table containing the Boggle board that was used,
                        // the time limit that was used for the game, and the five-part word summary.

                        string[] game = s.Substring(13).Split(' ');

                        #region Create Boggle Board
                        try
                        {
                            conn.Open();

                            // Build the query
                            string stm = "SELECT Game.Board FROM Game WHERE (Game.GameID = " + game[0] + ")";

                            MySqlDataAdapter da = new MySqlDataAdapter(stm, conn);

                            DataSet ds = new DataSet();

                            da.Fill(ds, "GameStats");
                            DataTable dt = ds.Tables["GameStats"];

                            dt.WriteXml("GameStats.xml");

                            // Build an HTML table with the following column headers:
                            html.Append("<table border = '1'>");
                            html.Append("<tr colspan = '4'> Boggle Board </tr>");

                            string bb = "";

                            foreach (DataRow row in dt.Rows)
                            {
                                foreach (DataColumn col in dt.Columns)
                                {
                                    bb = (string)row[col];
                                } 
                            }

                            html.Append("<tr>");
                            int count = 0;

                            foreach (char letter in bb)
                            {
                                if (count % 4 == 0)
                                {
                                    if (count != 15)
                                        html.Append("</tr><tr>");
                                    else
                                        html.Append("</tr>");
                                }

                                html.Append("<td>" + letter + "</td>");

                                count++;
                            }

                            html.Append("</br>");
                        }
                        catch (MySqlException ex)
                        {
                            Console.WriteLine("Error Homie!");

                        }
                        #endregion

                        #region Create Word Summary

                        try
                        {
                            Player player1 = new Player();
                            Player player2 = new Player();

                            // Requery the database with the requested player as player two.
                            string stm = "SELECT Player.PlayerName, Words.Word, Words.GameID, Words.Type FROM Words WHERE (Game.GameID = " + game[0] + ")";
                            MySqlDataAdapter da = new MySqlDataAdapter(stm, conn);

                            DataSet ds = new DataSet();

                            da.Fill(ds, "WordStats");
                            DataTable dt = ds.Tables["WordStats"];

                            dt.WriteXml("WordStats.xml");

                            bool firstTime = true;

                            // Add all stats to the previously built html table
                            foreach (DataRow row in dt.Rows)
                            {

                                foreach (DataColumn col in dt.Columns)
                                {
                                    // Name the players
                                    if(firstTime)
                                    {
                                        if(player1.Name == null)
                                            player1.Name = (string)row[col];
                                        else if(player1.Name != (string)row[col] && player2.Name != null)
                                            player2.Name = (string)row[col];
                                    }
                                    else

                                }

                                firstTime = true;
                            }

                            // Close the table
                            html.Append("</table>");

                            // Send the web page
                            webSocket.BeginSend(html.ToString(), (exc1, o) => { webSocket.Close(); }, webSocket);

                        }
                        catch (MySqlException ex)
                        {
                            Console.WriteLine("Error Homie!");

                        }
                        #endregion

                        webSocket.BeginSend(html.ToString(), (exc1, o) => { webSocket.Close(); }, webSocket);
                    }

                    #endregion

                    #region Error Html

                    // Any other message
                    else
                    {
                        // Send back an HTML page containing an error message

                        webSocket.BeginSend("<h1>Test 4 </h1>", (exc1, o) => { webSocket.Close(); }, webSocket);
                    }

                    #endregion
                }
                catch(Exception exc)
                {
                }
            }
        }

        /// <summary>
        /// This method is called when boggleServer.BeginAcceptSocket receives an incoming connection
        /// to the server
        /// </summary>
        /// <param name="result"></param>
        public void ConnectionRequested(IAsyncResult result)
        {
            // Obtain the socket corresponding to the incoming request.
            Socket s = boggleServer.EndAcceptSocket(result);

            // Should probably use a StringSocket here...
            StringSocket ss = new StringSocket(s, encoding);

            // Start listening to that player
            ss.BeginReceive(messageRetreived, ss);

            // Start listening for more incoming connections
            boggleServer.BeginAcceptSocket(ConnectionRequested, null);
        }

        /// <summary>
        /// This method should enqueue a player in our player pool if they have
        /// requested to play with a player name and place them on a new thread
        /// with a game if there exists a second player on the queue.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        /// <param name="payload"></param>
        private void messageRetreived(string s, Exception e, object payload)
        {
            // Cast the payload as a String Socket
            StringSocket currentSS = (StringSocket)payload;

            // Check if they asked to play
            if (s.StartsWith("play ", true, null))
            {
                Console.WriteLine(s);

                // Create a new player with the name and stringsocket
                Player tempPlayer = new Player(currentSS, s.Substring(4).Trim());

                lock (playerQueueLock)
                {
                    // Now that the player is ready, place them in the queue             
                    playerQueue.Enqueue(tempPlayer);

                    // Special callback if the player queue is of size 1
                    if (playerQueue.Count == 1)
                    {
                        // Set waiting player
                        waitingPlayer = tempPlayer;

                        // BeginReceive with special callback
                        currentSS.BeginReceive(waitingPlayerMessageReceived, waitingPlayer);
                    }
                }
            }
            // If we didn't receive a play command then keep listening for additional commands
            else
            {
                // The client has deviated from the protocol - send IGNORING message.
                currentSS.BeginSend("IGNORING " + s + "\n", (exc, o) => { }, 2);

                currentSS.BeginReceive(messageRetreived, currentSS);
            }

            // if there are two people wanting to play a game one with another
            // via the boggle server then pair them up, pass both String Sockets to a method
            // and begin a game of boggle!
            while (playerQueue.Count >= 2)
            {
                // Dequeue both StringSockets and get that game goin!
                lock (playerQueueLock)
                {
                    Player p1 = playerQueue.Dequeue();
                    Player p2 = playerQueue.Dequeue();

                    // Create a new game
                    Game g = new Game(p1, p2, optionalBoggleBoard, this.gameTime);

                    // Assign the game to both players
                    p1.Game = g;
                    p2.Game = g;

                    // Begin a new game on a new thread
                    Thread t = new Thread(new ThreadStart(g.GameOn));
                    t.Start();
                }
            }
        }

        /// <summary>
        /// Special callback to handle if the waiting player disconnects before a game starts
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        /// <param name="payload"></param>
        private void waitingPlayerMessageReceived(string s, Exception e, object payload)
        {
            // If someone removes themself from a the server
            // before a game has started
            if (s == null && e == null)
            {
                lock (playerQueueLock)
                {
                    if (playerQueue.Count > 0)
                    {
                        playerQueue.Dequeue();
                        Console.WriteLine("Player dequeud");
                    }
                }
                return;
            }
            else
            {
                waitingPlayer.Game.gameMessageReceived(s, e, payload);
            }
        }

        #endregion

        #region Game Time

        /// <summary>
        ///  This encapsulates two players and a method to begin the game
        /// </summary>
        private class Game
        {
            // Globals
            Player playerOne;
            Player playerTwo;
            string optionalBoggleBoard;
            BoggleBoard b;
            int gameTime;
            System.Timers.Timer timer;
            int timeCount;
            bool gameOver;

            /// <summary>
            /// Starts a new game, keeping track of two players and their scores as well as the time limit for the game
            /// </summary>
            /// <param name="player1"></param>
            /// <param name="player2"></param>
            /// <param name="_optionalBoggleBoard"></param>
            /// <param name="_gameTime"></param>
            public Game(Player _playerOne, Player _playerTwo, string _optionalBoggleBoard, int _gameTime)
            {
                playerOne = _playerOne;
                playerTwo = _playerTwo;
                optionalBoggleBoard = _optionalBoggleBoard;
                gameTime = _gameTime;
                timer = new System.Timers.Timer(1000);
                timeCount = _gameTime;
                gameOver = false;
            }

            /// <summary>
            /// Controls a game of boggle between two players
            /// </summary>
            /// <param name="player1"></param>
            /// <param name="player2"></param>
            public void GameOn()
            {
                // If the server has a custom board then be sure to use it
                if (optionalBoggleBoard == String.Empty)
                {
                    b = new BoggleBoard();
                }
                else
                {
                    b = new BoggleBoard(optionalBoggleBoard);
                }

                // Notify players that a game is about to begin
                //playerOne.StringSocket.BeginSend("A Game is afoot!\r\n", (e, o) => { }, 2);
                //playerTwo.StringSocket.BeginSend("A Game is afoot!\r\n", (e, o) => { }, 2);

                // Send the starting information
                playerOne.StringSocket.BeginSend("START " + b.ToString() + " " + gameTime + " " + playerTwo.Name + "\n", (e, o) => { }, 2);
                playerTwo.StringSocket.BeginSend("START " + b.ToString() + " " + gameTime + " " + playerOne.Name + "\n", (e, o) => { }, 2);

                // Will invoke timeElapse every 1000 milliseconds
                timer.Elapsed += timeElapsed;
                timer.Start();

                // Start listening for inputs
                playerOne.StringSocket.BeginReceive(gameMessageReceived, playerOne);
                playerTwo.StringSocket.BeginReceive(gameMessageReceived, playerTwo);


            }

            /// <summary>
            /// Handles all messages received during the game
            /// </summary>
            /// <param name="s"></param>
            /// <param name="e"></param>
            /// <param name="payload"></param>
            public void gameMessageReceived(string s, Exception e, object payload)
            {

                // Writes player input to server console
                Console.WriteLine(s);

                Player readyPlayer = (Player)payload;

                // Assume the opponent is playerOne
                Player opponent = playerOne;

                // If the readyPlayer is player one, change the opponent to playerTwo.
                if (readyPlayer.Equals(playerOne))
                {
                    opponent = playerTwo;
                }

                // If the player left
                if (s == null && !gameOver)
                {
                    // The server should send the command "TERMINATED" to the opponent
                    opponent.StringSocket.BeginSend("TERMINATED" + "\n", (exc, o) => { opponent.StringSocket.Close(); }, 2);

                    // Exit
                    return;
                }
                else if (s == null)
                {
                    // We have reached the end of the game and must close the other socket
                    opponent.StringSocket.Close();

                    return;
                }

                // Check if the cmd line input was valid
                if (s.StartsWith("word ", true, null))
                {
                    string word = s.Substring(4).Trim().ToUpper();

                    bool isLegal = (dictionaryFile.Contains(word) && b.CanBeFormed(word));

                    // if > 2 letters
                    if (word.Length > 2)
                    {
                        // Legal - CanBeFormed and in Dictionary
                        if (b.CanBeFormed(word) && dictionaryFile.Contains(word))
                        {

                            // Is it not in duplicates list and in our legal words list?
                            if (!readyPlayer.DuplicateWords.Contains(word) && !readyPlayer.LegalWords.Contains(word))
                            {

                                // Is it a duplicate?
                                if (opponent.LegalWords.Contains(word))
                                {

                                    // Add to duplicate, 
                                    readyPlayer.DuplicateWords.Add(word);
                                    opponent.DuplicateWords.Add(word);

                                    //Subtract from opponent list and score
                                    opponent.LegalWords.Remove(word);
                                    opponent.Score += SubScore(word);

                                    // Display score to the user
                                    readyPlayer.StringSocket.BeginSend("SCORE " + readyPlayer.Score + " " + opponent.Score + "\n", (exc, o) => { }, 2);
                                    opponent.StringSocket.BeginSend("SCORE " + opponent.Score + " " + readyPlayer.Score + "\n", (exc, o) => { }, 2);
                                }
                                // Otherwise we can add it to the readyPlayer's score.
                                else
                                {
                                    // Add the word to the player's legal word list and their score
                                    readyPlayer.LegalWords.Add(word);
                                    readyPlayer.Score += AddScore(word);

                                    // Display score to the user
                                    readyPlayer.StringSocket.BeginSend("SCORE " + readyPlayer.Score + " " + opponent.Score + "\n", (exc, o) => { }, 2);
                                    opponent.StringSocket.BeginSend("SCORE " + opponent.Score + " " + readyPlayer.Score + "\n", (exc, o) => { }, 2);
                                }
                            }

                            // Do nothing
                        }
                        // Illegal
                        else
                        {
                            // Subtract points if not already in illegal words list
                            if (!readyPlayer.IllegalWords.Contains(word))
                            {
                                // Add to player's illegal word list and subtract from score
                                readyPlayer.IllegalWords.Add(word);
                                readyPlayer.Score -= 1;

                                // Display score to the user
                                readyPlayer.StringSocket.BeginSend("SCORE " + readyPlayer.Score + " " + opponent.Score + "\n", (exc, o) => { }, 2);
                                opponent.StringSocket.BeginSend("SCORE " + opponent.Score + " " + readyPlayer.Score + "\n", (exc, o) => { }, 2);
                            }
                        }
                    }
                }
                else
                {
                    // The client has deviated from the protocol - send IGNORING message.
                    readyPlayer.StringSocket.BeginSend("IGNORING " + s + "\n", (exc, o) => { }, 2);
                }

                // Regardless, we need to keep listening to the player
                readyPlayer.StringSocket.BeginReceive(gameMessageReceived, readyPlayer);
            }

            #region Game Helper Methods

            /// <summary>
            /// Returns a positive value for a legal word.
            /// </summary>
            /// <param name="state"></param>
            private int AddScore(string word)
            {
                // Each remaining legal word earns a score that depends on its length.  Three and four-letter words are worth
                // one point, five-letter words are worth two points, six-letter words are worth three points, seven-letter words
                // are worth five points, and longer word are worth 11 points.
                if (word.Length == 3 || word.Length == 4)
                    return 1;
                else if (word.Length == 5)
                    return 2;
                else if (word.Length == 6)
                    return 3;
                else if (word.Length == 7)
                    return 5;
                else
                    return 11;
            }

            /// <summary>
            /// Returns a negative value for an illegal word.
            /// </summary>
            /// <param name="state"></param>
            private int SubScore(string word)
            {
                // Subtract points from the player due to duplicates.
                if (word.Length == 3 || word.Length == 4)
                    return -1;
                else if (word.Length == 5)
                    return -2;
                else if (word.Length == 6)
                    return -3;
                else if (word.Length == 7)
                    return -5;
                else
                    return -11;
            }

            /// <summary>
            /// Timer invoked method every 1000 milliseconds
            /// </summary>
            /// <param name="state"></param>
            private void timeElapsed(object sender, ElapsedEventArgs e)
            {
                // Advance the timer one second
                timeCount = timeCount - 1;

                // Notify the players a second has passed
                playerOne.StringSocket.BeginSend("TIME " + (timeCount) + "\n", (exc, o) => { }, 2);
                playerTwo.StringSocket.BeginSend("TIME " + (timeCount) + "\n", (exc, o) => { }, 2);

                // If we have run out of time then end the game
                if (timeCount == 0)
                {
                    timer.Stop();

                    // Display final scores to the users
                    playerOne.StringSocket.BeginSend("SCORE " + playerOne.Score + " " + playerTwo.Score + "\n", (exc, o) => { }, 2);
                    playerTwo.StringSocket.BeginSend("SCORE " + playerTwo.Score + " " + playerOne.Score + "\n", (exc, o) => { }, 2);

                    // Send the players their final scores and then print the summaries
                    playerOneSummary();
                    playerTwoSummary();

                    // Populate the database with all of the totally rad statistics. WORD.
                    populateDatabase();
                }
            }

            /// <summary>
            /// Ends the game for two players by printing all stats and closing the both sockets.
            /// </summary>
            private void playerOneSummary()
            {
                gameOver = true;

                // Prints the stats for player One and when it has completed proceeds to 
                playerOne.StringSocket.BeginSend("STOP " + printStats(playerOne, playerOne, playerOne.LegalWords) + printStats(playerOne, playerTwo, playerTwo.LegalWords) + printStats(playerOne, playerOne, playerOne.DuplicateWords) + printStats(playerOne, playerOne, playerOne.IllegalWords) + printStats(playerOne, playerTwo, playerTwo.IllegalWords) + "\n", (exc, o) => { }, 2);
            }

            /// <summary>
            /// Prints the stats for the second player and then closes the string sockets
            /// </summary>
            private void playerTwoSummary()
            {
                playerTwo.StringSocket.BeginSend("STOP " + printStats(playerTwo, playerTwo, playerTwo.LegalWords) + printStats(playerTwo, playerOne, playerOne.LegalWords) + printStats(playerTwo, playerTwo, playerTwo.DuplicateWords) + printStats(playerTwo, playerTwo, playerTwo.IllegalWords) + printStats(playerTwo, playerOne, playerOne.IllegalWords) + "\n", (exc, o) => { playerTwo.StringSocket.Close(); }, 2);
            }

            /// <summary>
            /// Prints the count and enumerates all words within a given collection and sends it to the client of the indicated player.
            /// </summary>
            /// <param name="sendPlayer"></param>
            /// <param name="collectionPlayer"></param>
            /// <param name="collection"></param>
            private string printStats(Player sendPlayer, Player collectionPlayer, HashSet<string> collection)
            {
                StringBuilder temp = new StringBuilder();

                temp.Append(collection.Count + " ");

                foreach (string word in collection)
                {
                    temp.Append(word + " ");
                }

                return temp.ToString();
            }

            /// <summary>
            /// Populate database for both players.  Utilize after the game has ended.
            /// </summary>
            private void populateDatabase()
            {
                // Connect to the DB
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    try
                    {
                        // Check for the player name in the player database
                        MySqlDataReader reader;
                        bool playerOneExists = false;
                        bool playerTwoExists = false;
                        int playerOneID = 0;
                        int playerTwoID = 0;
                        int gameCount = 0;
                        int gameID = 0;
                        int win = -1;

                        #region Add Players 

                        #region Game Win determination
                        // Determine who won the game
                       if (playerOne.Score > playerTwo.Score) // Player One won
                           win = 1;
                       else if (playerTwo.Score > playerOne.Score) // Player Two won
                           win = 2;
                       else // Tie
                           win = 0;

                        #endregion

                       conn.Open();
                       MySqlCommand cmd = conn.CreateCommand();

                        #region Player One Exists
                        cmd.CommandText = "select PlayerID, PlayerName, Wins, Losses, Ties from Player where Player.PlayerName = '" + playerOne.Name + "'";
                        using (reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // We now know the player exists in the table
                                // We do not need to add them
                                playerOneExists = true;
                                playerOneID = (int)reader["PlayerID"];

                                #region Add Player One's Win/Loss/Tie to Database
                                // If player one won, insert their new win count into the database
                                if (win == 1)
                                {
                                    gameCount = (int)reader["Wins"];
                                    gameCount++;                                 
                                    cmd.CommandText = "UPDATE Player SET Wins =" + gameCount + " WHERE Player.PlayerName = '" + playerOne.Name + "'";
                                }

                                // If player two won, insert player one's new loss count into the database
                                else if (win == 2)
                                {
                                    gameCount = (int)reader["Losses"];
                                    gameCount++;
                                    cmd.CommandText = "UPDATE Player SET Losses =" + gameCount + " WHERE Player.PlayerName = '" + playerOne.Name + "'";
                                }

                                // If it was a tie, insert player one's new tie count into the database
                                else
                                {
                                    gameCount = (int)reader["Ties"];
                                    gameCount++;
                                    cmd.CommandText = "UPDATE Player SET Ties =" + gameCount + " WHERE Player.PlayerName = '" + playerOne.Name + "'";
                                }

                                #endregion
                            }
                        }

                        cmd.ExecuteNonQuery();

                        #endregion

                        #region Player Two Exists

                        cmd.CommandText = "select PlayerID, PlayerName, Wins, Losses, Ties from Player where Player.PlayerName = '" + playerTwo.Name + "'";
                        using (reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // We now know the player exists in the table
                                // We do not need to add them
                                playerTwoExists = true;
                                playerTwoID = (int)reader["PlayerID"];

                                #region Add Player Two's Win/Loss/Tie to Database
                                // If player two won, insert their new win count into the database
                                if (win == 2)
                                {
                                    gameCount = (int)reader["Wins"];
                                    gameCount++;
                                    cmd.CommandText = "UPDATE Player SET Wins =" + gameCount + " WHERE Player.PlayerName = '" + playerTwo.Name + "'";                   
                                }

                                // If player two won, insert player two's new loss count into the database
                                else if (win == 1)
                                {
                                    gameCount = (int)reader["Losses"];
                                    gameCount++;
                                    cmd.CommandText = "UPDATE Player SET Losses =" + gameCount + " WHERE Player.PlayerName = '" + playerTwo.Name + "'";                                            
                                }

                                // If it was a tie, insert player two's new tie count into the database
                                else
                                {
                                    gameCount = (int)reader["Ties"];
                                    gameCount++;
                                    cmd.CommandText = "UPDATE Player SET Ties =" + gameCount + " WHERE Player.PlayerName = '" + playerTwo.Name + "'";                   
                                }

                                #endregion
                            }
                        }

                        cmd.ExecuteNonQuery();

                        #endregion

                        #region Player One DNE
                        // If they do not exist in the database, add them
                        if (!playerOneExists)
                        {
                            // Add playerOne to database and get his/her playerID
                            //cmd.CommandText = "INSERT Player (PlayerName) VALUES ('" + playerOne.Name + "')";

                            #region Insert player into the table with inital win/loss/tie
                            if (win == 1)
                                cmd.CommandText = "INSERT Player (PlayerName, Wins) VALUES ('" + playerOne.Name + "', " + 1 + ")";
                            else if (win == 2)
                                cmd.CommandText = "INSERT Player (PlayerName, Losses) VALUES ('" + playerOne.Name + "', " + 1 + ")";
                            else
                                cmd.CommandText = "INSERT Player (PlayerName, Ties) VALUES ('" + playerOne.Name + "', " + 1 + ")";
                            
                            cmd.ExecuteNonQuery();

                            #endregion

                            cmd.CommandText = "select PlayerID, PlayerName from Player where Player.PlayerName = '" + playerOne.Name + "'";
                            
                            using (reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    // Grab the player's ID
                                    playerOneID = (int)reader["PlayerID"];
                                }
                            }

                        }

                        #endregion

                        #region Player Two DNE
                        if (!playerTwoExists)
                        {
                            // Add playerTwo to database and get his/her playerID
                            //cmd.CommandText = "INSERT Player (PlayerName) VALUES ('" + playerTwo.Name + "')";

                            #region Insert player into the table with inital win/loss/tie
                            if (win == 2)
                                cmd.CommandText = "INSERT Player (PlayerName, Wins) VALUES ('" + playerTwo.Name + "', " + 1 + ")";
                            else if (win == 1)
                                cmd.CommandText = "INSERT Player (PlayerName, Losses) VALUES ('" + playerTwo.Name + "', " + 1 + ")";
                            else
                                cmd.CommandText = "INSERT Player (PlayerName, Ties) VALUES ('" + playerTwo.Name + "', " + 1 + ")";

                            cmd.ExecuteNonQuery();
                            #endregion

                            cmd.CommandText = "select PlayerID, PlayerName from Player where Player.PlayerName = '" + playerTwo.Name + "'";
                            using (reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    // Grab the player's ID
                                    playerTwoID = (int)reader["PlayerID"];
                                }
                            }
                        }
                        #endregion

                        #endregion

                        #region Add Game

                        DateTime currentTime = DateTime.Now;

                        // Now, add a new game to the game table
                        cmd.CommandText = "INSERT Game (Player1ID, Player1Name, Player2ID, Player2Name, GameDate, Board, TimeLimit, Player1Score, Player2Score) VALUES (" + playerOneID + ", '" + playerOne.Name + "', " + playerTwoID + ", '" + playerTwo.Name + "', '" + currentTime + "', " + "'" + this.b.ToString() +"', " + this.gameTime + ", " + this.playerOne.Score +", " + this.playerTwo.Score + ")";
                        cmd.ExecuteNonQuery();

                        // Now get the game ID
                        cmd.CommandText = "select GameID from Game where Game.GameDate = '" + currentTime + "'";
                        using (reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Grab the player's ID
                                gameID = (int)reader["GameID"];
                            }
                        }

                        #endregion

                        #region Add Words

                        // Populate the Words table
                        foreach(String word in this.playerOne.LegalWords)
                        {
                            // Now, add all words to the words table
                            // Create a command
                            cmd.CommandText = "INSERT Words (Word, GameID, PlayerID, Type) VALUES ('" + word + "', " + gameID + ", " + playerOneID + ", " + "'Legal')";
                            cmd.ExecuteNonQuery();
                        }

                        foreach (String word in this.playerOne.IllegalWords)
                        {
                            // Now, add all words to the words table
                            // Create a command
                            cmd.CommandText = "INSERT Words (Word, GameID, PlayerID, Type) VALUES ('" + word + "', " + gameID + ", " + playerOneID + ", " + "'Illegal')";
                            cmd.ExecuteNonQuery();
                        }

                        foreach (String word in this.playerOne.DuplicateWords)
                        {
                            // Now, add all words to the words table
                            // Create a command
                            cmd.CommandText = "INSERT Words (Word, GameID, PlayerID, Type) VALUES ('" + word + "', " + gameID + ", " + playerOneID + ", " + "'Duplicate')";
                            cmd.ExecuteNonQuery();
                        }

                        // Player two additions

                        foreach (String word in this.playerTwo.DuplicateWords)
                        {
                            // Now, add all words to the words table
                            // Create a command
                            cmd.CommandText = "INSERT Words (Word, GameID, PlayerID, Type) VALUES ('" + word + "', " + gameID + ", " + playerTwoID + ", " + "'Duplicate')";
                            cmd.ExecuteNonQuery();
                        }

                        foreach (String word in this.playerTwo.LegalWords)
                        {
                            // Now, add all words to the words table
                            // Create a command
                            cmd.CommandText = "INSERT Words (Word, GameID, PlayerID, Type) VALUES ('" + word + "', " + gameID + ", " + playerTwoID + ", " + "'Legal')";
                            cmd.ExecuteNonQuery();
                        }


                        foreach (String word in this.playerTwo.IllegalWords)
                        {
                            // Now, add all words to the words table
                            // Create a command
                            cmd.CommandText = "INSERT Words (Word, GameID, PlayerID, Type) VALUES ('" + word + "', " + gameID + ", " + playerTwoID + ", " + "'Illegal')";
                            cmd.ExecuteNonQuery();
                        }

                        #endregion

                        conn.Close();

                    }
                    catch (Exception e)
                    {
                    }
                }
            }

            #endregion
        }

        /// <summary>
        /// Encapsulating class holding a players name and associated string socket
        /// </summary>
        private class Player
        {
            // Instance variables for a player object:
            private StringSocket ss;
            private string name;
            private int score;
            private HashSet<string> legalWords;
            private HashSet<string> illegalWords;
            private HashSet<string> duplicateWords;
            Game game;

            /// <summary>
            /// Constructor requires a StringSocket object and the player's name.
            /// </summary>
            /// <param name="payloadReceive"></param>
            /// <param name="method"></param>
            public Player(StringSocket _ss, string _name)
            {
                // Initialize instance variables:
                this.ss = _ss;
                this.name = _name;
                this.legalWords = new HashSet<string>();
                this.illegalWords = new HashSet<string>();
                this.duplicateWords = new HashSet<string>();
                this.score = 0;
                game = null;
            }

            public Player()
            {
                // Initialize instance variables:
                this.ss = null;
                this.name = null;
                this.legalWords = new HashSet<string>();
                this.illegalWords = new HashSet<string>();
                this.duplicateWords = new HashSet<string>();
                this.score = 0;
                game = null;
            }

            /// <summary>
            /// Property which returns the payload identifier.
            /// </summary>
            public StringSocket StringSocket
            {
                get { return ss; }
            }

            /// <summary>
            /// Property which returns the request callback.
            /// </summary>
            public string Name
            {
                set { name = value; }
                get { return name; }
            }

            /// <summary>
            /// Property which returns the players game
            /// </summary>
            public Game Game
            {
                get { return game; }
                set { game = value; }
            }

            /// <summary>
            /// Property which returns the set of legal words played by this player.
            /// </summary>
            public HashSet<string> LegalWords
            {
                get { return legalWords; }
            }
            /// <summary>
            /// Property which returns the set of illegal words played by this player.
            /// </summary>
            public HashSet<string> IllegalWords
            {
                get { return illegalWords; }
            }
            /// <summary>
            /// Property which returns the duplicate words played by the player.
            /// </summary>
            public HashSet<string> DuplicateWords
            {
                get { return duplicateWords; }
            }

            /// <summary>
            /// Property which returns the player's score.
            /// </summary>
            public int Score
            {
                get { return score; }
                set { score = value; }
            }
        }

        #endregion

        #region Post Game

        /// <summary>
        /// Stop the TCP Listeners
        /// </summary>
        public void Stop()
        {
            boggleServer.Stop();
            webServer.Stop();
        }

        #endregion
    }
}
