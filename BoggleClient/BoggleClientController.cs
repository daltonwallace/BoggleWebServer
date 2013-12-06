using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BoggleClientModel;

namespace BoggleClient
{
    public partial class BoggleClientController : Form
    {
        #region Globals and Constructor

        // Create a model that will communicate with the server
        private Model model;

        /// <summary>
        /// Creates a new model and registers various line events
        /// </summary>
        public BoggleClientController()
        {
            InitializeComponent();

            // Initialize model component:
            model = new Model();

            // Add events for various incoming lines
            model.StartLineEvent += StartReceived;
            model.TimeLineEvent += TimeReceived;
            model.ScoreLineEvent += ScoreReceived;
            model.StopLineEvent += StopReceived;
            model.IgnoreLineEvent += IgnoreReceived;
            model.TerminateLineEvent += TerminateReceived;
        }

        #endregion

        #region Button and KeyDown Methods

        /// <summary>
        /// Connect to the server and enter the player in the queue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void startButton_Click(object sender, EventArgs e)
        {
            // Validate that the IP and player name boxes are not empty
            if (ipBox.Text != string.Empty && nameBox.Text != string.Empty)
            {
                // Connect to the Boggle Server
                model.Connect(ipBox.Text, 2000, nameBox.Text);
            }
        }

        /// <summary>
        /// Exit the player from the current game or player queue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitButton_Click(object sender, EventArgs e)
        {
            // TODO:
        }

        /// <summary>
        /// Send a word play to the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void enterButton_Click(object sender, EventArgs e)
        {
            // Attempt to send the word
            sendWord(wordBox.Text);
        }

        /// <summary>
        /// If the player hit enter, then the word in the wordBox will be sent to the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BoggleClientController_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Attempt to send the word
                sendWord(wordBox.Text);

                e.Handled = true;
            }
        }

        #endregion

        #region Attempt To Send Word

        /// <summary>
        /// Sends the word to the server, or turns the wordBox red
        /// </summary>
        /// <param name="word"></param>
        private void sendWord(string word)
        {
            // Validate that the wordBox is not empty
            if (word != string.Empty)
            {
                // Send the message to the server
                model.SendMessage("WORD " + word);

                wordBox.Text = "";
            }
            else
            {
                // Turn wordBox red to notify that nothing was sent
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
            // Invoke a text change for the game status
            statusBox.Invoke(new Action(() => { statusBox.Text = "Begin Game"; statusBox.BackColor = Color.ForestGreen; }));

            #region Set Up Boggle Board
            // Set up the Boggle Board with startInfo[0]
            int index = 0;

            foreach (char c in startInfo[0])
            {
                // Create an array holding all the labels
                System.Windows.Forms.Label[] labels = new System.Windows.Forms.Label[16] { letter1, letter2, letter3, letter4, letter5, letter6, letter7, letter8, letter9, letter10, letter11, letter12, letter13, letter14, letter15, letter16 };

                if (c == 'Q')
                {
                    labels[index].Invoke(new Action(() => { labels[index].Text = "QU"; }));
                }
                else
                {
                    labels[index].Invoke(new Action(() => { labels[index].Text = c.ToString().ToUpper(); }));
                }

                index++;
            }
            #endregion

            // Set the timeBox to initial time with startInfo[1]
            timeBox.Invoke(new Action(() => { timeBox.Text = startInfo[1]; }));

            // Set the opponenents name in opponentScoreLabel startInfo[2]
            opponentScoreLabel.Invoke(new Action(() => { opponentScoreLabel.Text = startInfo[2].ToUpper() + " Score"; }));
        }

        /// <summary>
        /// Updates the time label during the game
        /// </summary>
        /// <param name="text"></param>
        private void TimeReceived(string currentTime)
        {
            // Update the timeBox
            statusLabel.Invoke(new Action(() => { timeBox.Text = currentTime; }));
        }

        /// <summary>
        /// Updates the score during the game
        /// </summary>
        /// <param name="scores"></param>
        private void ScoreReceived(string[] scores)
        {
            // Update players score
            playerScoreBox.Invoke(new Action(() => { playerScoreBox.Text = scores[0]; }));

            // Update opponents score
            opponentScoreBox.Invoke(new Action(() => { opponentScoreBox.Text = scores[1]; }));
        }

        /// <summary>
        /// Presents the game summary to the user
        /// </summary>
        /// <param name="text"></param>
        private void StopReceived(string[] summaryInfo)
        {
            //statusLabel.Invoke(new Action(() => { statusLabel.Text = "Game is afoot."; }));

            // Display the summary to the user after the game has ended
            MessageBox.Show("This should be a summary");
        }

        /// <summary>
        /// Notify the user that we are ignoring the submission
        /// </summary>
        /// <param name="text"></param>
        private void IgnoreReceived(string ignoring)
        {
            // Set wordBox color to red
            wordBox.Invoke(new Action(() => { wordBox.Text = "IGNORING"; }));
        }

        /// <summary>
        /// Notify the user that the game has ended prematurely
        /// </summary>
        /// <param name="text"></param>
        private void TerminateReceived(string ignoring)
        {
            // Display Pop Up 
            MessageBox.Show("Your opponent has left the game");
        }

        #endregion

        private void exitButton_Click_1(object sender, EventArgs e)
        {

        }

    }
}
