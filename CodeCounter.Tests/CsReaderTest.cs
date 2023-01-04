using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace CodeCounter.Tests
{
    [TestClass()]
    public class CsReaderTest
    {
        const string kPathSelf = "../../../..";
 
        [TestMethod()]
        public void TestCsReadSelf()
        {
            // Analyze my own code.

            var memoryStream = new MemoryStream();
            TextWriter conOut = new StreamWriter(memoryStream);

            var stats = new CodeStats(conOut);
            stats.MakeTree = true;

            Assert.IsTrue(stats != null);
            stats.ReadRoot(kPathSelf);      // do work.

            Assert.IsTrue(stats.NumberOfProjects == 2);
            Assert.IsTrue(stats.NumberOfDirectories >= 2);
            Assert.IsTrue(stats.NumberOfClasses >= 26);

            // string outStr = conOut
        }
    }
}
