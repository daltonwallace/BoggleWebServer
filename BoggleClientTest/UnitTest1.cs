using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BoggleClient;
using System.Windows.Forms;

namespace BoggleClientTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestSummaryMessage()
        {
            BoggleClientController tester = new BoggleClientController();
 
            string[] testArray = new string[]{"0","0"};

            string summary = tester.SummaryMessage(testArray);

        }
    }
}
