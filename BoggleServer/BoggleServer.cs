﻿using System;
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

        #endregion

        #region Pre-Game Warmup

        /// <summary>
        /// Receives argument from user.  Requires 2 - 3 arguments to begin boggle game,
        /// otherwise it will throw an exception.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //args = new String[] { "120", "C:/Users/Dalton/Desktop/School/CS 3500/Assignments/PS8Git/dictionary.txt", "" };

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

            // Ask our new boggle server to call a specific method once a connection arrives
            // the waiting and calling will happen on another thread.  This call will return immediately
            // and the constructor will return to main
            boggleServer.BeginAcceptSocket(ConnectionRequested, null);
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
                }
            }

            /// <summary>
            /// Ends the game for two players by printing all stats and closing the both sockets.
            /// </summary>
            private void playerOneSummary()
            {
                gameOver = true;

                // Prints the stats for player One and when it has completed proceeds to 
                playerOne.StringSocket.BeginSend("STOP " + printStats(playerOne, playerOne, playerOne.LegalWords) + printStats(playerOne, playerTwo, playerTwo.LegalWords) + printStats(playerOne, playerOne, playerOne.DuplicateWords) + printStats(playerOne, playerOne, playerOne.IllegalWords) + printStats(playerOne, playerTwo,playerTwo.IllegalWords) + "\n", (exc, o) => { }, 2);
            }

            /// <summary>
            /// Prints the stats for the second player and then closes the string sockets
            /// </summary>
            private void playerTwoSummary()
            {
                playerTwo.StringSocket.BeginSend("STOP " + printStats(playerTwo, playerTwo, playerTwo.LegalWords) + printStats(playerTwo, playerOne,            playerOne.LegalWords) +printStats(playerTwo, playerTwo, playerTwo.DuplicateWords) + printStats(playerTwo, playerTwo, playerTwo.IllegalWords) + printStats(playerTwo, playerOne,playerOne.IllegalWords) + "\n", (exc, o) => { playerTwo.StringSocket.Close(); }, 2);
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
        /// Stop the TCP Listener
        /// </summary>
        public void Stop()
        {
            boggleServer.Stop();
        }

        #endregion
    }
}
